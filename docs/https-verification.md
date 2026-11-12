# MF-17 transport verification

Record date: 2026-11-12. MF-17 remains in progress until its emulator and phone acceptance checks have actual results. See the [setup guide](https-development.md) and [native checklist](../tests/MuscleForge.Mobile.UiTests/README.md).

## Build and host checks

The Android transport change is commit `a56dc18d9f9e3c69d3108657ca66b7729f3f9072`: the typed API client uses `AndroidMessageHandler`, without a custom certificate validator, cookies or automatic redirects.

- Debug Android build: passed with zero warnings/errors. Signed APK: `src/MuscleForge.Mobile/bin/Debug/net10.0-android36.0/local.muscleforge.app-Signed.apk`, SHA-256 `58dabcc18264401acdd0ad92d4b07aabda80072da59d0bd75033dca0037cb638`.
- Manager's compiled Debug APK policy check: passed. It checks the manifest's referenced resource, effective debuggable flag, cleartext setting, and complete compiled trust configuration.
- Release Android build: passed with normal AOT and size optimization; final packaging ran with warnings treated as errors and zero warnings/errors. Signed APK: `src/MuscleForge.Mobile/bin/Release/net10.0-android36.0/local.muscleforge.app-Signed.apk`, SHA-256 `09e496b5ce7f2661e0b2b98ac9135a27252d334571887190f02c5da1aa552cab`. The SDK container used the installed ARM64 build tools and their native dependencies; no Release optimization or TLS policy was disabled.
- Manager's compiled Release policy check: passed. The guard rejected the Debug APK presented as Release and a tampered APK with recompiled cleartext-permitting policy. The implementer also verified rejection of Release presented as Debug. CI now builds both configurations with warnings treated as errors and checks both compiled policies.
- A copy of the Release APK was signed with the existing development identity for an update over the installed Debug app, SHA-256 `c58afdb3c84807ed75cdb80a335478fcc038cab5223694fe720d7525fcf35d1e`. Its Release policy guard passed and its public signing-certificate digest matches Debug. This is a test artifact, not the final release identity/signing work in MF-32.
- Standalone managed payload checks passed for both Debug and Release, for `arm64-v8a` and `x86_64`.
- Portable regression suite: 78 passed, zero failed/skipped, using the existing Release test assembly. This is not an Android handler test.
- OpenSSL: the temporary leaf verifies against its private CA for the configured hostname and emulator IP SAN. Verification rejects an unrelated hostname and the chain without that CA.
- A temporary MF-12 API container serving HTTPS status returned `{"service":"MuscleForge","apiVersion":"v1"}` to curl with the issuing CA. Curl rejected both an untrusted issuer and a hostname mismatch (exit 60). No validation bypass was used.
- The documented Compose override passed `docker compose config --quiet` with synthetic configuration. That override was not deployed.

The isolated transport endpoint uses image `muscleforge-backend:mf12`, ID `sha256:0750e9dfcdcc6acb8bcea0617abedfb55b2b5412b4461fb2110ba9b80343441f`, built during MF-12 validation. MF-12 is merged in backend main `18fbebfc471313a009a27d1bf2f78e81152b5a65`; the image is identified independently of that merge. The endpoint has no owner bootstrap or usable database, uses ephemeral authentication keys, and establishes transport/status only. No personal deployment, database migration or authenticated catalog result is claimed.

The temporary test CA/leaf expire after 30/14 days and remain outside the repositories. Exact private network names, device connection addresses and local artifact locations are recorded with MF-17 rather than published here. No private keys or credentials are part of this record.

## Phone observations

Owner's OnePlus 13, model `CPH2653`, Android 16/API 36, connected through paired wireless ADB. The Debug APK above installed successfully over the existing app without clearing its data. Its native Sign in and Server settings pages opened.

| Case | Actual result |
| --- | --- |
| DNS for configured short hostname | Resolves to the expected LAN server; ping succeeds. |
| Debug before installing the new CA | Save & connect fails with the safe address/network/trusted-certificate message and Needs attention badge. |
| Debug with installed CA, matching host | Passed. After the owner installed the CA, Save & connect displayed Connected and “Connected to MuscleForge API v1.”; the API recorded status 200. |
| Debug with installed CA, hostname mismatch | Passed. A second reachable listener using a leaf issued by the installed CA but with an unrelated DNS SAN failed with Needs attention and the safe connection error. |
| HTTP address | Rejected locally with “Enter an HTTPS API address without credentials, a query, or a fragment.” |
| HTTPS redirect to HTTP | Passed. A matching trusted HTTPS test listener returned 302 toward an HTTP status endpoint. Settings showed failure, and the HTTP endpoint's request counter remained zero. |
| Restart/address retention and Not checked reset | Passed. Force-stop/cold launch retained the last valid HTTPS address and showed Not checked until another check. An invalid HTTP edit was not persisted. |
| Remove CA and retry after restart | Pending. |
| Release with only a user-installed CA | Passed. The Release device-test APK updated the existing app without clearing data, opened native Settings, retained the address, and rejected the same matching endpoint while the CA remained installed. Android package flags confirmed it was not debuggable. |
| Release with a matching system-trusted endpoint | Pending; no such test endpoint configured. |

The owner confirmed installation of the new development CA through Android Settings. The matching endpoint failed before installation and succeeded afterward. Accessibility labels are present in the UI hierarchy; TalkBack usability and keyboard checks remain pending until exercised.

After Release verification, the original Debug APK was restored over the same installation. Its debuggable flag was confirmed, and a cold launch followed by Save & connect succeeded again. The app was left on the working development address. Temporary hostname-mismatch and redirect test containers were removed; the separate status-only HTTPS development endpoint remains available for follow-up checks.

## Emulator observations

Pending. This workspace has no emulator executable or exposed KVM device; the owner confirmed that no emulator is available now. Physical-phone and host results do not substitute for emulator Settings/TLS checks. Use a separate emulator-capable host and the guide's `10.0.2.2` SAN/listener route, recording its exact image/API, APK hash, backend endpoint and each observed result. The alias targets the computer running that emulator; an API on another machine needs a reachable LAN/VPN address or an explicitly configured tunnel.
