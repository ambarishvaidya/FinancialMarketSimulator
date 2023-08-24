using CsvObjectify.Column.Helper;
using CsvObjectify.Column;
using CsvObjectify;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Forex
{
    internal class FileDataExtractor
    {
        private ILogger _logger;

        internal FileDataExtractor(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<FileDataExtractor>();
        }

        internal void ParseFile(string filePath, Action<TickDefinition> addTickDefinition)
        {
            try
            {
                CsvParserLog.SetLogger((s) => _logger.LogError(s), (s) => _logger.LogInformation(s), (s) => _logger.LogDebug(s));
                ICsvParser<Tick> parser = CsvParser<Tick>.Build(CsvProfile.Build(
                    new ColumnMetadata[]
                    {
                        ColumnDefinitionHelper.CreateStringColumn("CurrencyPair", "CurrencyPair"),
                        ColumnDefinitionHelper.CreateDoubleColumn("Bid", "Bid"),
                        ColumnDefinitionHelper.CreateDoubleColumn("Ask", "Ask"),
                        ColumnDefinitionHelper.CreateDoubleColumn("Spread", "Spread"),
                        ColumnDefinitionHelper.CreateIntColumn("PublishFrequencyInMs", "PublishFrequencyInMs"),
                    }, new FileDetails()
                    {
                        FilePath = filePath,
                        IsFirstRowHeader = true,
                    }));
                foreach (Tick tick in parser.Parse())
                {
                    addTickDefinition(new TickDefinition(tick.CurrencyPair, tick.Bid, tick.Ask, tick.Spread, tick.PublishFrequencyInMs));
                }
            }
            catch (Exception oex)
            {
                _logger.LogError(oex.Message);
            }
        }
    }

    internal class Tick
    {
        public string CurrencyPair { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }
        public double Spread { get; set; }
        public int PublishFrequencyInMs { get; set; }
    }
}
