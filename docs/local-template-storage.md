# Local template storage

> MF-97 adds a device guest partition and additive schema version 3 with import receipts. See [guest workspace](guest-workspace.md) for the current scope, migration and pending verification. Account-based workflow and verification references below describe earlier increments.

MF-20 adds the durable local template store, its schema and Android registration. MF-21 supplies the native template screens and offline workspace navigation. This increment does not add those screens, template HTTP endpoints, an outbox or server synchronization. Server template backup/restore remains deferred to MF-96; workout start/save/history remain server-backed.

## Ownership and project boundaries

`Mobile.Core/Templates` contains immutable template snapshots, validation and `ILocalTemplateStore`. It has no SQLite or MAUI dependency. `Mobile.Storage` is a portable assembly containing the actual sqlite-net-pcl adapter; both the app and the real SQLite test project reference that same assembly. Mobile owns its lazy singleton registration and app-private path, `FileSystem.AppDataDirectory/muscleforge.db3`. There is no linked test copy of the implementation and no dependency on the backend checkout.

The owner-approved local-first decision supersedes the original online-template scope. [Decision 0002](decisions/0002-local-template-storage.md) records this change and the portable adapter boundary. Guest initialization may open the database to import a previously verified matching library.

## Scope and operations

`TemplateScope.Create(server, accountId)` normalizes a valid HTTPS API root using the existing endpoint parser and rejects empty account IDs, credentials, query strings and fragments. The pair of normalized server URI and account UUID partitions every template and child query/write. Different API roots remain different partitions even if they happen to reach the same deployment.

This is a local identity key, not proof of authentication. Callers supply a previously verified scope; MF-21 owns restoring the appropriate cached local workspace and preventing access after explicit logout or through another account/server. The store itself never refreshes credentials or calls an API. Existing sign-out does not delete this database. No passwords or bearer/refresh tokens are stored here.

- `GetAsync` returns an ordered complete snapshot, or null when absent in the supplied scope; archived snapshots remain available.
- `ListAsync` returns summaries ordered by case-insensitive name then ID. Offset must be nonnegative and page size is 1–100; the caller explicitly chooses whether to include archived templates.
- `SaveAsync` takes a full immutable candidate and expected local revision. New candidates use revision/expected revision 0 and are saved as revision 1. Updates must match both candidate and persisted revision; successful commits increment once. Conflicts leave the existing graph untouched.
- `ArchiveAsync` requires the current positive revision, records a UTC timestamp and increments the revision without deleting children. Archived templates cannot be edited or unarchived through save. A missing scoped template and stale/archived revision have distinct typed errors.

Callers generate stable template/exercise/set UUIDs. Snapshot constructors defensively copy child collections; local revisions are distinct from future remote versions. `TemplateValidationException`, `TemplateConflictException`, `TemplateNotFoundException` and `TemplateStorageException` allow the future UI to preserve edits and present appropriate recovery. No local operation reports success before its SQLite transaction commits. Cancellation is checked before work and before commit; it does not undo an already completed commit or promise to interrupt a native SQLite call immediately.

## Values and transactions

Templates retain owner-scoped exercise name/equipment/tracking/load metadata for offline use. A known archived exercise cannot be newly selected, but an already referenced exercise can remain under the same local child/catalog identity while the user repairs a stale plan. Online session start will revalidate current server exercise ownership/archive state separately.

Names are trimmed and bounded to 100 characters. Positions 0–99 are unique within each parent, allowing gaps and at most 100 exercises/sets. Rest belongs to the exercise and is absent or 0–86,400 seconds. Prescribed repetition endpoints are either both absent or both positive and ordered. Warm-up/working kind, optional target weight and cached load convention are preserved.

Input kg/lb conversion uses MF-18's existing exact decimal helper and pinned examples. Canonical kg is nonnegative, no more than 9,999,999.999 and has at most three fractional digits. SQLite stores nullable **64-bit integer thousandths of kg**, up to 9,999,999,999, rather than REAL. CHECK constraints also reject fractional integer fields, invalid ranges/kinds and malformed repetition pairs. Session-start target fields consume canonical kg and must not convert pounds a second time.

Each operation owns/disposes its native connection. Foreign keys are enabled explicitly; full graph reads use a read transaction. Writes and migration initialization use `BEGIN IMMEDIATE`, a bounded busy timeout, parameterized values and revision predicates. Replacing child rows, updating the revision and reading the resulting snapshot happen within one transaction. Any validation/conflict/SQL failure rolls back the graph; the caller's immutable candidate is not modified. SQLite parent/child keys include the complete account/server scope.

## Schema evolution and dependencies

The application database uses `PRAGMA user_version`. Version 1 creates the complete template graph; version 2 additively supplies positive integer local revisions and the listing index. Version 3 adds import receipts without replacing existing tables or data. Initialization and version changes are transactional, and a newer unsupported version fails without dropping data. A populated independent v1 SQL fixture verifies that IDs, account scope, ordering, rest, prescriptions and exact weights survive the migration to the current schema; MF-97 also verifies a populated v2 upgrade and non-destructive import.

MF-25 must extend this same populated database/migration sequence for drafts and cached workout data. Do not create a competing database version owner, reset user_version, or drop/recreate tables on startup. Templates are locally owned data retained across logout, not disposable cache. Explicit deletion is separate; uninstalling or clearing app data removes local content. Local persistence is not server backup and is not encrypted by default; Android backup remains disabled as before.

[sqlite-net-pcl 1.11.285](https://www.nuget.org/packages/sqlite-net-pcl/1.11.285) is centrally pinned. Lockfiles resolve SQLitePCLRaw core/provider 3.0.3 and [SourceGear.sqlite3 3.53.3](https://www.nuget.org/packages/SourceGear.sqlite3/3.53.3). Package inspection confirmed Linux arm64 and Android arm64/x64 native assets and the SQLite public-domain license. No second native bundle is added. Explicit DynamicDependency annotations preserve the adapter's sqlite-net row DTOs for trimming; the separate physical-phone probe verifies this production adapter with full trimming and AOT.

## Verification checkpoint

The complete portable Release build passed with warnings as errors and zero warnings/errors. All 121 core tests and 14 real SQLite tests passed with zero errors, failures or skips. Tests cover conversion fixtures, immutable inputs, exact weight round trips, scoped isolation, concurrent revision/initialization races, archival, cancellation, SQL constraints, trigger-induced full rollback, file reopen, populated upgrades and unsupported future-version preservation. TRX reports are retained under ignored `artifacts/verification/` in the development worktree.

The main Android Debug build passed with warnings as errors and zero warnings/errors. The managed-payload and Debug network-policy guards passed, and its APK includes native SQLite for arm64-v8a and x86_64. The pinned generated API clients are current. Normal main-app Release compilation remains a CI check.

On 2026-11-18, a OnePlus 13 running Android 16 passed the separate [Android storage probe](../tests/MuscleForge.Mobile.Storage.DeviceTests/README.md). Its Release build enables full trimming and AOT and passed with zero warnings/errors. `MF20 seed PASS` and `MF20 verify PASS` came from different Android process IDs across a force-stop. The exact production adapter persisted the graph, stable IDs, exercise rest, null and maximum weights, and 45.359 kg; verification also checked account isolation, update revisions, stale-write rejection, archival and reopen. The probe used only synthetic data in its own private package and was removed afterward; the installed MuscleForge app/data was untouched.

MF-21 implements native template screens and cached-workspace navigation. Android builds, physical-phone ordering, force-stop/reopen, populated app upgrade, logout and account/server isolation passed; the owner also confirmed airplane-mode editing and reopening. See [local templates](local-templates.md) for the full evidence and the owner's decision to skip remaining phone failure-path checks. The isolated MF-20 adapter probe alone does not establish those UI flows, server backup or a second physical ABI.
