# 0001 — Native shell and contract boundary

Date: 2026-11-01

Status: Accepted for the initial scaffold

Use MAUI Mobile, portable Mobile.Core, and generated Client projects as specified in the baseline. Only Mobile contains platform APIs. Shell handles navigation; views bind to a portable view model with async commands and injected network/settings interfaces.

The first slice uses a public status endpoint. Workout, offline, and authentication capabilities arrive in later increments. Credentials must use SecureStorage and writes must preserve local drafts before network requests.

Python generator v1 handles the initial status contract deterministically without dependencies and fails on unsupported request/security/schema shapes. Extend it or replace it with a pinned general-purpose tool with the next API slice. The contract hash and backend commit remain explicit; generation stays local to this repository.

Tooling update (2026-11-09): the initial Python generator described above has been replaced by pinned NSwag.ConsoleCore 14.7.1. See [the current contract workflow](../api-contract.md). The repository and native UI boundaries in this decision remain in effect.

The application ID `local.muscleforge.app` is provisional. Finalize it and the signing identity before the first signed personal APK. Debug builds trust user-installed development CAs. Certificate and hostname verification remain enabled, and Android backups are disabled.

This increment does not complete milestone 1's login/ownership exit criteria. No SDK/Android runtime was available in the authoring environment. The first CI build and device checks remain required. Resolve and commit NuGet lockfiles with the first equipped restore; CI publishes them for review.
