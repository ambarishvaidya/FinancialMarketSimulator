namespace Forex;

public record TickDefinition(string CurrencyPair, double Bid, double Ask, double Spread, int PublishFrequencyInMs);
