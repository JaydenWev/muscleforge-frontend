# Units and time zones

Signed-in Settings loads and saves the account's weight unit and IANA time zone on the server. Choose kg or lb, enter an IANA identifier such as `Europe/Amsterdam`, or select **Use phone time zone** to fill the phone's current identifier; select **Save preferences** to persist either choice. The phone button does not change the server on its own. Server defaults for existing accounts are kg and `Etc/UTC`. A failed load or save is reported in Settings, and a failed save leaves the edits available for retry. Updates use last-write-wins semantics.

Weight values in future workout features will be submitted as the entered decimal and selected unit; the server converts to kilograms. One pound is exactly 0.45359237 kg. The unrounded kilogram equivalent must be at most 9,999,999.999 kg; the server rounds once to three fractional digits, away from zero, for storage. Zero is valid. Entry accepts at most three fractional digits with the current locale's decimal separator (Dutch comma or English dot), and rejects signs, grouping, exponents, and the other separator. Formatting a stored kilogram value uses at most three fractional digits, trims trailing zeroes, and does not overwrite the stored value. A future session must capture its time zone separately from this account preference.

## Native entry verification

Debug builds include an explicit-intent-only page containing the reusable weight entry control. It is not part of the product navigation and is excluded from Release. On a physical Android phone, launch the debug MainActivity with action `local.muscleforge.app.DEBUG_WEIGHT_ENTRY`, for example using `adb shell am start -n <application-id>/<main-activity-component> -a local.muscleforge.app.DEBUG_WEIGHT_ENTRY`. The page shows the current value's validity and unit. It is deliberately a root page without an in-app Back option. Verify numeric keyboard input, Dutch and English separators, invalid and excessive precision, keyboard dismissal, and accessibility on the actual device. Force-stop and launch normally to return to product navigation. The portable tests do not establish physical keyboard behavior.

## Verification checkpoint

The portable Release build and all 114 tests passed. Coverage includes shared semantic fixtures, account/server scope, refresh and cancellation, preference failures, and rejection of omitted required response fields. The contract is pinned in `contracts/source.json`, and the copied semantic examples have independent commit/hash provenance in `contracts/units-timezones-source.json`. [Hosted CI run 43729569064](https://github.com/JaydenWev/muscleforge-frontend/actions/runs/43729569064) passed the core and Android Debug/Release jobs on 2026-11-15.

Local Android Debug and normal-trimming/AOT Release builds from frontend `6517e96055027841f024fcd38ed08d029c879b6d` also passed on 2026-11-15 with zero warnings/errors. Both configurations passed the standalone managed-payload and packaged network-policy checks for arm64-v8a and x86_64. Copies signed with the existing development identity are retained under the ignored `artifacts/mf18/` directory:

| Configuration | APK SHA-256 |
| --- | --- |
| Debug development-signed copy | `229f06b86eec98e9da75d18defd3a636da6be2ba223c84a99af088ee3309e263` |
| Release development-signed copy | `3498620bc61bd970986208cecb81a9ed94c8b9bbbd9ce8724023125a8662e459` |

Both copies have signing-certificate SHA-256 `f89ca854b548317aa4ab86c3b21714c0438644380285495ee5f55b2da3b79326`, matching the existing development installation. These are verification artifacts, not MF-32's final release identity.

A live Linux .NET consumer of the actual generated client and preferences gateway passed against the isolated PostgreSQL-backed HTTPS API on 2026-11-15: sign in, read preferences, save and confirm lb with `Europe/Amsterdam`, restore and confirm the original preferences, and sign out. It used normal OS certificate validation. This establishes the portable HTTP path, not Android SecureStorage or trimming behavior.

## Physical-phone checkpoint — 2026-11-15

On the OnePlus 13 (CPH2653, Android 16/API 36), the Debug development-signed APK above was installed as an update, preserving app data. It connected through the normal Android HTTP handler and installed development CA to the isolated PostgreSQL-backed API at `https://jadyn-server:7444/`, image `muscleforge-backend:mf18-units-timezones` from backend `5a0b1546bb09c476358306cf46f259fd2a517be2`.

After native sign-in, Settings loaded kg and `Etc/UTC`. Selecting lb and **Use phone time zone** filled `Europe/Amsterdam`; saving and reloading returned both values. Force-stop, relaunch and reopening Settings retained lb and `Europe/Amsterdam`. Saving `Invalid/Zone` showed the save-error message while retaining the entered value. Selecting the phone time zone and saving again recovered with **Preferences saved.** These are actual native/generated-client/API/PostgreSQL results.

The weight verification page opened through a warm explicit intent, but a cold-start intent was overwritten by authentication navigation. The debug-only fix in frontend `ec20a90` makes the session navigator own the verification page before restoration can replace it; manager and independent review found no actionable defects. Its Debug build passed with zero warnings/errors, both ABI payload guards and the compiled Debug network-policy guard. The development-signed copy is `artifacts/mf18/local.muscleforge.app-Debug-coldstart-development-Signed.apk`, SHA-256 `9b5fb79a421c9115b285b601856ffa7ac20ce5cc267400852a49f9671d00fb91`, with the same signing identity above. The following physical follow-up supersedes the pending cold-start and keyboard checks. JSON required-member behavior has not been separately exercised in the trimmed Android runtime.

## Physical numeric-keyboard follow-up — 2026-11-16

The same OnePlus 13 / Android 16 successfully cold-started the verification page with the pinned `ec20a90` APK. Actual Gboard key taps accepted English `12.5` and Dutch `12,5`. They also exposed an Android input-filter defect: using the other separator silently removed it, turning either `12.5` or `12,5` into valid `125` in the opposite locale. The initial keyboard check therefore failed.

Frontend `3dfd311` preserves entered text for the existing strict validator while requesting the numeric decimal/sign keyboard, scoped to the weight control. Manager and independent review found no actionable defects. Its Debug build passed with zero warnings/errors, both ABI payload checks, compiled Debug network policy and APK v2/v3 signature verification. The installed update is `artifacts/mf18/local.muscleforge.app-Debug-numeric-development-Signed.apk`, SHA-256 `290cd75f4f749033d9e4c89c3b5f2bba639db2a8a11f42e8f57770c7ddef2c54`, signed with the same development identity. App data was preserved.

After installing that update, these regression checks passed using taps on Gboard's actual on-screen numeric keys, with Android per-app locales `en-US` and `nl-NL` and a fresh process for each locale:

| Case | English input/result | Dutch input/result |
| --- | --- | --- |
| Correct separator | `12.5` → Valid: 12.5 kg | `12,5` → Valid: 12.5 kg |
| Wrong separator | `12,5` retained → Invalid | `12.5` retained → Invalid |
| Zero external load | `0` → Valid: 0 kg | `0` → Valid: 0 kg |
| Negative load | `-1` retained → Invalid | `-1` retained → Invalid |
| Excess precision | `1.2345` retained → Invalid | `1,2345` retained → Invalid |

A separate native text smoke check delivered `1e3` through ADB: the full text remained visible and Invalid. This was not an actual clipboard-paste check. Android Back dismissed the keyboard while leaving the verification page visible. Force-stop and normal launch returned to the protected Workout screen, confirming normal startup after the debug route. The original empty per-app locale override and 120000 ms screen timeout were restored and read back. No emulator or TalkBack verification was performed.
