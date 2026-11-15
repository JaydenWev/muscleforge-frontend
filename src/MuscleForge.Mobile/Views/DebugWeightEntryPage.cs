#if DEBUG
using System.Globalization;
using MuscleForge.Mobile.Controls;
using MuscleForge.Mobile.Core.Measurements;

namespace MuscleForge.Mobile.Views;

// Explicit debug intent only: exercises the same control that future workout screens consume.
public sealed class DebugWeightEntryPage : ContentPage
{
    public DebugWeightEntryPage()
    {
        Title = "Weight entry verification";
        var weight = new WeightEntry();
        var unit = new Picker { Title = "Unit", ItemsSource = new[] { "kg", "lb" }, SelectedIndex = 0, AutomationId = "DebugWeightUnit" };
        var result = new Label { AutomationId = "DebugWeightResult" };
        void Update()
        {
            weight.Unit = unit.SelectedIndex == 1 ? WeightUnit.Lb : WeightUnit.Kg;
            result.Text = weight.TryGetValue(out var value) ? $"Valid: {value.ToString(CultureInfo.InvariantCulture)} {unit.SelectedItem}" : "Invalid";
        }
        weight.TextChanged += (_, _) => Update();
        unit.SelectedIndexChanged += (_, _) => Update();
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 24, Spacing = 16, Children = { new Label { Text = "Weight entry verification" }, unit, weight, result } } };
        Update();
    }
}
#endif
