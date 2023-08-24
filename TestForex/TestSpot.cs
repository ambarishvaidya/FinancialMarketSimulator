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

    [TestCase("EURUSD", 1, 1, -1, 1)]
    [TestCase("EURUSD", 1, 1, 0, 1)]
    [TestCase("EURUSD", 110, 110.861, 20, 1000)]
    public void GetPriceLimit_WithAnyFields_DoesNotThrowException(string ccy, double bid, double ask, double spread, int freq)
    {

        var tickDefn = new TickDefinition(ccy, bid, ask, spread, freq);
        Spot spot = new Spot();        
        Assert.DoesNotThrow(() => spot.GetPriceLimit(tickDefn));
    }    
}