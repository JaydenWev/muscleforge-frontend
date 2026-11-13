using System.Globalization;

namespace MuscleForge.Mobile.Core.Measurements;

public enum WeightUnit { Kg, Lb }

/// <summary>Decimal rules shared by weight entry and future workout fields.</summary>
public static class Weight
{
    public const decimal KilogramsPerPound = 0.45359237m;
    public const decimal MaximumKilograms = 9_999_999.999m;

    public static bool TryParse(string? text, CultureInfo culture, WeightUnit unit, out decimal value)
    {
        value = 0;
        if (string.IsNullOrEmpty(text) || !Enum.IsDefined(unit)) return false;
        var separator = culture.NumberFormat.NumberDecimalSeparator;
        if (separator is not ("." or ",")) return false;
        var separatorIndex = text.IndexOf(separator, StringComparison.Ordinal);
        if (separatorIndex == 0 || separatorIndex == text.Length - 1 ||
            (separatorIndex >= 0 && (text.Length - separatorIndex - 1 > 3 ||
                                    text.IndexOf(separator, separatorIndex + 1, StringComparison.Ordinal) >= 0))) return false;
        foreach (var character in text)
            if (character is not (>= '0' and <= '9') && character.ToString() != separator) return false;
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, culture, out value)) return false;
        return TryToKilograms(value, unit, out _);
    }

    public static bool TryToKilograms(decimal value, WeightUnit unit, out decimal kilograms)
    {
        kilograms = 0;
        if (value < 0 || !Enum.IsDefined(unit) || ((decimal.GetBits(value)[3] >> 16) & 0xff) > 3) return false;
        // Bound before multiplication to avoid overflow from externally supplied decimals.
        if (value > MaximumKilograms / (unit == WeightUnit.Lb ? KilogramsPerPound : 1m) + 1m) return false;
        var converted = unit == WeightUnit.Lb ? value * KilogramsPerPound : value;
        if (converted > MaximumKilograms) return false;
        kilograms = decimal.Round(converted, 3, MidpointRounding.AwayFromZero);
        if (kilograms <= MaximumKilograms) return true;
        kilograms = 0;
        return false;
    }

    public static string Format(decimal kilograms, WeightUnit unit, CultureInfo culture)
    {
        if (kilograms < 0 || kilograms > MaximumKilograms) throw new ArgumentOutOfRangeException(nameof(kilograms));
        if (!Enum.IsDefined(unit)) throw new ArgumentOutOfRangeException(nameof(unit));
        var display = unit == WeightUnit.Lb ? kilograms / KilogramsPerPound : kilograms;
        return decimal.Round(display, 3, MidpointRounding.AwayFromZero).ToString("0.###", culture);
    }
}
