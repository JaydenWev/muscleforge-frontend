# Android smoke checks

Appium automation will be added with the first authenticated workout flow. No automated device test is claimed by this scaffold.

For this increment, install the debug APK and check on an Android device or emulator:

1. With no saved sign-in, the app launches with native sign-in. Open **Server settings** to configure the API. After signing in, the four Shell tabs appear.
2. Workout, Templates, and History show their planned state without editable workout data.
3. In Settings, enter a reachable HTTPS API base URL with a trusted certificate.
4. **Save & connect** reports the supported API version and updates the connection badge on Workout. After restarting, the URL is retained and the badge reads **Not checked** until checked again. Editing the URL clears the previous success state.
5. An HTTP URL is rejected. An unreachable server shows an error and allows retry.
6. The software keyboard does not hide the action; TalkBack identifies the URL field and action.
7. The dashboard cards and **Open settings** buttons select the correct tab. No header, card, or bottom tab overlaps the status bar or gesture area.
8. Select Dark, Light, and System in **Set the mood**. Text, cards, artwork, tabs, and system bar icons stay legible. Restart and confirm the choice is retained. In System mode, change the Android appearance and confirm the app follows it.
9. Scroll every page with a larger Android font size and in landscape; the server field and action remain reachable.

Stable automation IDs: `ApiBaseUrlEntry`, `CheckApiConnectionButton`, `ApiConnectionStatus`, `AppearanceDark`, `AppearanceLight`, `AppearanceSystem`.

## MF-14 authenticated flow — pending device evidence

Use a reachable real backend with an owner account and a certificate trusted by this Debug build. Record the APK/frontend revision, backend revision, device model/Android version, certificate hostname/CA setup, and pass/fail observations. Never record passwords, refresh/access tokens, or secure-storage payloads.

1. Cold start without credentials: see sign-in, use Server settings, save/check the HTTPS address, then return to sign-in. HTTP must be rejected; an untrusted certificate or mismatched hostname must fail without an override.
2. Invalid email/empty password: show local validation. Wrong password: show failure and clear the password; remain outside the Shell. Turn off connectivity and attempt login: show a recoverable error. Restore connectivity and retry successfully.
3. During a slow login/restoration, check progress, disabled actions, reachable Cancel, keyboard scrolling, TalkBack labels, and password-manager autofill. Cancel must leave no newly saved session; force-stop/reopen to check. Also test large text and landscape.
4. Successful login: show the Shell and the actual API account email in Settings. Server address controls must be disabled. Force-stop/reopen: restore through the real refresh/me endpoints and return to the same account without entering the password.
5. Wait through access expiry or use a dedicated test server with shortened lifetimes: request a protected context and verify one refresh across concurrent callers. The existing scaffold has no private workout request screen, so exercise this through a device test harness/debugger without displaying or logging token values; record this check as pending if it cannot be exercised.
6. Reject the saved refresh credential through a dedicated test account's security-stamp reset/expiry: require sign-in. Repeat startup without connectivity: show restoration failure; **Retry saved session** must recover after reconnecting.
7. Sign out from Settings: immediately leave the protected Shell. Back must not reveal it. Force-stop/reopen: remain signed out. Sign in, then clear saved sign-in from the unprotected recovery flow and verify restoration no longer occurs.
8. On an expendable test installation, simulate secure-storage read/write/remove failure or corrupt saved payload using a test build/injected adapter. Show a safe storage/recovery message; never grant access after a failed write, claim successful removal after a failed clear, remove unrelated preferences, or log credentials. Fakes in portable tests alone do not establish this platform behavior.
9. After logout, change server and attempt restore: never send the old server's credential to the new server. Verify other settings (URL/appearance) survive clearing the sign-in.

Auth automation IDs: `SignInEmail`, `SignInPassword`, `SignInStatus`, `SignInButton`, `SignInCancelButton`, `RestoreSessionCancelButton`, `SignOutButton`. Portable policy tests verify routing decisions; this checklist establishes actual platform storage, lifecycle and Back behavior. Appium automation is not implemented yet.
