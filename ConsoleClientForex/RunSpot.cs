using Forex;
using Microsoft.Extensions.Logging;
using System.Timers;

namespace ConsoleClientForex
{
    internal class RunSpot
    {
        private readonly ILogger _logger;
        private string _testFilePath;
        private const string TestForexSpotFileName = "Forex.Spot.TestFile.txt";
        private readonly ISpot spot;
        private readonly System.Timers.Timer _pauseTimer, _resumeTimer, _stopTimer;
        private readonly int PAUSE_TIME = 5000;
        private readonly int RESUME_TIME = 10000;
        private readonly int STOP_TIME = 5000;

        private bool _createFromFile = true;

        public RunSpot()
        {            
            ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddLog4Net());
            _logger = loggerFactory.CreateLogger<RunSpot>();

            
            spot = _createFromFile ? CreateSpotFromFile(loggerFactory) : CreateSpotFromTickerDefinition(loggerFactory);
            
            //spot.OnTickUpdate += Spot_OnTickUpdate;

            _pauseTimer = new System.Timers.Timer(PAUSE_TIME);
            _pauseTimer.Elapsed += PauseTimer_Elapsed;
            _pauseTimer.Start();

            _resumeTimer = new System.Timers.Timer(RESUME_TIME);
            _resumeTimer.Elapsed += ResumeTimer_Elapsed;

            _stopTimer = new System.Timers.Timer(STOP_TIME);
            _stopTimer.Elapsed += StopTimer_Elapsed;

            spot.Start();
            if(File.Exists(_testFilePath))
                File.Delete(_testFilePath);
        }

        private void StopTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _logger.LogInformation("Stopping the appplication!");
            spot.Stop();
            _stopTimer.Stop();
            _stopTimer.Dispose();
            _logger.LogInformation("Press Any key to exit the app!");
        }

        private void ResumeTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _resumeTimer.Stop();
            _resumeTimer.Dispose();
            spot.Resume();
            _logger.LogInformation($"Stopping the application in {STOP_TIME/1000} seconds");
            _stopTimer.Start();
        }

        private void PauseTimer_Elapsed(object sender, ElapsedEventArgs e)
        {           
            spot.Pause();
            _pauseTimer.Stop();
            _pauseTimer.Dispose();

            _logger.LogInformation(Environment.NewLine + "PAUSED with state " + spot.CurrentState);
            _logger.LogInformation(Environment.NewLine + "Currently Registered Paris and Frequency!");
            _logger.LogInformation(string.Join("", spot.GetScheduledTicks().Select(item => Environment.NewLine + item.ccyPair + " @ " + item.frequency)));
            _resumeTimer.Start();
            _logger.LogInformation($"Will resume in {RESUME_TIME/1000} seconds");
        }

        private async Task Spot_OnTickUpdate(string tickData)
        {
            await Task.Run(() => ProcessTickData(tickData));            
        }

        private void ProcessTickData(string tickData)
        {
            if (tickData.Contains("EURGBP3"))
            {
                _logger.LogInformation("Bocking thread for 5 seconds");
                Thread.Sleep(5000);
            }
            _logger.LogInformation(tickData + " " + DateTime.Now.ToString("HH:mm:ss.fff"));
        }

        private ISpot CreateSpotFromFile(ILoggerFactory loggerFactory)
        {
            _testFilePath = Path.Combine(Path.GetTempPath(), TestForexSpotFileName);
            using (StreamWriter sw = new StreamWriter(_testFilePath))
            {
                sw.WriteLine("CurrencyPair,Bid,Ask,Spread,PublishFrequencyInMs");
                sw.WriteLine("EURUSD, 1.1234, 1.1235, 1.1236, 1500");
                sw.WriteLine("EURGBP1, 0.8901, 0.8902, 0.8903, 123");
                sw.WriteLine("EURGBP2, 0.8901, 0.8902, 0.8903, 123");
                sw.WriteLine("EURGBP3, 0.8901, 0.8902, 0.8903, 123");
                sw.WriteLine("EURGBP4, 0.8901, 0.8902, 0.8903, 123");
                sw.WriteLine("EURGBP5, 0.8901, 0.8902, 0.8903, 123");
                sw.WriteLine("EURGBP6, 0.8901, 0.8902, 0.8903, 123");
                sw.WriteLine("EURGBP7, 0.8901, 0.8902, 0.8903, 123");
                sw.WriteLine("EURGBP8, 0.8901, 0.8902, 0.8903, 123");
                sw.WriteLine("EURGBP9, 0.8901, 0.8902, 0.8903, 123");
                sw.WriteLine("EURGBP06, 0.8901, 0.8902, 0.8903, 1230");
                sw.WriteLine("EURGBP03, 0.8901, 0.8902, 0.8903, 1230");
                sw.WriteLine("EURGBP03, 0.8901, 0.8902, 0.8903, 1230");
                sw.WriteLine("EURGBP03, 0.8901, 0.8902, 0.8903, 1230");
                sw.WriteLine("EURGBP04, 0.8901, 0.8902, 0.8903, 1230");
                sw.WriteLine("EURGBP05, 0.8901, 0.8902, 0.8903, 1230");
                sw.WriteLine("EURGBP06, 0.8901, 0.8902, 0.8903, 1230");


            }
            return new Forex.Spot(loggerFactory, _testFilePath);
        }

        private ISpot CreateSpotFromTickerDefinition(ILoggerFactory loggerFactory)
        {
            var tickDefinition = new TickDefinition[]
            {
                new TickDefinition("EURUSD", 1.1234, 1.1235, 1.1236, 500),
                new TickDefinition("EURGBP", 0.8901, 0.8902, 0.8903, 230),
                new TickDefinition("EURJPY", 120.1234, 120.1235, 120.1236, 0),
            };
            return new Forex.Spot(loggerFactory, tickDefinition);
        }
    }
}
