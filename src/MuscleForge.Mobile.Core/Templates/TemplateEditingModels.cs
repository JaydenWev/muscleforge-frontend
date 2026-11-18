using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MuscleForge.Mobile.Core.Measurements;

namespace MuscleForge.Mobile.Core.Templates;

public sealed class TemplateExerciseEditor : ObservableObject
{
    private readonly Action changed;
    private readonly CultureInfo culture;
    private WeightUnit unit;
    private string restSecondsText;
    internal TemplateExerciseEditor(LocalTemplateExercise exercise, WeightUnit unit, CultureInfo culture, Action changed)
    {
        Id = exercise.Id;
        Metadata = exercise.Metadata;
        this.unit = unit;
        this.culture = culture;
        this.changed = changed;
        restSecondsText = exercise.RestSeconds?.ToString(CultureInfo.InvariantCulture) ?? "";
        foreach (var set in exercise.Sets) Sets.Add(new(set, unit, culture, changed));
    }
    public Guid Id { get; }
    public LocalExerciseMetadata Metadata { get; }
    public string RestSecondsText { get => restSecondsText; set { if (SetProperty(ref restSecondsText, value)) changed(); } }
    public ObservableCollection<TemplateSetEditor> Sets { get; } = [];
    public bool AddSet()
    {
        if (Sets.Count >= 100) return false;
        Sets.Add(new(new LocalTemplateSet(Guid.NewGuid(), Sets.Count, TemplateSetKind.Working, null, null, null), unit, culture, changed));
        changed();
        return true;
    }
    public void RemoveSet(TemplateSetEditor set)
    {
        if (Sets.Remove(set)) changed();
    }
    public void MoveSet(TemplateSetEditor set, int direction)
    {
        var from = Sets.IndexOf(set);
        var to = from + direction;
        if (from >= 0 && Math.Abs(direction) == 1 && to >= 0 && to < Sets.Count)
        {
            Sets.Move(from, to);
            changed();
        }
    }
    internal LocalTemplateExercise Snapshot(int position) => new(Id, position, Metadata, ParseInteger(RestSecondsText, "Rest must be a whole number from 0 to 86400 seconds."), Sets.Select((s, i) => s.Snapshot(i)));
    internal static int? ParseInteger(string? text, string message)
    {
        if (string.IsNullOrEmpty(text)) return null;
        if (text.Any(c => c is < '0' or > '9') || !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number)) throw new TemplateValidationException(message);
        return number;
    }
    internal void ChangeUnit(WeightUnit value) => unit = value;
}

public sealed class TemplateSetEditor : ObservableObject
{
    private readonly Action changed;
    private readonly CultureInfo culture;
    private WeightUnit unit;
    private decimal? originalKg;
    private string originalDisplay;
    private string repMinText, repMaxText, weightText;
    private TemplateSetKind kind;
    internal TemplateSetEditor(LocalTemplateSet set, WeightUnit unit, CultureInfo culture, Action changed)
    {
        Id = set.Id;
        kind = set.Kind;
        this.unit = unit;
        this.culture = culture;
        this.changed = changed;
        originalKg = set.WeightKg;
        repMinText = set.RepMin?.ToString(CultureInfo.InvariantCulture) ?? "";
        repMaxText = set.RepMax?.ToString(CultureInfo.InvariantCulture) ?? "";
        originalDisplay = weightText = set.WeightKg.HasValue ? Weight.Format(set.WeightKg.Value, unit, culture) : "";
    }
    public Guid Id { get; }
    public TemplateSetKind Kind { get => kind; set { if (SetProperty(ref kind, value)) changed(); } }
    public string RepMinText { get => repMinText; set { if (SetProperty(ref repMinText, value)) changed(); } }
    public string RepMaxText { get => repMaxText; set { if (SetProperty(ref repMaxText, value)) changed(); } }
    public string WeightText { get => weightText; set { if (SetProperty(ref weightText, value)) changed(); } }
    private decimal? ParseWeight()
    {
        if (WeightText == originalDisplay) return originalKg;
        if (string.IsNullOrEmpty(WeightText)) return null;
        if (!Weight.TryParse(WeightText, culture, unit, out var value) || !Weight.TryToKilograms(value, unit, out var kg)) throw new TemplateValidationException("Enter a valid nonnegative weight with up to three decimal places.");
        return kg;
    }
    internal bool CanChangeUnit()
    {
        try
        {
            _ = ParseWeight();
            return true;
        }
        catch (TemplateValidationException)
        {
            return false;
        }
    }
    internal void ChangeUnit(WeightUnit value)
    {
        var kg = ParseWeight();
        unit = value;
        originalKg = kg;
        originalDisplay = kg.HasValue ? Weight.Format(kg.Value, unit, culture) : "";
        weightText = originalDisplay;
        OnPropertyChanged(nameof(WeightText));
    }
    internal LocalTemplateSet Snapshot(int position) => new(Id, position, Kind,
    TemplateExerciseEditor.ParseInteger(RepMinText, "Repetitions must be positive whole numbers."),
    TemplateExerciseEditor.ParseInteger(RepMaxText, "Repetitions must be positive whole numbers."), ParseWeight());
}
