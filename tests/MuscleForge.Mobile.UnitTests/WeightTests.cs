using Xunit;
using System.Globalization;
using MuscleForge.Mobile.Core.Measurements;

namespace MuscleForge.Mobile.UnitTests;

public class WeightTests
{
    [Theory]
    [InlineData("nl-NL", "12,345", 12.345)]
    [InlineData("en-US", "12.345", 12.345)]
    [InlineData("nl-NL", "0", 0)]
    public void ParsesStrictLocaleInput(string locale, string text, double expected)
    {
        Assert.True(Weight.TryParse(text, CultureInfo.GetCultureInfo(locale), WeightUnit.Kg, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Theory]
    [InlineData("nl-NL", "1.5")]
    [InlineData("en-US", "1,5")]
    [InlineData("en-US", "1.2345")]
    [InlineData("en-US", "1,000")]
    [InlineData("en-US", "-1")]
    [InlineData("en-US", "+1")]
    [InlineData("en-US", "1e2")]
    [InlineData("en-US", " 1")]
    [InlineData("en-US", ".5")]
    [InlineData("en-US", "1.")]
    public void RejectsAmbiguousOrInvalidInput(string locale, string text) =>
        Assert.False(Weight.TryParse(text, CultureInfo.GetCultureInfo(locale), WeightUnit.Kg, out _));

    [Theory]
    [InlineData("1", WeightUnit.Lb, "0.454")]
    [InlineData("2.5", WeightUnit.Lb, "1.134")]
    [InlineData("9999999.999", WeightUnit.Kg, "9999999.999")]
    public void ConvertsOnceWithStoragePrecision(string input, WeightUnit unit, string expected)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        Assert.True(Weight.TryParse(input, culture, unit, out var value));
        Assert.True(Weight.TryToKilograms(value, unit, out var kilograms));
        Assert.Equal(decimal.Parse(expected, culture), kilograms);
    }

    [Fact]
    public void RejectsOutOfRangeAndDisplaysWithoutChangingCanonicalValue()
    {
        var culture = CultureInfo.GetCultureInfo("nl-NL");
        Assert.False(Weight.TryParse("10000000", culture, WeightUnit.Kg, out _));
        Assert.False(Weight.TryParse("22046226,217", culture, WeightUnit.Lb, out _));
        Assert.False(Weight.TryToKilograms(1.0000m, WeightUnit.Kg, out _));
        const decimal kilograms = 1.234m;
        Assert.Equal("2,721", Weight.Format(kilograms, WeightUnit.Lb, culture));
        Assert.Equal("1,234", Weight.Format(kilograms, WeightUnit.Kg, culture));
    }
}
