using MuscleForge.Mobile.Core.Authentication;
using MuscleForge.Mobile.Core.Navigation;
using Xunit;

namespace MuscleForge.Mobile.UnitTests;

public class SessionNavigationTests
{
    [Theory]
    [InlineData(AuthStatus.Restoring, SessionPage.Loading, SessionPage.Loading)]
    [InlineData(AuthStatus.Restoring, SessionPage.SignIn, SessionPage.SignIn)]
    [InlineData(AuthStatus.SigningIn, SessionPage.SignIn, SessionPage.SignIn)]
    [InlineData(AuthStatus.SignedIn, SessionPage.Loading, SessionPage.Protected)]
    [InlineData(AuthStatus.SigningOut, SessionPage.Protected, SessionPage.SignIn)]
    [InlineData(AuthStatus.SignedOut, SessionPage.Protected, SessionPage.SignIn)]
    [InlineData(AuthStatus.RequiresSignIn, SessionPage.Protected, SessionPage.SignIn)]
    [InlineData(AuthStatus.StorageError, SessionPage.Protected, SessionPage.SignIn)]
    [InlineData(AuthStatus.ConnectionError, SessionPage.Protected, SessionPage.SignIn)]
    public void Only_signed_in_state_exposes_protected_pages(AuthStatus status, SessionPage current, SessionPage expected) =>
        Assert.Equal(expected, SessionNavigation.Decide(status, current));
}
