# Local templates — MF-21

> MF-97 supersedes the account-based navigation and exercise-selection behavior described below. See [guest workspace](guest-workspace.md) for the current workflow and pending verification; the verification record below applies to MF-21.

Templates are owned by the device and stored in its private SQLite database. The Templates screen lists active templates, can include archived templates, and opens details or a new editor. A template has a name and an ordered list of exercises; each exercise can have optional rest and ordered warm-up or working sets with optional repetition and weight targets. Exercises and sets can be added, removed and reordered. Archive hides a template from the active list while retaining its contents for viewing; it does not delete it.

Choose **Save template** to persist changes. **Saved on device** appears after the SQLite transaction commits. Validation, storage and revision-conflict errors leave the editor's unsaved changes available for correction or retry; reloading the saved version requires confirmation when edits would be discarded. Leaving a dirty editor also asks for confirmation. Unsaved editor changes are held in memory, not a durable draft: process death or closing the app can lose them. Save before leaving the app.

The editor offers kg and lb and converts entered values to canonical kg for storage. The selector currently starts at kg; it does not automatically follow or synchronize the server's unit preference. Displayed offline exercise metadata is a snapshot and may be stale.

## Exercise selection and offline use

When signed in to the matching account and server, the picker can use the online exercise catalog or saved exercise metadata. Offline selection uses metadata already present in this account/server's saved template snapshots, including archived templates; a known archived exercise cannot be newly selected. An empty local workspace has no saved exercise metadata, so selecting its first exercise requires the online catalog. Existing exercises remain in their templates when offline; metadata from another account or server is never substituted.

A previously verified workspace can reopen locally after a process restart without waiting for network authentication. Its Templates screens do not need a server probe or template endpoint. Background authentication can complete while the local workspace remains open; **Open app** enters the full authenticated shell. An offline or expired session does not authorize HTTP: network features still require an authenticated session for the same account and normalized server. Sign-in and server settings remain available.

Explicit sign-out suspends the local workspace immediately, records a signed-out marker and clears the refresh credential. The marker prevents automatic session restoration if credential removal fails; the app reports a storage failure and offers a retry. If the marker itself cannot be saved, durable logout across restart cannot be guaranteed, so retry before closing the app. Sign-out does not delete SQLite templates. They remain isolated by verified account and server and become accessible again after signing into the same scope. See [native authentication](native-auth.md) for credential handling.

Local persistence is not backup: clearing app data or uninstalling removes templates. Server sync, backup and restore are deferred to MF-96. Workout start and history remain server-backed; selecting a local template and handing its snapshot to workout start is MF-26. There is no template start action in this increment. Workout drafts are MF-25.

## Verification status

The portable Release builds passed with warnings treated as errors and zero warnings or errors: 140 Core tests and 16 real SQLite tests pass. The additional logout regression verifies that retrying restoration after a failed signed-out-marker write cannot reopen a suspended workspace in the same process. [CI for revision eb6bef2](https://github.com/JaydenWev/muscleforge-frontend/actions/runs/44485766751) passed Core, real SQLite, generated-client drift, and Android Debug/Release packaging checks. The local Debug build and APK guards also passed after the review fixes.

Physical OnePlus 13 / Android 16 checks passed native template creation/editing/details, cached and online exercise selection, explicit saved-on-device status, dirty-editor Back confirmation, archive retention/filtering, force-stop reopening with the synthetic APIs unavailable, and same-signer installation over a populated template graph. Under en-NL, `20,5` kg is accepted and `20.5` is rejected without losing the draft. Under en-US, `21.5` is accepted. Creating a template with 100 lb while the APIs were stopped reopened as exactly 45.359 kg. Test APIs and temporary phone locale/timeout settings were restored afterward. The earlier [MF-20 storage checkpoint](local-template-storage.md) records a separate physical-device adapter probe.

On 2026-11-19 the owner confirmed that editing, saving and reopening a template succeeded in airplane mode with Wi-Fi off. Further checks on the updated APK retained exercise order (Deadlift before Bench Press) and prescribed set order/kind (warm-up 2 kg before working 1 kg) through force-stop/reopen. Explicit logout, Android Back and force-stop/relaunch did not reopen the signed-out workspace. A second synthetic account could not see the owner's templates; the same second account UUID on another server had a separate empty library. Restoring the original server/account exposed the owner's retained template again. Disposable accounts were removed, the original server/account restored and screen timeout returned to its original value; the main app's data was retained.

The owner explicitly requested skipping the remaining phone UI checks for injected storage failures, loading cancellation and revision-conflict recovery and proceeding with delivery. These behaviors have automated coverage, but full native failure-path acceptance is not claimed. No additional tests were run for the final documentation-only delivery commit. Unit preference still starts at kg, unsaved edits remain in memory, and server backup is deferred as described above.

## Workout-start consumer handoff

MF-26 can capture an immutable committed snapshot from the same local store using the verified workspace scope:

```csharp
var snapshot = await templates.GetAsync(scope, templateId, cancellationToken)
    ?? throw new InvalidOperationException("The selected template is unavailable.");
if (snapshot.IsArchived)
    throw new InvalidOperationException("Choose an active template.");
// Capture snapshot.Id, snapshot.Revision and its stable exercise/set IDs.
// WeightKg is already canonical kg; do not convert it again.
```

Freeze that snapshot and the session-start request/IDs before sending. Starting a workout still requires online authentication, ownership validation and the MF-22/MF-26 implementation. A cached local workspace provides neither API credentials nor server authorization. The request must not require template upload or a matching server-template foreign key, and later local edits must not change an already captured session snapshot. `contracts/fixtures/local-template.json` supplies the consumer example; no start button or synchronization protocol is implemented here.
