using MuscleForge.Mobile.Controls;
using MuscleForge.Mobile.Core.Templates;
using MuscleForge.Mobile.Core.Workspaces;

namespace MuscleForge.Mobile.Views;

public sealed class ExistingLibrariesPage(ILegacyTemplateMigration migration, TemplateScope guestScope) : ContentPage, IDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly VerticalStackLayout body = new() { Padding = 24, Spacing = 20 };
    private bool busy;
    private bool disposed;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Title = "Existing libraries";
        Shell.SetNavBarIsVisible(this, true);
        Content = new ScrollView { Content = body };
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (busy || disposed) return;
        busy = true;
        body.Children.Clear();
        body.Children.Add(ForgeUi.Text("Loading libraries saved by earlier app versions…"));
        try
        {
            var libraries = await migration.ListLibrariesAsync(lifetime.Token);
            if (disposed) return;
            body.Children.Clear();
            body.Children.Add(ForgeUi.Header("ON THIS DEVICE", "Existing libraries", "Copy one saved library into your local templates. Sources are retained and existing local templates are never overwritten."));
            if (libraries.Count == 0) body.Children.Add(ForgeUi.Text("No earlier account libraries are saved on this device."));
            for (var i = 0; i < libraries.Count; i++)
            {
                var library = libraries[i];
                var account = library.Scope.AccountId.ToString("D")[..8];
                var label = $"Library {i + 1} · Account {account}\n{library.Scope.Server.AbsoluteUri}";
                var button = ForgeUi.Button("Copy templates", () => ImportAsync(library, label), true);
                button.AutomationId = "ImportLegacyLibrary";
                body.Children.Add(ForgeUi.Card(new VerticalStackLayout
                {
                    Spacing = 12,
                    Children = { ForgeUi.Text(label, "SectionTitle"), ForgeUi.Text($"{library.TemplateCount} saved templates", "SmallText"), button }
                }));
            }
            AddBack();
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (disposed) return;
            body.Children.Clear();
            body.Children.Add(ForgeUi.Text("Could not load existing libraries. Your saved data is unchanged."));
            body.Children.Add(ForgeUi.Button("Retry", LoadAsync, true));
            AddBack();
        }
        finally { busy = false; }
    }

    private async Task ImportAsync(LegacyTemplateLibrary library, string label)
    {
        if (busy || disposed) return;
        busy = true;
        try
        {
            if (!await DisplayAlertAsync("Copy this library?", $"{label}\n\nOriginal templates will be retained. Conflicting local IDs will be skipped without overwriting anything.", "Copy", "Cancel")) return;
            if (disposed) return;
            var result = await migration.ImportLibraryAsync(library.Scope, guestScope, lifetime.Token);
            if (disposed) return;
            await DisplayAlertAsync("Saved on device", $"{result.Imported} copied; {result.AlreadyImported} already copied; {result.Conflicts} ID conflicts skipped. Original libraries are unchanged.", "OK");
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (!disposed) await DisplayAlertAsync("Could not copy templates", "The copy did not complete. Your saved libraries are unchanged; try again.", "OK");
        }
        finally { busy = false; }
    }

    private void AddBack()
    {
        var back = ForgeUi.Button("Back", async () => await Navigation.PopAsync(), true);
        back.AutomationId = "ExistingLibrariesBack";
        body.Children.Add(back);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
