using System.Globalization;
using System.Text.Json;
using MuscleForge.Mobile.Core.Measurements;
using Xunit;

namespace MuscleForge.Mobile.UnitTests;

public class SharedUnitsTimeZonesFixtureTests
{
    [Fact]
    public void WeightConversionsMatchSharedExamples()
    {
        using var fixture = ReadFixture();
        foreach (var item in fixture.RootElement.GetProperty("weightConversions").EnumerateArray())
        {
            var unit = ReadUnit(item);
            Assert.True(Weight.TryToKilograms(item.GetProperty("value").GetDecimal(), unit, out var kilograms));
            Assert.Equal(item.GetProperty("kilograms").GetDecimal(), kilograms);
        }
        foreach (var item in fixture.RootElement.GetProperty("invalidWeights").EnumerateArray())
        {
            var unit = ReadUnit(item);
            Assert.False(Weight.TryToKilograms(item.GetProperty("value").GetDecimal(), unit, out _));
        }
    }

    [Fact]
    public void CompletedRepetitionsMatchSharedExamples()
    {
        using var fixture = ReadFixture();
        foreach (var item in fixture.RootElement.GetProperty("completedRepetitions").EnumerateArray())
            Assert.Equal(item.GetProperty("valid").GetBoolean(), Repetitions.IsValidCompleted(item.GetProperty("repetitions").GetInt32()));
    }

    [Fact]
    public void InstantsMapToSharedSessionLocalDays()
    {
        using var fixture = ReadFixture();
        foreach (var item in fixture.RootElement.GetProperty("localDays").EnumerateArray())
        {
            var instant = DateTimeOffset.Parse(item.GetProperty("instant").GetString()!, CultureInfo.InvariantCulture);
            var zone = TimeZoneInfo.FindSystemTimeZoneById(item.GetProperty("timeZoneId").GetString()!);
            var localDay = TimeZoneInfo.ConvertTime(instant, zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            Assert.Equal(item.GetProperty("localDay").GetString(), localDay);
        }
    }

    private static JsonDocument ReadFixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "units-timezones.json")));
    private static WeightUnit ReadUnit(JsonElement item) => item.GetProperty("unit").GetString() switch
    {
        "kg" => WeightUnit.Kg,
        "lb" => WeightUnit.Lb,
        _ => throw new InvalidOperationException("Unknown fixture unit.")
    };
}
