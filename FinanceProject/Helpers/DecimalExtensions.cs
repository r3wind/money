using System.Globalization;

namespace FinanceProject.Helpers;

public static class DecimalExtensions
{
    private static readonly CultureInfo GBP = new("en-GB");

    public static string ToGBP(this decimal value) => value.ToString("C", GBP);
    public static string ToGBPShort(this decimal value) => value.ToString("C0", GBP);
}
