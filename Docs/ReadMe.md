# Financial Market Simulator

The Financial Market Data Simulator is a versatile tool designed to generate synthetic market data across various financial markets. By providing ticker 
symbols and their corresponding initial values, coupled with a defined publish frequency, this library empowers users to effortlessly simulate market conditions 
and trends. Whether you're working on algorithmic trading strategies, conducting market research, or testing financial applications, the Financial Market 
Data Simulator offers a streamlined way to generate and work with dummy data. 

## Dependencies
  - [CsvObjectify](https://github.com/ambarishvaidya/CsvObjectify)
  - [PriceProducer](https://github.com/ambarishvaidya/MarketDataProducer) 
  - Microsoft.Extensions.Logging

## Features 

- **Customizable**: Define your own ticker symbols, initial values, and publish frequency to simulate market conditions and trends.
- **Realistic**: The Financial Market Data Simulator uses a random walk model to generate realistic market data.
- **Efficient**: Library uses PriceProducer that produces tick within PriceLimit configured internally for each ticker symbol.
- **Operations**: The library exposes operations to Start, Pause, Resume and Stop the publishing.
- **Logging**: The library uses Microsoft Extenstions for logging.

## Working

Forex uses CsvObjectify to parse file for ticker symbols and their corresponding initial values. Each ticker symbol in the file is parsed into TickerDefinition. 
The library uses PriceProducer that produces tick within PriceLimit which created from TickerDefinition for each ticker symbol. 
Timers are configured for tickers with same publish frequency. On the timer elapsed event, a random variation is added to each ticker configured for that timer.
Consuming application receives callback for each new ticker rate.

## Usage

**TickDefinition** is the main class that defines a ticker symbol and its corresponding initial values. It is created either from a file or constructed manually.
The file can have any name and extension, but needs to have the following header
```csv
CurrencyPair,Bid,Ask,Spread,PublishFrequencyInMs
```
Each TickerDefinition creates a PriceLimit - from PriceProducer - which is then used to generate random tick within the limit.

Spot relies on **ISpot** that exposes methods that can be used to get status of Publisher the current Tickers that are configured for publishing and some house keeping operations.
```csharp
public interface ISpot
{
    void Start();
    void Pause();
    void Resume();
    void Stop();

    void AddTickDefinition(TickDefinition tickDefinition);

    string[] Ticks { get; }
    TickDefinition this[string currencyPair] { get; }    
    State CurrentState { get; }
    (string ccyPair, int frequency)[] GetScheduledTicks();

    event OnTickUpdate OnTickUpdate;
}
```


## Quick start 

Create a new instance of Forex
```csharp
ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddLog4Net());

// Using file with ticker symbols and their corresponding initial values
// Uses CsvObjectify to parse file for ticker symbols and their corresponding initial values
ISpot spot = new Spot(loggerFactory, _testFilePath);

// Using TickerDefinition for EURUSD requested to be published every 1 second
ISpot spot = new Spot(loggerFactory, new List<TickerDefinition> { new TickerDefinition("EURUSD", 120.1234, 120.1238, 0.01, 1000) });
```

Register a callback
```csharp
spot.OnTickUpdate += Spot_OnTickUpdate;

// new tic is returned as string with format "Ticker  : Bid, Ask, Last"
private async Task Spot_OnTickUpdate(string tickData)
{
    _logger.LogInformation(tickData + " " + DateTime.Now.ToString("HH:mm:ss.fff"));
}
```

Start publishing
```csharp
spot.Start();
```
