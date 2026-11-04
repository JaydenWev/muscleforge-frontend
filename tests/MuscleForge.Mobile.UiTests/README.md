# Android smoke checks

Appium automation will be added with the first authenticated workout flow. No automated device test is claimed by this scaffold.

For this increment, install the debug APK and check on an Android device or emulator:

1. The app launches with the MuscleForge name and four Shell tabs.
2. Workout, Templates, and History show their planned state without editable workout data.
3. In Settings, enter a reachable HTTPS API base URL with a trusted certificate.
4. **Save & connect** reports the supported API version and updates the connection badge on Workout. After restarting, the URL is retained and the badge reads **Not checked** until checked again. Editing the URL clears the previous success state.
5. An HTTP URL is rejected. An unreachable server shows an error and allows retry.
6. The software keyboard does not hide the action; TalkBack identifies the URL field and action.
7. The dashboard cards and **Open settings** buttons select the correct tab. No header, card, or bottom tab overlaps the status bar or gesture area.
8. Select Dark, Light, and System in **Set the mood**. Text, cards, artwork, tabs, and system bar icons stay legible. Restart and confirm the choice is retained. In System mode, change the Android appearance and confirm the app follows it.
9. Scroll every page with a larger Android font size and in landscape; the server field and action remain reachable.

Stable automation IDs: `ApiBaseUrlEntry`, `CheckApiConnectionButton`, `ApiConnectionStatus`, `AppearanceDark`, `AppearanceLight`, `AppearanceSystem`.
