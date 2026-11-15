# Units and time zones

Signed-in Settings loads and saves the account's weight unit and IANA time zone on the server. Choose kg or lb, enter an IANA identifier such as `Europe/Amsterdam`, or select **Use phone time zone** to fill the phone's current identifier; select **Save preferences** to persist either choice. The phone button does not change the server on its own. Server defaults for existing accounts are kg and `Etc/UTC`. A failed load or save is reported in Settings, and a failed save leaves the edits available for retry. Updates use last-write-wins semantics.

Weight values in future workout features will be submitted as the entered decimal and selected unit; the server converts to kilograms. One pound is exactly 0.45359237 kg. The unrounded kilogram equivalent must be at most 9,999,999.999 kg; the server rounds once to three fractional digits, away from zero, for storage. Zero is valid. Entry accepts at most three fractional digits with the current locale's decimal separator (Dutch comma or English dot), and rejects signs, grouping, exponents, and the other separator. Formatting a stored kilogram value uses at most three fractional digits, trims trailing zeroes, and does not overwrite the stored value. A future session must capture its time zone separately from this account preference.

## Native entry verification

Debug builds include an explicit-intent-only page containing the reusable weight entry control. It is not part of the product navigation and is excluded from Release. On a physical Android phone, launch the debug MainActivity with action `local.muscleforge.app.DEBUG_WEIGHT_ENTRY`, for example using `adb shell am start -n <application-id>/<main-activity-component> -a local.muscleforge.app.DEBUG_WEIGHT_ENTRY`. The page shows the current value's validity and unit. Verify numeric keyboard input, Dutch and English separators, invalid and excessive precision, keyboard dismissal, and accessibility on the actual device. Restart the app normally to return to product navigation. The portable tests do not establish physical keyboard behavior.

## Verification checkpoint

The portable Release build and all 114 tests passed. Coverage includes shared semantic fixtures, account/server scope, refresh and cancellation, preference failures, and rejection of omitted required response fields. The contract is pinned in `contracts/source.json`, and the copied semantic examples have independent commit/hash provenance in `contracts/units-timezones-source.json`. [Hosted CI run 43729569064](https://github.com/JaydenWev/muscleforge-frontend/actions/runs/43729569064) passed the core and Android Debug/Release jobs on 2026-11-15.

Local Android Debug and normal-trimming/AOT Release builds from frontend `6517e96055027841f024fcd38ed08d029c879b6d` also passed on 2026-11-15 with zero warnings/errors. Both configurations passed the standalone managed-payload and packaged network-policy checks for arm64-v8a and x86_64. Copies signed with the existing development identity are retained under the ignored `artifacts/mf18/` directory:

| Configuration | APK SHA-256 |
| --- | --- |
| Debug development-signed copy | `229f06b86eec98e9da75d18defd3a636da6be2ba223c84a99af088ee3309e263` |
| Release development-signed copy | `3498620bc61bd970986208cecb81a9ed94c8b9bbbd9ce8724023125a8662e459` |

Both copies have signing-certificate SHA-256 `f89ca854b548317aa4ab86c3b21714c0438644380285495ee5f55b2da3b79326`, matching the existing development installation. These are verification artifacts, not MF-32's final release identity.

The Settings-to-API phone flow, actual Dutch/English numeric-keyboard checks, the debug intent's cold-start behavior, and JSON required-member behavior in the trimmed Android runtime remain unverified. Successful builds and portable tests do not establish those device results. Work resumed on 2026-11-15; ADB had no connected phone when this checkpoint was recorded.
