# Units and time zones

Signed-in Settings loads and saves the account's weight unit and IANA time zone on the server. Choose kg or lb, enter an IANA identifier such as `Europe/Amsterdam`, or select **Use phone time zone** to fill the phone's current identifier; select **Save preferences** to persist either choice. The phone button does not change the server on its own. Server defaults for existing accounts are kg and `Etc/UTC`. A failed load or save is reported in Settings, and a failed save leaves the edits available for retry. Updates use last-write-wins semantics.

Weight values in future workout features will be submitted as the entered decimal and selected unit; the server converts to kilograms. One pound is exactly 0.45359237 kg. The unrounded kilogram equivalent must be at most 9,999,999.999 kg; the server rounds once to three fractional digits, away from zero, for storage. Zero is valid. Entry accepts at most three fractional digits with the current locale's decimal separator (Dutch comma or English dot), and rejects signs, grouping, exponents, and the other separator. Formatting a stored kilogram value uses at most three fractional digits, trims trailing zeroes, and does not overwrite the stored value. A future session must capture its time zone separately from this account preference.

## Native entry verification

Debug builds include an explicit-intent-only page containing the reusable weight entry control. It is not part of the product navigation and is excluded from Release. On a physical Android phone, launch the debug MainActivity with action `local.muscleforge.app.DEBUG_WEIGHT_ENTRY`, for example using `adb shell am start -n <application-id>/<main-activity-component> -a local.muscleforge.app.DEBUG_WEIGHT_ENTRY`. The page shows the current value's validity and unit. Verify numeric keyboard input, Dutch and English separators, invalid and excessive precision, keyboard dismissal, and accessibility on the actual device. Restart the app normally to return to product navigation. The portable tests do not establish physical keyboard behavior.

## Verification checkpoint

The portable Release build passed with zero warnings/errors and all 114 in-process tests passed. Coverage includes shared semantic fixtures, account/server scope, refresh and cancellation, preference failures, and rejection of omitted required response fields. The contract is pinned in `contracts/source.json`, and the copied semantic examples have independent commit/hash provenance in `contracts/units-timezones-source.json`.

Android Debug/Release builds, trimming diagnostics for the JSON required-member modifier, APK checks, the Settings-to-API phone flow and physical Dutch/English keyboard checks remain pending. The explicit debug intent and its cold-start behavior are unverified. Work was stopped at the owner's request on 2026-11-13; no further checks were started after that request.
