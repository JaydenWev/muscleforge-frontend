# Native authentication — MF-14

The native sign-in page calls the pinned backend login endpoint, then `/api/v1/auth/me` to resolve the authoritative UUID/email. Only a verified account with successfully saved refresh credentials opens the protected Shell. Startup shows a restoration screen while checking saved credentials. Sign-out replaces the window root immediately, removes the app's credential key, and prevents Back from returning to the old Shell.

Set the HTTPS server address through **Server settings** before signing in. An unreachable server, invalid password, missing or invalid saved session, and unavailable secure storage produce explicit messages. **Retry saved session** retries restoration after connectivity or storage recovers. **Clear saved sign-in** retries local credential removal. Sign-in and restoration can be cancelled. Password whitespace is preserved and the view-model password is cleared after every attempt. The password is never saved by the app; Android's user-controlled password manager may offer autofill on supported versions.

## Storage and identity

`AuthSession` lives in platform-independent Mobile.Core. `ApiAuthGateway` adapts the generated-client facade. `SecureRefreshCredentialStore` uses one owned SecureStorage key, `muscleforge.auth.refresh.v1`. Its versioned JSON value contains the normalized HTTPS server, expected account UUID and refresh token. It contains no access token or password. Secure-storage errors are sanitized, and clearing a session removes only this key.

Access tokens exist only in memory. Every returned `AuthContext` captures the server URI and account UUID alongside its access token; consumers must use that captured URI. A saved credential for another server is never transmitted. Restoration/refresh resolves `/auth/me` again and rejects a mismatched UUID. Server settings are disabled while signed in. There is no token/password storage in Preferences, SQLite, source, or logs, and credential-bearing application types redact `ToString()` output.

The service coordinates concurrent refreshes through one shared task. A cancelled caller stops waiting without cancelling refresh for other callers. Expiry is measured from request start with a safety margin. A renewed refresh credential is persisted before the new access context becomes available. A generation guard rejects late results after logout, another sign-in, or a server change; a storage gate orders writes and removal. Cancellation after a platform write removes the interrupted credential before reporting cancellation. Storage failure never grants an authenticated context.

Refresh rejection requires online sign-in. A transient network failure drops the in-memory authenticated state but retains the saved refresh credential for **Retry saved session**. Corrupt/unknown envelopes remain untouched until the user clears or replaces saved sign-in. Failed removal is reported: the app cannot promise a successful logout across restart until secure removal succeeds.

## Server guarantees and future work

This feature consumes `contracts/source.json` unchanged: backend revision `24567a7b195b663d4dd2d99ecc5b40971a99a3e0`, SHA-256 `66715ce7d5b7572567248db444d4b3b816a6cd1a8e478ea0e07fc7213845e902`, NSwag 14.7.1. HTTPS certificate and hostname validation stay enabled. Android backup remains disabled; Release trusts system CAs, while Debug also permits a user-installed development CA.

The backend uses opaque Identity access/refresh tokens (15 minutes/14 days). Refresh credentials may be reused until expiry or security-stamp invalidation. Local logout is credential removal, without a server revocation endpoint or per-device revocation promise. A different API must not be assumed to have the same refresh semantics.

There are no workout drafts yet. MF-25, MF-28 and MF-39 must add account-scoped persistence and the pending-data logout/account-change guard before local edits exist. MF-37 extends authentication recovery during replay. Do not infer offline replay or draft safety from these tests. MF-17 covers trusted HTTPS setup on the emulator/phone.

## Verification record — 2026-11-10

All 53 portable tests passed using xUnit's in-process runner; the portable Release build passed with warnings treated as errors (zero warnings). The suite covers sign-in/restore, authoritative identity, invalid/mismatched envelopes, write/read/remove failures, expiry, shared refresh success/failure, cancellation, logout/write races, server changes, view-model input/loading/cancellation, and navigation policy. These tests use fakes for secure storage and native navigation. The normal VSTest/MSBuild parallel runner is restricted by this workspace's socket policy, so local builds use a single MSBuild node and tests run through the compiled xUnit executable.

Pinned NSwag generation/drift checks passed without changing the contract or generated files. The Android Debug build passed with warnings treated as errors (zero warnings); the standalone signed APK payload check passed for `arm64-v8a` and `x86_64`. Android autofill calls are guarded for API 26+ to preserve the app's Android 7 minimum.

An explicit temporary integration harness passed the actual `AuthSession` and `ApiAuthGateway` against the backend test host and a real isolated PostgreSQL 17.11 database: sign-in, refresh-only persistence, process-style restoration from the saved envelope, concurrent refresh, account continuity, logout, and subsequent signed-out startup. Only synthetic test credentials were used. This validates the real HTTP contract and session engine, not Android networking, UI or SecureStorage.

Actual native acceptance is pending. Both TCP and local-socket ADB daemon startup are denied in this workspace, and no USB device is exposed. Use the [device checklist](../tests/MuscleForge.Mobile.UiTests/README.md) with the built APK, recording the frontend/backend revisions, device/Android version, certificate setup, and observed results. MF-14 must remain open until the required native evidence exists.
