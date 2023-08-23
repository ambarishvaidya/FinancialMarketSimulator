namespace Forex;

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

public delegate Task OnTickUpdate(string tickData);