using System;

namespace Knox
{
    public static class StringExtensions
    {
        public static bool CaseInsensitiveContains(this string target, string lookFor)
        {
            return target.IndexOf(lookFor, StringComparison.OrdinalIgnoreCase) > -1;
        }
    }
}
