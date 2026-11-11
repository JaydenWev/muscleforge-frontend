# Exercise catalog — MF-15

After sign-in, open **Exercises** from the bottom tabs or Workout. Search by exercise name/equipment and use **Load more** for additional results. Results use the backend's literal case-insensitive search and pages of 50. **Show archived** includes archived records for management; normal selection excludes them. Lists use native virtualized `CollectionView` controls with loading, empty, cancellation, error and retry states.

**Add exercise** opens a native form for name, equipment and load convention. **Open** fetches a fresh copy before enabling edits. Names/equipment must be nonblank and at most 100 characters after trimming. **Total external load** includes the bar for barbell exercises; **Per dumbbell** means the weight of one dumbbell. Only external-weight-and-repetitions tracking is supported. Save shows success only after a valid API acknowledgement; malformed responses or wrong resource identities do not become saved records.

Archiving requires confirmation and cannot be undone through this API. It removes the exercise from normal selection while retaining it for management. An archived exercise can still have its metadata edited; the app keeps its archive state. Template/session creation will enforce archive selection rules in those future features. This catalog does not create a workout or template.

## Failures and identity

Local validation and known server field errors give fixed guidance without echoing arbitrary response text. Forms retain entered values when saving fails. A lost response may follow a successful server mutation: the app tells the owner to check the catalog before trying again and never automatically retries a write after a network failure. Exercise metadata uses the backend's last-write-wins rules, without version receipts or offline replay. Forms remain in memory; durable workout drafts belong to MF-25 and later work.

The portable gateway captures MF-14's `AuthContext` with server URI, account UUID and access token. Calls never pair an old token with mutable connection settings. One 401 can trigger the shared refresh and one retry for the same account/server; other errors and ambiguous write failures are not automatically replayed. Late responses after a session change or cancellation cannot populate another account's page. Logout clears/disposes catalog state and replaces the protected Shell.

## Reusable selection for MF-21

`IExercisePicker.PickAsync(CancellationToken)` returns a nullable `ExerciseSelection` containing server URI, account UUID and exercise UUID, without credentials. The native picker lists active exercises, performs an authoritative item lookup before selection, and rechecks session scope after closing. A row archived since loading cannot be newly selected. Explicit Cancel, caller cancellation, Back and session changes should complete without a selection; pending operations and view-model subscriptions are cleaned up when navigation removes their pages.

MF-21 supplies the template consumer. It must validate the returned server/account scope and let the backend check archive state again when saving the template; a client lookup is not an atomic promise that the exercise remains active. The catalog's **Open** action edits/manages the item; it does not pretend to add an exercise to a workout. Native picker/Back completion behavior still requires device evidence.

## Contract and verification

The independent frontend pins backend implementation `c2ac02fccdf912e4bb6318688d0cfa8c0f7b06cb`, snapshot SHA-256 `02bb5de0ff0142ff0a5717575480c525072c1ef6fbc84d09a05b39ff9c858e8f`, OpenAPI 3.0.4, document `0.1.0-dev`, `/api/v1`. NSwag.ConsoleCore 14.7.1 generates the client/DTOs. The facade accepts the declared `201 Created` for creation, keeps undeclared status rejection, uses per-request bearer headers and sanitizes malformed successful bodies. No generator replacement, new package or backend domain reference is used.

All 78 portable tests pass, including auth/status regressions, generated client creation/errors, response identity checks, bounded refresh, ambiguous write behavior, search ordering/cancellation, pagination deduplication, form retention, session invalidation and archived selection. The portable Release build passes with warnings treated as errors and zero warnings. These results use HTTP/storage/navigation fakes and do not prove native Shell or device behavior.

The final Android Debug build passes with warnings treated as errors and zero warnings. The standalone signed APK payload check passes for `arm64-v8a` and `x86_64`. Pinned NSwag regeneration/drift checks also pass. These are build/package checks, not a device launch or navigation result.

The explicit temporary integration harness passed the actual session, generated client, catalog gateway and view models against the MF-12 API test host and isolated PostgreSQL with synthetic accounts: seeded listing, create/edit/archive, stale active-row selection after server archive, archived metadata editing, other-owner 404 responses, and logout/account-change cleanup. This establishes API-to-view-model behavior, not native networking, keyboard or Shell behavior. No cross-repository reference is committed into a product project.

Delivery gates remain explicit: native form/keyboard/accessibility and picker Back/cancellation checks from the [device checklist](../tests/MuscleForge.Mobile.UiTests/README.md), PR/CI integration, and verification of the MF-12 backend merge/deployment. At implementation start, the owner reported merge, but both GitHub and `git ls-remote` still showed backend main at `2f9fe981` without MF-12. The app is built against the tested pinned MF-12 contract; no merge or native device result is inferred from that pin. ADB daemon startup remains denied in this workspace.
