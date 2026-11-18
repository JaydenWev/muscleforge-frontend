using System.Globalization;
using MuscleForge.Mobile.Core.Authentication;
using MuscleForge.Mobile.Core.Measurements;
using MuscleForge.Mobile.Core.Templates;
using MuscleForge.Mobile.Core.Workspaces;
using MuscleForge.Mobile.Views.Templates;

namespace MuscleForge.Mobile.Services;

public sealed class TemplatePages(ILocalTemplateStore store, ITemplateExercisePicker picker)
{
    public TemplateListPage CreateList(TemplateScope scope)
    {
        TemplateListPage? list = null;
        var unit = WeightUnit.Kg;
        Task Edit(Guid? id)
        {
            var editor = new TemplateEditorViewModel(store, scope, unit, CultureInfo.CurrentCulture, MainThread.BeginInvokeOnMainThread);
            return list!.Navigation.PushAsync(new TemplateEditorPage(editor, scope, picker, id, unit));
        }
        Task Open(Guid? id) => id.HasValue
            ? list!.Navigation.PushAsync(new TemplateDetailsPage(store, scope, id.Value, unit, value => Edit(value)))
            : Edit(null);
        list = new TemplateListPage(new TemplateListViewModel(store, scope, MainThread.BeginInvokeOnMainThread), Open);
        return list;
    }
}

public sealed class WorkspacePageFactory(TemplatePages templates, ILocalWorkspace workspace, IAuthSession session) : IWorkspacePageFactory
{
    public Page CreatePage(Action openAppOrSignIn)
    {
        var scope = workspace.CurrentScope ?? throw new InvalidOperationException("No local workspace is available.");
        var list = templates.CreateList(scope);
        var open = new ToolbarItem { AutomationId = "WorkspaceSignIn" };
        void Update() => open.Text = session.State.Status == AuthStatus.SignedIn ? "Open app" : "Sign in";
        EventHandler<SessionState> changed = (_, _) => MainThread.BeginInvokeOnMainThread(Update);
        session.StateChanged += changed;
        Update();
        open.Clicked += (_, _) => openAppOrSignIn();
        list.ToolbarItems.Add(open);
        var signOut = new ToolbarItem { Text = "Sign out", AutomationId = "WorkspaceSignOut" };
        signOut.Clicked += async (_, _) =>
        {
            await session.SignOutAsync();
            if (session.State.Status == AuthStatus.StorageError)
                await list.DisplayAlertAsync("Sign-out incomplete", "Local sign-out could not be saved. Retry sign-out before closing the app.", "OK");
        };
        list.ToolbarItems.Add(signOut);
        return new WorkspaceNavigationPage(list, () => session.StateChanged -= changed);
    }

    private sealed class WorkspaceNavigationPage : NavigationPage, IDisposable
    {
        private readonly Action cleanup;
        private bool disposed;
        public WorkspaceNavigationPage(Page root, Action cleanup) : base(root)
        {
            this.cleanup = cleanup;
            Popped += (_, args) => (args.Page as IDisposable)?.Dispose();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            cleanup();
            foreach (var page in Navigation.NavigationStack.OfType<IDisposable>()) page.Dispose();
        }
    }
}
