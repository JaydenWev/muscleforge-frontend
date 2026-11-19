# 0003 — Device-local guest workspace

Date: 2026-11-19  
Status: Implemented for MF-97; accepted by the owner with the verification limits recorded in the guest workspace guide

The app's local core opens directly into a device workspace without login, onboarding or network access. A separately persisted device UUID identifies its local template partition. Existing authentication implementation and credentials are retained, but startup does not read or refresh them and this core has no account route. Optional account backup and synchronization are deferred to MF-96; local persistence is not backup.

The existing SQLite database remains the single owner of local templates. Its additive version 3 adds durable import receipts. The initial guest identity binds automatic import to a previously verified version 1 cached workspace on the configured server; signed-out version 2 and other libraries require explicit selection in Settings. The binding persists with the guest ID, so later account/server changes never silently select another source and failed imports retry the original choice. Imports preserve the source and its complete graph. A destination ID collision is reported without overwriting; receipts prevent retries from overwriting edits or recreating deleted guest templates. This does not introduce merge, sync, HTTP or an outbox.

MF-98 will supply a full offline catalog. Until then the editor can select only exercise metadata already saved in templates, and a fresh user can save empty plans. MF-25 and MF-26 cover local workouts and starting from templates. The weight unit selector defaults to kg.

This decision supersedes the account-based startup and online workout assumptions in [decision 0002](0002-local-template-storage.md) and earlier architecture documents. See [guest workspace](../guest-workspace.md) for behavior, migration limits and verification status.
