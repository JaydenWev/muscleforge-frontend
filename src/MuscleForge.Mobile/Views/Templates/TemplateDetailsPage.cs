using MuscleForge.Mobile.Controls;
using MuscleForge.Mobile.Core.Measurements;
using MuscleForge.Mobile.Core.Templates;
using System.Globalization;

namespace MuscleForge.Mobile.Views.Templates;

public sealed class TemplateDetailsPage : ContentPage, IDisposable
{
    private readonly ILocalTemplateStore store;
    private readonly TemplateScope scope;
    private readonly Guid id;
    private readonly Func<Guid, Task> edit;
    private readonly WeightUnit unit;
    private readonly CancellationTokenSource lifetime = new();
    private readonly VerticalStackLayout body = new() { Spacing = 14 };
    private LocalTemplate? current;
    private bool busy, disposed;
    public TemplateDetailsPage(ILocalTemplateStore store, TemplateScope scope, Guid id, WeightUnit unit, Func<Guid, Task> edit)
    {
        this.store = store; this.scope = scope; this.id = id; this.unit = unit; this.edit = edit;
        Title = "Template details"; Shell.SetNavBarIsVisible(this, true);
        Content = ForgeUi.PageContent(body);
    }
    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private async Task LoadAsync()
    {
        if (busy || disposed) return; busy = true; body.Children.Clear(); body.Children.Add(ForgeUi.Text("Loading template…"));
        try
        {
            current = await store.GetAsync(scope, id, lifetime.Token);
            if (disposed) return;
            body.Children.Clear();
            if (current is null) { body.Children.Add(ForgeUi.Text("This template is no longer available.")); AddBack(); return; }
            body.Children.Add(ForgeUi.Header("YOUR LIBRARY", current.Name, current.IsArchived ? "Archived · Saved on device" : "Saved on device"));
            if (current.Exercises.Count == 0) body.Children.Add(ForgeUi.Text("No exercises in this template."));
            foreach (var exercise in current.Exercises)
            {
                var content = new VerticalStackLayout { Spacing = 8 };
                content.Children.Add(ForgeUi.Text(exercise.Metadata.Name, "SectionTitle"));
                content.Children.Add(ForgeUi.Text(exercise.Metadata.IsArchived ? "Archived exercise" : exercise.Metadata.Equipment, "SmallText"));
                if (exercise.RestSeconds.HasValue) content.Children.Add(ForgeUi.Text($"Rest: {exercise.RestSeconds} seconds", "SmallText"));
                if (exercise.Sets.Count == 0) content.Children.Add(ForgeUi.Text("No sets", "SmallText"));
                foreach (var set in exercise.Sets)
                {
                    var reps = set.RepMin.HasValue ? $"{set.RepMin}–{set.RepMax} reps" : "No rep target";
                    var weight = set.WeightKg.HasValue ? $" · {Weight.Format(set.WeightKg.Value, unit, CultureInfo.CurrentCulture)} {(unit == WeightUnit.Kg ? "kg" : "lb")}" : "";
                    content.Children.Add(ForgeUi.Text($"{(set.Kind == TemplateSetKind.Warmup ? "Warm-up" : "Working")} · {reps}{weight}", "SmallText"));
                }
                body.Children.Add(ForgeUi.Card(content));
            }
            if (!current.IsArchived)
            {
                var editButton = ForgeUi.Button("Edit template", () => edit(id)); editButton.AutomationId = "TemplateEdit"; body.Children.Add(editButton);
                var archive = ForgeUi.Button("Archive template", ArchiveAsync, true); archive.AutomationId = "TemplateArchive"; body.Children.Add(archive);
            }
            AddBack();
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            body.Children.Clear(); body.Children.Add(ForgeUi.Text("Could not load this template."));
            var retry = ForgeUi.Button("Retry", LoadAsync, true); retry.AutomationId = "TemplateDetailsRetry"; body.Children.Add(retry); AddBack();
        }
        finally { busy = false; }
    }
    private async Task ArchiveAsync()
    {
        if (busy || disposed || current is null || current.IsArchived) return;
        if (!await DisplayAlertAsync("Archive template?", "This hides it from the active list. Its contents remain on this device.", "Archive", "Cancel")) return;
        if (disposed) return;
        busy = true;
        try { current = await store.ArchiveAsync(scope, id, current.Revision, lifetime.Token); }
        catch (TemplateConflictException) { await DisplayAlertAsync("Template changed", "Reload the template before archiving.", "OK"); }
        catch (OperationCanceledException) { }
        catch (Exception) { await DisplayAlertAsync("Could not archive", "The template was not archived. Please try again.", "OK"); }
        finally { busy = false; }
        if (!disposed && !lifetime.IsCancellationRequested) await LoadAsync();
    }
    private void AddBack() { var back = ForgeUi.Button("Back", async () => await Navigation.PopAsync(), true); back.AutomationId = "TemplateDetailsBack"; body.Children.Add(back); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
