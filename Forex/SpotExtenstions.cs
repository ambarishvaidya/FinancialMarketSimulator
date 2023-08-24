using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Forex;

internal static class SpotExtensions
{
    public static int AdjustedPublishTime(this int initialFreq, int roundToNearestFreq)
    {
        if (initialFreq <= 0) return roundToNearestFreq;
        int remainder = initialFreq % roundToNearestFreq;
        return initialFreq + (remainder == 0 ? 0 : roundToNearestFreq - remainder);
    }

    public static bool IsTickDefinitionValid(this TickDefinition tickDefinition)
    {
        return tickDefinition.Bid > 0 && tickDefinition.Ask > 0 && tickDefinition.Spread > 0;
    }
}
