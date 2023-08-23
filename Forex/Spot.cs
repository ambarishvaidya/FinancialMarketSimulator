using CsvObjectify;
using CsvObjectify.Column;
using CsvObjectify.Column.Helper;
using Microsoft.Extensions.Logging;
using PriceProducer;
using System;
using System.Collections.Concurrent;

namespace Forex;

public class Spot : ISpot
{
    private readonly ILogger<Spot> _logger;
    private Random _random = new Random();
    private string _filePath;
    private State _currentstate = State.NotSetUp;
    private TickDefinition[] _tickDefinitions;
    private ConcurrentDictionary<string, TickDefinition> _tickDefinitionsDictionary;
    internal ConcurrentDictionary<string, (double[] rates, int updateFrequency)> _currencyPairs;
    internal ConcurrentDictionary<int, ConcurrentQueue<string>> _pairsByFrequency;
    internal ConcurrentDictionary<int, System.Timers.Timer> _timersByFrequency;
    private Dictionary<string, PriceLimit> _ccyPairLimitMap;
    internal FileDataExtractor _fileDataExtractor;

    private Pricer _pricer;    
    private readonly int PUBLISH_FREQUENCY = 100;

    public event OnTickUpdate OnTickUpdate;

    internal Spot()
    { }

    public Spot(ILoggerFactory loggerFactory) : this(loggerFactory, null, null)
    { }

    public Spot(ILoggerFactory loggerFactory, string filePath) : this(loggerFactory, filePath, null)
    { }

    public Spot(ILoggerFactory loggerFactory, TickDefinition[] tickDefinitions) : this(loggerFactory, null, tickDefinitions)
    {
        this._tickDefinitions = tickDefinitions;
    }

    private Spot(ILoggerFactory loggerFactory, string filePath, TickDefinition[] tickDefinitions)
    {
        this._logger = loggerFactory.CreateLogger<Spot>();        
        this._filePath = filePath;
        this._tickDefinitions = tickDefinitions;

        _pricer = new Pricer(loggerFactory);
        _fileDataExtractor = new FileDataExtractor(loggerFactory);
        _currentstate = State.NotSetUp;
        _logger.LogInformation($"Current State: {_currentstate}");
        _tickDefinitionsDictionary = new ConcurrentDictionary<string, TickDefinition>();
        Initialize();
    }

    public void Start()
    {
        if (_tickDefinitionsDictionary.Count == 0)
        {
            _logger.LogWarning("No tick definitions found.");            
        }

        ClearDataStructuresAndStopTimers();
        InitializeDataStructures();
        ScheduleTimers();

        foreach (var timer in _timersByFrequency.Values)
        {
            timer.Start();
        }
        _currentstate = _timersByFrequency.Values.Any() ? State.Started : _currentstate;
    }

    public void Pause()
    {
        if (_timersByFrequency.Count > 0)
        {
            _logger.LogInformation($"Pausing timers.");
            foreach (var timer in _timersByFrequency.Values)
            {
                timer.Stop();
            }
            _currentstate = State.Paused;
        }
        _logger.LogInformation($"Current State: {CurrentState}");
    }

    public void Resume()
    {
        if (_timersByFrequency.Count > 0)
        {
            _logger.LogInformation($"Resuming timers.");
            foreach (var timer in _timersByFrequency.Values)
            {
                timer.Start();
            }
            _currentstate = State.Resumed;
        }
        _logger.LogInformation($"Current State: {CurrentState}");
    }

    public void Stop()
    {
        _logger.LogInformation($"Stopping timers.");
        ClearDataStructuresAndStopTimers();
    }

    public void AddTickDefinition(TickDefinition tickDefinition)
    {
        if (!tickDefinition.IsTickDefinitionValid())
        {
            _logger.LogWarning($"Invalid tick definition: {tickDefinition}");
            return;
        }
        _tickDefinitionsDictionary.AddOrUpdate(tickDefinition.CurrencyPair, tickDefinition,
            (key, oldValue) => {
                _logger.LogWarning($"Tick definition already exists for {key}. Overwriting with new value. {tickDefinition}");
                return tickDefinition;
            });
        _logger.LogInformation($"Added tick definition: {tickDefinition}");
    }

    public string[] Ticks => _tickDefinitionsDictionary.Values.Select(cp => cp.CurrencyPair).ToArray();

    public State CurrentState => _currentstate;

    public TickDefinition this[string currencyPair] => _tickDefinitionsDictionary[currencyPair];

    public (string ccyPair, int frequency)[] GetScheduledTicks()
    {
        return _currencyPairs.Select(item => (item.Key, item.Value.updateFrequency)).ToArray();
    }

    private void Initialize()
    {
        _logger.LogInformation($"Initializing Spot Simulator.");
        if (!string.IsNullOrEmpty(_filePath))
        {
            _logger.LogInformation($"Parsing file for Ticker Definition : {_filePath}");
            _fileDataExtractor.ParseFile(_filePath, AddTickDefinition);
        }

        if (_tickDefinitions != null)
        {
            _logger.LogInformation($"Adding Ticker Definition.");
            foreach (TickDefinition tickDefinition in _tickDefinitions)
            {
                AddTickDefinition(tickDefinition);
            }
        }

        foreach (TickDefinition tickDefinition in _tickDefinitionsDictionary.Values)
        {
            _ccyPairLimitMap.Add(tickDefinition.CurrencyPair, _pricer.SetPriceLimitForBidAsk(tickDefinition.Bid, tickDefinition.Ask));
        }

        _currentstate = _tickDefinitionsDictionary.Any() ? State.SetUp : State.NotSetUp;
        _logger.LogInformation($"Current State: {CurrentState}");
    }

    private void ScheduleTimers()
    {
        foreach (TickDefinition tickDefinition in _tickDefinitionsDictionary.Values)
        {
            int publishFrequency = tickDefinition.PublishFrequencyInMs.AdjustedPublishTime(PUBLISH_FREQUENCY);
            var ccyPair = tickDefinition.CurrencyPair;
            var dataTuple = (new double[] { tickDefinition.Bid, tickDefinition.Ask, tickDefinition.Last }, publishFrequency);

            _currencyPairs.AddOrUpdate(ccyPair, dataTuple, (key, oldValue) => dataTuple);
            if (!_pairsByFrequency.ContainsKey(publishFrequency))
            {
                _pairsByFrequency.TryAdd(publishFrequency, new ConcurrentQueue<string>());

                // Create the timer and add it to timersByFrequency
                var timer = new System.Timers.Timer(publishFrequency);
                timer.Elapsed += (sender, e) => PublishCurrencies(publishFrequency);
                timer.AutoReset = true;
                _timersByFrequency.TryAdd(publishFrequency, timer);                
            }
            _logger.LogInformation($"Scheduled {ccyPair} to be published every {publishFrequency} ms");
            _pairsByFrequency[publishFrequency].Enqueue(ccyPair);
        }
    }

    private void PublishCurrencies(int updateFrequency)
    {
        var timer = _timersByFrequency[updateFrequency];
        timer.Stop();
        UpdateCurrencyPairs(updateFrequency);
        timer.Start();
    }

    private void UpdateCurrencyPairs(int updateFrequency)
    {
        if (_pairsByFrequency.TryGetValue(updateFrequency, out var pairs))
        {
            Parallel.ForEach(pairs, pairKey =>
            {
                if (_currencyPairs.TryGetValue(pairKey, out var pairInfo))
                {
                    var rates = pairInfo.rates;

                    double fraction = _random.NextDouble() / 100 * (_random.Next(2) % 2 == 0 ? 1 : -1) / 2;
                    _pricer.NextPrice(rates, fraction, _random.Next(2) % 2 == 0, _ccyPairLimitMap[pairKey]);
                    //GenerateRandomDeviation(rates,_random.NextDouble(), _random.Next(1, 3), _random.Next(1, 10) %  2 == 0);
                    _logger.LogInformation($"Updated {pairKey} with {rates[0]}, {rates[1]}, {rates[2]}");
                    OnTickUpdate?.Invoke(pairKey + " : " + rates[0] + ", " + rates[1] + ", "+ rates[2]);                    
                }
            });
        }
    }
    
    private void InitializeDataStructures()
    {
        _currencyPairs = new ConcurrentDictionary<string, (double[], int)>();
        _pairsByFrequency = new ConcurrentDictionary<int, ConcurrentQueue<string>>();
        _timersByFrequency = new ConcurrentDictionary<int, System.Timers.Timer>();
        _ccyPairLimitMap = new Dictionary<string, PriceLimit>();
    }

    private void ClearDataStructuresAndStopTimers()
    {
        _logger.LogInformation($"Clearing data structures and stopping timers.");
        _currencyPairs?.Clear();
        _pairsByFrequency?.Clear();
        _ccyPairLimitMap?.Clear();
        if (_timersByFrequency?.Count > 0)
        {
            foreach (var timer in _timersByFrequency.Values)
            {
                timer.Stop();
                timer.Dispose();
            }
            _timersByFrequency.Clear();

            _currentstate = State.Stopped;
        }
        _logger.LogInformation($"Current State: {CurrentState}");
    }

    internal void GenerateRandomDeviation(Span<double> rates, double random, int bidorAskOrLast, bool addFraction)
    {
        // Generate a random number between the specified minDeviation and maxDeviation
        int multiplier = addFraction ? 1 : -1;
        double fraction = (random / 100 * multiplier) / 2;
        int bidOrAskOrBoth = bidorAskOrLast > 2 ? 1 : bidorAskOrLast;//1 for bid, 2 for ask
        if (bidOrAskOrBoth == 1)
        {
            rates[0] = Math.Round(rates[0] + fraction, 4);
            if (rates[0] >= rates[1])
                rates[1] = Math.Round(rates[1] + fraction, 4);
        }
        else if (bidOrAskOrBoth == 2)
        {
            rates[1] = Math.Round(rates[1] + fraction, 4);
            if (rates[0] >= rates[1])
                rates[0] = Math.Round(rates[0] + fraction, 4);
        }

        rates[2] = Math.Round((rates[0] + rates[1]) / 2, 4);
    }
}

public enum State
{
    NotSetUp,
    SetUp,
    Started,
    Paused,
    Resumed,
    Stopped,
    Updated
}