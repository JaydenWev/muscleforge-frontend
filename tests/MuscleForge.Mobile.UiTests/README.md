# Android smoke checks

Appium automation will be added with the first authenticated workout flow. No automated device test is claimed by this scaffold.

For this increment, install the debug APK and check on an Android device or emulator:

1. With no saved sign-in, the app launches with native sign-in. Open **Server settings** to configure the API. After signing in, the five Shell tabs appear: Workout, Exercises, Templates, History and Settings.
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
5. Wait through access expiry or use a dedicated test server with shortened lifetimes, then search/load Exercises: the protected request should recover without entering the password. Verify one refresh across concurrent callers through a native test consumer/debugger without displaying or logging token values; record that concurrency check as pending if it cannot be exercised.
6. Reject the saved refresh credential through a dedicated test account's security-stamp reset/expiry: require sign-in. Repeat startup without connectivity: show restoration failure; **Retry saved session** must recover after reconnecting.
7. Sign out from Settings: immediately leave the protected Shell. Back must not reveal it. Force-stop/reopen: remain signed out. Sign in, then clear saved sign-in from the unprotected recovery flow and verify restoration no longer occurs.
8. On an expendable test installation, simulate secure-storage read/write/remove failure or corrupt saved payload using a test build/injected adapter. Show a safe storage/recovery message; never grant access after a failed write, claim successful removal after a failed clear, remove unrelated preferences, or log credentials. Fakes in portable tests alone do not establish this platform behavior.
9. After logout, change server and attempt restore: never send the old server's credential to the new server. Verify other settings (URL/appearance) survive clearing the sign-in.

Auth automation IDs: `SignInEmail`, `SignInPassword`, `SignInStatus`, `SignInButton`, `SignInCancelButton`, `RestoreSessionCancelButton`, `SignOutButton`. Portable policy tests verify routing decisions; this checklist establishes actual platform storage, lifecycle and Back behavior. Appium automation is not implemented yet.

## MF-15 catalog and picker — pending device evidence

Use the MF-12 API with its catalog migration explicitly applied and owner configuration enabled. Record frontend/APK and backend revisions, device/Android version and trusted endpoint; use only expendable test accounts/data. Never capture credentials or token values. These checks are not claimed to have run.

1. Sign in, then open Exercises from both the bottom tab and Workout. See the owner's seeded exercises, equipment and correct total/per-dumbbell guidance. Search name/equipment, including literal `%`, `_` and backslash. Load additional pages from an account with more than 50 exercises without duplicate rows or stuck progress.
2. During a slow search, submit another query and cancel loading. Late results must not replace the newer query or be presented as a confirmed empty catalog. Retry after restoring connectivity. Check true empty results separately.
3. Create a custom exercise using both load conventions. Empty/whitespace/overlong fields must show validation; Save must remain reachable above the keyboard in portrait, landscape and large text. Repeated Save taps during acknowledgement/navigation must create only one exercise; repeated Archive taps must open only one confirmation. Verify TalkBack identifies fields/actions, the load choices are readable, and touch targets are usable.
4. Open/edit the saved exercise. Fresh loading must finish before edits are enabled. Check retry on a failed load and retention of entered fields on a failed save. A lost save response must not create a fake success or an automatic duplicate POST; inspect the catalog before retrying manually.
5. Cancel the archive confirmation and verify nothing changed. Confirm archive, verify it disappears from normal results and appears only with Show archived, and confirm editing its metadata retains archive status. Back must return to refreshed results; background/return must not unexpectedly overwrite an unsaved form.
6. From a native test consumer of `IExercisePicker` (MF-21 supplies the product consumer), select an active exercise and inspect only the returned server/account/exercise UUID scope, never tokens. Archive a visible row from another client before selecting it: selection must be rejected. The picker must exclude archived records.
7. Open the picker again and check explicit Cancel, Android Back, cancellation while waiting for a response/push/pop animation, and caller-token cancellation. Each pending request must finish once with no selection, remove its page and handlers, and allow a subsequent picker to open. Rotating/backgrounding alone must not complete a selection.
8. Sign out while a catalog/editor/picker request is pending, then sign in as a different account. No prior items, searches, form data or late selection may appear; Back must not reveal the old Shell. Requesting another owner's exercise UUID must show an honest not-found/error result.

Catalog IDs: `ExerciseSearch`, `ExerciseSearchButton`, `ExerciseShowArchived`, `ExerciseAdd`, `ExerciseName`, `ExerciseEquipment`, `ExerciseLoadConvention`, `ExerciseSave`, `ExerciseArchive`. The actual native picker Task completion, Shell stack and accessibility checks remain open until observed on Android; portable fakes do not establish them.
