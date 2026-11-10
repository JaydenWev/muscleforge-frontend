using MuscleForge.Mobile.Core.Authentication;

namespace MuscleForge.Mobile.Core.Navigation;

public enum SessionPage { Loading, SignIn, Protected }

public static class SessionNavigation
{
    public static SessionPage Decide(AuthStatus status, SessionPage current) => status switch
    {
        AuthStatus.SignedIn => SessionPage.Protected,
        AuthStatus.Restoring when current == SessionPage.Loading => SessionPage.Loading,
        _ => SessionPage.SignIn
    };
}
