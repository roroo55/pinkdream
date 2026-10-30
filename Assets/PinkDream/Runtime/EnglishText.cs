using System;

namespace PinkDream
{
    // Date Night's supplied glyph set supports letters. Spell progress numbers out.
    internal static class EnglishText
    {
        private static readonly string[] Units =
        {
            "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
            "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"
        };
        private static readonly string[] Tens = { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };

        public static string Number(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (value < 20) return Units[value];
            if (value < 100) return Tens[value / 10] + Remainder(value % 10);
            if (value < 1000) return Number(value / 100) + " hundred" + Remainder(value % 100);
            if (value < 1000000) return Number(value / 1000) + " thousand" + Remainder(value % 1000);
            if (value < 1000000000) return Number(value / 1000000) + " million" + Remainder(value % 1000000);
            return Number(value / 1000000000) + " billion" + Remainder(value % 1000000000);
        }

        private static string Remainder(int value) => value == 0 ? string.Empty : " " + Number(value);
    }
}
