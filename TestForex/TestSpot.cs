using Forex;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace TestForex;

public class TestSpot
{
    private string _testFilePath;
    private const string TestForexSpotFileName = "Forex.Spot.TestFile.txt";
    private Random _random = new Random();

    [SetUp]
    public void Setup()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), TestForexSpotFileName);
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Test]
    public void ILoggerFactoryConstructor_Initialization_ReturnsISpot()
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        ISpot spot = new Spot(loggerFactory);
        Assert.IsNotNull(spot);
    }

    [Test]
    public void ConstructorWithLoggerAndFile_Initialization_ReturnsISpot()
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        string filePath = "";
        ISpot spot = new Spot(loggerFactory, filePath);
        Assert.IsNotNull(spot);
    }

    [Test]
    public void ConstructorWithLoggerAndTickDefinitions_Initialization_ReturnsISpot()
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        TickDefinition[] tickDefinitions = new TickDefinition[1];
        tickDefinitions[0] = new TickDefinition("EURUSD", 1.0, 1.0004, 1.0, 1);
        ISpot spot = new Spot(loggerFactory, tickDefinitions);
        Assert.IsNotNull(spot);
    }

    [Test]
    public void AddTickDefinition_ValidInput_ThrowsNoException()
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        ISpot spot = new Spot(loggerFactory);
        TickDefinition tickDefinition = new TickDefinition("EURUSD", 1.0, 1.0, 1.0, 1);
        spot.AddTickDefinition(tickDefinition);
        Assert.That(spot.Ticks.Count, Is.EqualTo(1));
    }

    [Test]
    public void AddTickDefinition_DuplicateEntry_KeepsSingleEntry()
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        ISpot spot = new Spot(loggerFactory);
        TickDefinition tickDefinition = new TickDefinition("EURUSD", 1.0, 1.0, 1.0, 1);
        spot.AddTickDefinition(tickDefinition);
        spot.AddTickDefinition(tickDefinition);
        Assert.That(spot.Ticks.Count, Is.EqualTo(1));
    }

    [Test]
    public void AddTickDefinition_DuplicateEntry_KeepsLastEntry()
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        ISpot spot = new Spot(loggerFactory);
        TickDefinition tickDefinition = new TickDefinition("EURUSD", 1.0, 1.0, 1.0, 1);
        spot.AddTickDefinition(tickDefinition);
        tickDefinition = new TickDefinition("EURUSD", 2.0, 2.0, 2.0, 1);
        spot.AddTickDefinition(tickDefinition);
        TickDefinition eurusd = spot["EURUSD"];
        Assert.IsTrue(Double.Equals(eurusd.Bid, 2.0) && Double.Equals(eurusd.Ask, 2.0) && Double.Equals(eurusd.Spread, 2.0) && eurusd.PublishFrequencyInMs == 1);
    }

    [Test]
    public void Start_WithEmptyCollection_Returns0Count()
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        ISpot spot = new Spot(loggerFactory);
        spot.Start();
        Assert.That(spot.GetScheduledTicks().Count, Is.EqualTo(0));
    }

    [TestCase("CP|BID|ASK|LAST|PublishFrequencyInMs")]
    [TestCase("CurrencyPair|Bid|ASK|LAST|PublishFrequencyInMs")]
    [TestCase("Currency Pair|Bid|Ask|Last|PublishFrequencyInMs")]
    [TestCase("CurrencyPair,Bid,ASK,LAST|PublishFrequencyInMs")]
    [TestCase("Currency Pair,Bid,Ask,Last,PublishFrequencyInMs")]
    public void Start_WithIncorrectHeader_StateIsNotSetUp(string header)
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        using (StreamWriter sw = new StreamWriter(_testFilePath))
        {
            sw.WriteLine(header);
            sw.WriteLine("EURUSD|1.0|1.0|1.0|1");
            sw.WriteLine("EURUSD|2.0|2.0|2.0|1");
        }
        ISpot spot = new Spot(loggerFactory, _testFilePath);
        Assert.IsTrue(spot.CurrentState == State.NotSetUp);
    }

    [Test]
    public void Start_WithIncorrectHeaderFormat_StateIsNotSetUp()
    {
        //Expected to be ',' delimited
        ILoggerFactory loggerFactory = new LoggerFactory();
        using (StreamWriter sw = new StreamWriter(_testFilePath))
        {            
            sw.WriteLine("CurrencyPair|Bid|Ask|Last|PublishFrequencyInMs");
            sw.WriteLine("EURUSD|1.0|1.0|1.0|1");
            sw.WriteLine("EURUSD|2.0|2.0|2.0|1");
        }
        ISpot spot = new Spot(loggerFactory, _testFilePath);
        Assert.IsTrue(spot.CurrentState == State.NotSetUp);
    }

    [Test]    
    public void Start_WithCorrectHeader_ReturnsInstance()
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        using (StreamWriter sw = new StreamWriter(_testFilePath))
        {            
            sw.WriteLine("CurrencyPair,Bid,Ask,Last,PublishFrequencyInMs");
            sw.WriteLine("EURUSD|1.0|1.0|1.0,1");
            sw.WriteLine("EURUSD|2.0|2.0|2.0,1");
        }
        ISpot spot = new Spot(loggerFactory, _testFilePath);
        Assert.IsNotNull(spot);
    }

    [Test]
    public void Start_WithAdditionalHeaderItem_ReturnsInstance()
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        using (StreamWriter sw = new StreamWriter(_testFilePath))
        {
            sw.WriteLine("CurrencyPair,Bid,Ask,Last,PublishFrequencyInMs,LastVolume");
            sw.WriteLine("EURUSD|1.0|1.0|1.0,1");
            sw.WriteLine("EURUSD|2.0|2.0|2.0,1");
        }
        ISpot spot = new Spot(loggerFactory, _testFilePath);
        Assert.IsNotNull(spot);
    }

    [Test]
    public void Start_WithLessHeaderItem_StateIsNotSetUp()
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        using (StreamWriter sw = new StreamWriter(_testFilePath))
        {
            sw.WriteLine("CurrencyPair,Bid,Ask,PublishFrequencyInMs");
            sw.WriteLine("EURUSD|1.0|1.0|1.0,1");
            sw.WriteLine("EURUSD|2.0|2.0|2.0,1");
        }
        ISpot spot = new Spot(loggerFactory, _testFilePath);
        Assert.IsTrue(spot.CurrentState == State.NotSetUp);
    }

    [Test]
    public void Start_WithIncorrectDataItems_ReturnsEmptyCollection()
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        using (StreamWriter sw = new StreamWriter(_testFilePath))
        {
            sw.WriteLine("CurrencyPair,Bid,Ask,Spread,PublishFrequencyInMs");
            sw.WriteLine("EURUSD|1.0|1.0|1.0|1");
            sw.WriteLine("EURUSD|2.0|2.0,2.0|1");
        }
        ISpot spot = new Spot(loggerFactory, _testFilePath);
        spot.Start();
        Assert.That(spot.GetScheduledTicks().Count, Is.EqualTo(0));
    }

    [Test]
    public void Start_WithCorrectDataItems_ReturnsSingleItem()
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        using (StreamWriter sw = new StreamWriter(_testFilePath))
        {
            sw.WriteLine("CurrencyPair,Bid,Ask,Spread,PublishFrequencyInMs");
            sw.WriteLine("EURUSD,1.0,1.0,1.0,1");
            sw.WriteLine("EURUSD,2.0,2.0,2.0,1");
        }
        ISpot spot = new Spot(loggerFactory, _testFilePath);
        spot.Start();
        Assert.That(spot.GetScheduledTicks().Count, Is.EqualTo(1));
    }

    [TestCase(10, 100, 100)]
    [TestCase(175, 180, 10)]
    [TestCase(35, 50, 50)]
    [TestCase(0, 50, 50)]
    [TestCase(-100, 50, 50)]
    public void NextAdjustedPublishTime_ValidInputs_ReturnsValidResponse(int inputFrequency, int expectedFrequency, int nearestRoundFrequency = 100)
    {
        Assert.That(expectedFrequency, Is.EqualTo(inputFrequency.AdjustedPublishTime(nearestRoundFrequency)));
    }

    [Test]
    public void Start_With_EURUSD_USDJPY_USDGBP_DataItems_ReturnsCountThree()
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        using (StreamWriter sw = new StreamWriter(_testFilePath))
        {
            sw.WriteLine("CurrencyPair,Bid,Ask,Spread,PublishFrequencyInMs");
            sw.WriteLine("EURUSD,1.0,1.0,1.0,1");
            sw.WriteLine("USDJPY,2.0,2.0,2.0,1");
            sw.WriteLine("USDGBP,2.0,2.0,2.0,1");
        }
        ISpot spot = new Spot(loggerFactory, _testFilePath);
        spot.Start();
        Assert.That(spot.GetScheduledTicks().Count, Is.EqualTo(3));
    }

    [Test]
    public void Start_WithIncorrectEURUSD_Correct_USDJPY_USDGBP_DataItems_ReturnsCountTwo()
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        using (StreamWriter sw = new StreamWriter(_testFilePath))
        {
            sw.WriteLine("CurrencyPair,Bid,Ask,Spread,PublishFrequencyInMs");
            sw.WriteLine("EURUSD,0,0,0,1");
            sw.WriteLine("USDJPY,2.0,2.0,2.0,1");
            sw.WriteLine("USDGBP,2.0,2.0,2.0,1");
        }
        ISpot spot = new Spot(loggerFactory, _testFilePath);
        spot.Start();
        Assert.That(spot.GetScheduledTicks().Count, Is.EqualTo(2));
    }

    [TestCase("EURUSD,1.0,2.0,0,1")]//has 0 as last
    [TestCase("EURUSD,1.0,2.0,-1.0,1")]//has -ve as last
    public void Start_WithAnyIncorrectEURUSD_Correct_USDJPY_USDGBP_DataItems_ReturnsCountTwo(string data)
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        using (StreamWriter sw = new StreamWriter(_testFilePath))
        {
            sw.WriteLine("CurrencyPair,Bid,Ask,Spread,PublishFrequencyInMs");
            sw.WriteLine(data);
            sw.WriteLine("USDJPY,2.0,2.0,2.0,1");
            sw.WriteLine("USDGBP,2.0,2.0,2.0,1");
        }
        ISpot spot = new Spot(loggerFactory, _testFilePath);
        spot.Start();
        Assert.That(spot.GetScheduledTicks().Count, Is.EqualTo(2));
    }

    [Test]
    public void Start_WithThreeCorrectTickerDefinition_SchedulesThree()
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        var tickerDefn = new TickDefinition[]
        {
            new TickDefinition("EURUSD", 1, 1, 1, 1),
            new TickDefinition("USDJPY", 1, 1, 1, 1),
            new TickDefinition("USDGBP", 1, 1, 1, 1)
        };
        ISpot spot = new Spot(loggerFactory, tickerDefn);
        spot.Start();
        Assert.That(spot.GetScheduledTicks().Count, Is.EqualTo(3));
    }

    [Test]
    public void Start_WithDuplicateEURUSDTickerDefinition_SchedulesEURUSD()
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        var tickerDefn = new TickDefinition[]
        {
            new TickDefinition("EURUSD", 1, 1, 1, 1),
            new TickDefinition("EURUSD", 1, 1, 1, 1),
            new TickDefinition("EURUSD", 1, 1, 1, 1)
        };
        ISpot spot = new Spot(loggerFactory, tickerDefn);
        spot.Start();
        Assert.That(spot.GetScheduledTicks().Count, Is.EqualTo(1));
    }

    [Test]
    public void Start_WithIncorrectTickerDefinition_SchedulesZero()
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        var tickerDefn = new TickDefinition[]
        {
            new TickDefinition("EURUSD", 1, 1, -1, 1),
            new TickDefinition("EURUSD", 1, 0, 1, 1),            
        };
        ISpot spot = new Spot(loggerFactory, tickerDefn);
        spot.Start();
        Assert.That(spot.GetScheduledTicks().Count, Is.EqualTo(0));
    }

    [TestCase(1.2345, 1.2346, 0.2729934440436973, 1, true)]
    [TestCase(1.2345, 1.2346, 0.2729934440436973, 1, false)]
    [TestCase(1.2345, 1.2346, 0.2729934440436973, 2, true)]
    [TestCase(1.2345, 1.2346, 0.2729934440436973, 2, false)]
    public void GenerateRandomDeviation_Inputs_ConfirmAskIsGreaterThanBid(double bid, double ask, double rand, int bidOrAsk, bool addFraction)
    {
        double last = Math.Round((bid + ask) / 2, 4);

        double[] rates = new double[] {bid, ask, last };

        Spot spot = new Spot();

        spot.GenerateRandomDeviation(rates, rand, bidOrAsk, addFraction);

        Assert.IsTrue(rates[1] > rates[0]);

    }

    [TestCase(1.2345, 1.2346, 0.04, 1, true, 1.2347, 1.2348)]
    [TestCase(1.2345, 1.2346, 0.04, 1, false, 1.2343, 1.2346)]
    [TestCase(1.2345, 1.2346, 0.004, 1, true, 1.2345, 1.2346)]
    [TestCase(1.2345, 1.2346, 0.004, 1, false, 1.2345, 1.2346)]

    [TestCase(1.2345, 1.2346, 0.04, 2, true, 1.2345, 1.2348)]
    [TestCase(1.2345, 1.2346, 0.04, 2, false, 1.2343, 1.2344)]
    [TestCase(1.2345, 1.2346, 0.004, 2, true, 1.2345, 1.2346)]
    [TestCase(1.2345, 1.2346, 0.004, 2, false, 1.2345, 1.2346)]

    public void GenerateRandomDeviation_Inputs_CompareToExpectedResults(double bid, double ask, double rand, int bidOrAsk, bool addFraction, double newBid, double newAsk)
    {
        double last = Math.Round((bid + ask) / 2, 4);

        double[] rates = new double[] { bid, ask, last };

        Spot spot = new Spot();

        spot.GenerateRandomDeviation(rates, rand, bidOrAsk, addFraction);

        Assert.IsTrue(newBid == rates[0] && newAsk == rates[1]);

    }

    [TestCase(10.123, 10.124)]
    [TestCase(100.005, 100.006)]
    [TestCase(1.2345, 1.2348)]
    [TestCase(10.123, 10.127)]
    [TestCase(100.005, 100.010)]
    public void GenerateRandomDeviation_Inputs_ConfirmAskIsGreaterThanBid(double bid, double ask)
    {
        double last = Math.Round((bid + ask) / 2, 4);

        double[] rates = new double[] { bid, ask, last };

        Spot spot = new Spot();

        spot.GenerateRandomDeviation(rates, _random.NextDouble(), _random.Next(1,3), _random.Next(1, 10) % 2 == 0);

        double difference = rates[1] - rates[0];

        Assert.IsTrue(rates[1] > rates[0]);

    }
}