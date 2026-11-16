using System.Globalization;
using MuscleForge.Mobile.Core.Measurements;

namespace MuscleForge.Mobile.Controls;

/// <summary>Reusable decimal weight entry; consumers decide when and how to save the value.</summary>
public sealed class WeightEntry : ContentView
{
    private readonly Entry entry;

    public WeightEntry()
    {
        entry = new NumericWeightInput { Keyboard = Keyboard.Numeric, AutomationId = "WeightEntry", Placeholder = "0" };
        SemanticProperties.SetDescription(entry, "Weight");
        entry.TextChanged += (_, _) => TextChanged?.Invoke(this, EventArgs.Empty);
        Content = entry;
    }

    public event EventHandler? TextChanged;
    public string? Text { get => entry.Text; set => entry.Text = value; }
    public WeightUnit Unit { get; set; }
    public bool TryGetValue(out decimal value) => Weight.TryParse(Text, CultureInfo.CurrentCulture, Unit, out value);
}

// Identifies this entry for the Android keyboard mapping without affecting other numeric inputs.
internal sealed class NumericWeightInput : Entry;
