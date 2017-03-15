using System;

namespace tailor
{
    public static class StringEx
    {
        public static string TrimWithEllipsis(this string str, int trimLength)
        {
            if (trimLength <= 3) return "...";

            return str.IsLongerThan(trimLength) ? str.Substring(0, trimLength - 3) + "..." : str.PadRight(trimLength);
        }

    }
}
