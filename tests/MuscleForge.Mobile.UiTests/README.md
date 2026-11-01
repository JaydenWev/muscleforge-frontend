# Android smoke checks

Appium automation will be added with the first authenticated workout flow. No automated device test is claimed by this scaffold.

For this increment, install the debug APK and check on an Android device or emulator:

1. The app launches with the MuscleForge name and four Shell tabs.
2. Workout, Templates, and History show their planned state without editable workout data.
3. In Settings, enter a reachable HTTPS API base URL with a trusted certificate.
4. Save and check reports the supported API version, and retains the URL after restarting.
5. An HTTP URL is rejected. An unreachable server shows an error and allows retry.
6. The software keyboard does not hide the action; TalkBack identifies the URL field and action.

Stable automation IDs: `ApiBaseUrlEntry`, `CheckApiConnectionButton`, `ApiConnectionStatus`.
