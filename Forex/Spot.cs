using Microsoft.Extensions.Logging;
using PriceProducer;
using System.Collections.Concurrent;

namespace Forex;

public class Spot : ISpot
{
    private readonly ILogger<Spot> _logger;
    private Random _random = new Random();
    private string _filePath;
    private State _currentstate = State.NotSetUp;
    private TickDefinition[] _tickDefinitions;

    private ConcurrentDictionary<string, (TickDefinition tickDefinition, PriceLimit priceLimit)> _tickDefinitionsDictionary;
    internal ConcurrentDictionary<string, (double[] rates, int updateFrequency)> _currencyPairs;
    internal ConcurrentDictionary<int, ConcurrentQueue<string>> _pairsByFrequency;
    internal ConcurrentDictionary<int, System.Timers.Timer> _timersByFrequency;    

    internal FileDataExtractor _fileDataExtractor;

    private Pricer _pricer;
    private readonly int PUBLISH_FREQUENCY = 100;

    public event OnTickUpdate OnTickUpdate;

    internal Spot()
    { 
        _logger = new LoggerFactory().CreateLogger<Spot>();
    }

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
        _tickDefinitionsDictionary = new ConcurrentDictionary<string, (TickDefinition tickDefinition, PriceLimit priceLimit)>();
        Initialize();
    }

    /// <summary>
    /// Initializes the data structures, schedule and start the timers.
    /// </summary>
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

    /// <summary>
    /// Pauses the tick publishing.
    /// </summary>
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

    /// <summary>
    /// Resumes the tick publishing.
    /// </summary>
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

    /// <summary>
    /// Stops the publishing. Clears all data structures and stops all timers.
    /// </summary>
    public void Stop()
    {
        _logger.LogInformation($"Stopping timers.");
        ClearDataStructuresAndStopTimers();
    }

    /// <summary>
    /// Adds a tick definition to the list of tick definitions.
    /// For the new tick to take into effect, the Start() method needs to be called.
    /// </summary>
    /// <param name="tickDefinition"></param>
    public void AddTickDefinition(TickDefinition tickDefinition)
    {
        if (!tickDefinition.IsTickDefinitionValid())
        {
            _logger.LogWarning($"Invalid tick definition: {tickDefinition}");
            return;
        }

        PriceLimit priceLimit = null;
        priceLimit = GetPriceLimit(tickDefinition);
        if (priceLimit == null)
        {
            _logger.LogWarning($"Invalid tick definition: {tickDefinition}. Not adding for Processing!");
            return;
        }

        _logger.LogInformation($"Price Limit for {tickDefinition.CurrencyPair} is {priceLimit}");        

        _tickDefinitionsDictionary.AddOrUpdate(tickDefinition.CurrencyPair, (tickDefinition, priceLimit),
            (key, oldValue) =>
            {
                _logger.LogWarning($"Tick definition already exists for {key}. Overwriting with new value. {tickDefinition}");
                return (tickDefinition, priceLimit);
            });
        _logger.LogInformation($"Added tick definition: {tickDefinition}");
    }

    public string[] Ticks => _tickDefinitionsDictionary.Values.Select(cp => cp.tickDefinition.CurrencyPair).ToArray();

    public State CurrentState => _currentstate;

    public TickDefinition this[string currencyPair] => _tickDefinitionsDictionary[currencyPair].tickDefinition;

    public (string ccyPair, int frequency)[] GetScheduledTicks()
    {
        return _currencyPairs.Select(item => (item.Key, item.Value.updateFrequency)).ToArray();
    }

    internal PriceLimit GetPriceLimit(TickDefinition tickDefinition)
    {
        PriceLimit priceLimit = null;
        try
        {
            priceLimit = _pricer.SetPriceLimitForBidAskSpread(tickDefinition.Bid, tickDefinition.Ask, tickDefinition.Spread);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, $"Error while setting price limit for {tickDefinition}");
        }

        return priceLimit;
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

        _currentstate = _tickDefinitionsDictionary.Any() ? State.SetUp : State.NotSetUp;
        _logger.LogInformation($"Current State: {CurrentState}");
    }

    private void ScheduleTimers()
    {
        foreach (var tdpl in _tickDefinitionsDictionary.Values)
        {
            int publishFrequency = tdpl.tickDefinition.PublishFrequencyInMs.AdjustedPublishTime(PUBLISH_FREQUENCY);
            var ccyPair = tdpl.tickDefinition.CurrencyPair;
            var dataTuple = (new double[] { tdpl.tickDefinition.Bid, tdpl.tickDefinition.Ask, tdpl.tickDefinition.Spread }, publishFrequency);

            _currencyPairs.AddOrUpdate(ccyPair, dataTuple, (key, oldValue) => dataTuple);
            if (!_pairsByFrequency.ContainsKey(publishFrequency))
            {
                _pairsByFrequency.TryAdd(publishFrequency, new ConcurrentQueue<string>());

                // Create the timer and add it to timersByFrequency
                var timer = new System.Timers.Timer(publishFrequency);
                timer.Elapsed += (sender, e) => PublishTimerElapsed(publishFrequency);
                timer.AutoReset = true;
                _timersByFrequency.TryAdd(publishFrequency, timer);
            }
            _logger.LogInformation($"Scheduled {ccyPair} to be published every {publishFrequency} ms");
            _pairsByFrequency[publishFrequency].Enqueue(ccyPair);
        }
    }

    private void PublishTimerElapsed(int updateFrequency)
    {
        var timer = _timersByFrequency[updateFrequency];
        timer.Stop();
        PublishNextTick(updateFrequency);
        timer.Start();
    }

    private void PublishNextTick(int updateFrequency)
    {
        if (_pairsByFrequency.TryGetValue(updateFrequency, out var pairs))
        {
            Parallel.ForEach(pairs, pairKey =>
            {
                if (_currencyPairs.TryGetValue(pairKey, out var pairInfo))
                {
                    var rates = pairInfo.rates;

                    double fraction = _random.NextDouble() / 100 * (_random.Next(2) % 2 == 0 ? 1 : -1) / 2;
                    _pricer.NextPrice(rates, fraction, _random.Next(2) % 2 == 0, _tickDefinitionsDictionary[pairKey].priceLimit);
                    rates[2] = Math.Abs(rates[0] + rates[1]) / 2;
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug($"Updated {pairKey} with {rates[0]}, {rates[1]}, {rates[2]}");
                    OnTickUpdate?.Invoke(pairKey + " : " + rates[0] + ", " + rates[1] + ", " + rates[2]);
                }
            });
        }
    }

    private void InitializeDataStructures()
    {
        _currencyPairs = new ConcurrentDictionary<string, (double[], int)>();
        _pairsByFrequency = new ConcurrentDictionary<int, ConcurrentQueue<string>>();
        _timersByFrequency = new ConcurrentDictionary<int, System.Timers.Timer>();
    }

    private void ClearDataStructuresAndStopTimers()
    {
        _logger.LogInformation($"Clearing data structures and stopping timers.");
        _currencyPairs?.Clear();
        _pairsByFrequency?.Clear();
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