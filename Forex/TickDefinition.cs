namespace Forex;

public record TickDefinition(string CurrencyPair, double Bid, double Ask, double Last, int PublishFrequencyInMs);
