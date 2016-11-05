using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGProxyDemo.Helpers
{
    public static class DateTimeHelpers
    {
        public static string GetElapsedSmallTime(DateTime targetDateTime)
        {
            var elapsed = DateTime.Now.Subtract(targetDateTime);

            if (elapsed.TotalDays > 14)
                return $"{(int)elapsed.TotalDays/7}w";
            if(elapsed.TotalDays>=1)
                return $"{(int)elapsed.TotalDays}d";
            if (elapsed.TotalHours >= 1)
                return $"{(int)elapsed.TotalHours}h";
            if (elapsed.TotalMinutes >= 1)
                return $"{(int)elapsed.TotalMinutes}min{((elapsed.TotalMilliseconds >= 2) ? "s" : string.Empty)}";

            return $"{(int)elapsed.TotalSeconds}s";
        }
    }
}
