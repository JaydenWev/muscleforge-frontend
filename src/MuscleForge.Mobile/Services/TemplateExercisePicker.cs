using MuscleForge.Mobile.Controls;
using MuscleForge.Mobile.Core.Authentication;
using MuscleForge.Mobile.Core.Catalog;
using MuscleForge.Mobile.Core.Templates;
using MuscleForge.Mobile.Core.Workspaces;

namespace MuscleForge.Mobile.Services;

public sealed class TemplateExercisePicker(
    IExercisePicker onlinePicker,
    IExerciseCatalogGateway catalog,
    ILocalTemplateStore templates,
    ILocalWorkspace workspace,
    IAuthSession session) : ITemplateExercisePicker
{
    public async Task<LocalExerciseMetadata?> PickAsync(TemplateScope scope, CancellationToken cancellationToken = default)
    {
        if (workspace.CurrentScope != scope) return null;
        var root = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (root is null) return null;
        bool CanUseApi() => workspace.CurrentScope == scope && session.State.Status == AuthStatus.SignedIn
            && session.State.Account?.Id == scope.AccountId && session.State.Server == scope.Server;
        var connected = CanUseApi();
        if (connected)
        {
            var source = await root.DisplayActionSheetAsync("Choose exercise source", "Cancel", null,
                "Online catalog", "Saved exercise metadata");
            if (source == "Online catalog")
            {
                if (!CanUseApi()) return null;
                var selection = await onlinePicker.PickAsync(cancellationToken);
                if (selection is null || selection.BaseUri != scope.Server || selection.AccountId != scope.AccountId || !CanUseApi())
                    return null;
                var exercise = await catalog.GetAsync(selection.ExerciseId, cancellationToken);
                if (!CanUseApi() || exercise.IsArchived) return null;
                return new LocalExerciseMetadata(scope, exercise.Id, exercise.Name, exercise.Equipment,
                    ExerciseTrackingType.ExternalWeightAndReps, exercise.LoadConvention, false);
            }
            if (source != "Saved exercise metadata") return null;
        }

        // Only metadata owned by this workspace is available offline. Absence is explicit,
        // rather than substituting another user's catalog or manufacturing exercise IDs.
        var available = new Dictionary<Guid, LocalExerciseMetadata>();
        for (var offset = 0; ; offset += 100)
        {
            var page = await templates.ListAsync(scope, offset, 100, true, cancellationToken);
            foreach (var summary in page)
            {
                var template = await templates.GetAsync(scope, summary.Id, cancellationToken);
                if (template is null) continue;
                foreach (var exercise in template.Exercises)
                {
                    var metadata = exercise.Metadata;
                    // Catalog archival is permanent. A known archived copy takes precedence.
                    if (!available.TryGetValue(metadata.ExerciseId, out var prior) || !prior.IsArchived)
                        available[metadata.ExerciseId] = metadata;
                }
            }
            if (page.Count < 100) break;
        }
        if (workspace.CurrentScope != scope || cancellationToken.IsCancellationRequested) return null;
        var completion = new TaskCompletionSource<LocalExerciseMetadata?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var navigation = root.Navigation;
        CachedExercisePage? picker = null;
        var finished = false;
        async Task Finish(LocalExerciseMetadata? result)
        {
            if (finished) return;
            finished = true;
            if (navigation.NavigationStack.LastOrDefault() == picker) await navigation.PopAsync();
            completion.TrySetResult(workspace.CurrentScope == scope ? result : null);
        }
        picker = new CachedExercisePage(available.Values.Where(item => !item.IsArchived).OrderBy(item => item.Name).ToArray(), Finish);
        using var registration = cancellationToken.Register(() => MainThread.BeginInvokeOnMainThread(() => _ = Finish(null)));
        await navigation.PushAsync(picker);
        if (finished && navigation.NavigationStack.LastOrDefault() == picker) await navigation.PopAsync();
        else if (cancellationToken.IsCancellationRequested) await Finish(null);
        return await completion.Task;
    }

    private sealed class CachedExercisePage : ContentPage
    {
        private readonly Func<LocalExerciseMetadata?, Task> finish;

        public CachedExercisePage(IReadOnlyList<LocalExerciseMetadata> available, Func<LocalExerciseMetadata?, Task> finish)
        {
            this.finish = finish;
            Title = "Saved exercises";
            Shell.SetNavBarIsVisible(this, true);
            Shell.SetTabBarIsVisible(this, false);
            var items = new CollectionView
            {
                ItemsSource = available,
                SelectionMode = SelectionMode.Single,
                AutomationId = "TemplateCachedExercises",
                ItemTemplate = new DataTemplate(() =>
                {
                    var name = ForgeUi.Text("", "SectionTitle");
                    name.SetBinding(Label.TextProperty, nameof(LocalExerciseMetadata.Name));
                    var equipment = ForgeUi.Text("", "SmallText");
                    equipment.SetBinding(Label.TextProperty, nameof(LocalExerciseMetadata.Equipment));
                    return new VerticalStackLayout { Padding = new Thickness(24, 16), MinimumHeightRequest = 64, Children = { name, equipment } };
                })
            };
            items.SelectionChanged += async (_, args) =>
            {
                if (args.CurrentSelection.FirstOrDefault() is LocalExerciseMetadata metadata) await finish(metadata);
            };
            items.Header = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 12,
                Children =
                {
                    ForgeUi.Header("ON THIS DEVICE", "Saved exercises", "Metadata from your saved templates may be out of date. Online workout start will validate exercises again."),
                    ForgeUi.Text(available.Count == 0 ? "No available exercise metadata is saved for this account and server. Sign in while connected to choose from your catalog. Existing template content is retained." : "Choose an exercise below."),
                    ForgeUi.Button("Back", () => finish(null), true)
                }
            };
            Content = items;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _ = finish(null);
        }
    }
}
