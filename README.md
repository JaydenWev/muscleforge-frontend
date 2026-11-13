# MuscleForge Frontend

Native .NET MAUI Android app for MuscleForge, a personal workout tracker. Companion repository: [muscleforge-backend](https://github.com/JaydenWev/muscleforge-backend).

## Current increment

The app starts with native sign-in, restores a saved session, and opens Workout, Exercises, Templates, History, and Settings tabs after resolving the account from the API. Exercises supports search, paging, custom creation, editing and permanent archiving, with equipment and load guidance. Settings shows the account and offers sign-out. Server configuration remains available before sign-in. The app uses a forest-and-lime palette, Manrope typography, and persistent dark, light, or system appearance. Workout logging and template editing are still planned.

Access tokens stay in memory; refresh credentials use Android SecureStorage. The portable session service coordinates refresh and verifies server/account identity. See [native authentication and its verification limits](docs/native-auth.md), the [architecture baseline](docs/architecture.md), and the [foundation decision](docs/decisions/0001-initial-foundation.md). SQLite drafts and synchronization are future increments.

See [the exercise catalog workflow and acceptance record](docs/exercise-catalog.md). This app revision requires the MF-12 backend exercise endpoints and its explicitly applied catalog migration; `/api/v1/status` success alone does not prove those endpoints are deployed.

## Requirements

| Component | Pin |
| --- | --- |
| .NET SDK / workload set | 10.0.403 (`global.json`) |
| MAUI Controls | 10.0.120 |
| Java | Eclipse Temurin JDK 21.0.13 (CI) |
| Android platform / build tools | API 36 / 36.0.0 |
| Minimum Android | API 24, Android 7.0; confirm the owner's phone before release |
| Python | 3.11+ for the Android APK payload utility only |

Android is the only app target. `local.muscleforge.app` is a development application ID that makes no domain ownership claim. Finalize the ID and signing identity before the first signed personal release.

[MAUI 10 support ends May 11, 2027](https://dotnet.microsoft.com/en-us/platform/support/policy/maui), independently of .NET 10 LTS. [Android toolchain guidance](https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-10?view=net-maui-10.0) covers API 36 and JDK 21. Recheck support when upgrading.

## Build the platform-independent code

The portable projects need .NET and Bash; no Android workload, backend checkout or database:

```bash
dotnet tool restore
scripts/generate-api-client.sh --check
dotnet restore tests/MuscleForge.Mobile.UnitTests/MuscleForge.Mobile.UnitTests.csproj
dotnet test tests/MuscleForge.Mobile.UnitTests/MuscleForge.Mobile.UnitTests.csproj -c Release --no-restore
```

`MuscleForge.Frontend.Core.slnf` loads the portable projects in an IDE. The full `MuscleForge.Frontend.sln` includes Android and needs its workload.

## Build and install Android

Install JDK 21.0.13 and Android command-line tools, set `JAVA_HOME` and `ANDROID_HOME`, then run:

```bash
sdkmanager 'platforms;android-36' 'build-tools;36.0.0' 'platform-tools'
dotnet workload install maui-android
dotnet restore src/MuscleForge.Mobile/MuscleForge.Mobile.csproj
dotnet build src/MuscleForge.Mobile/MuscleForge.Mobile.csproj -c Debug
python3 scripts/check-apk-payload.py src/MuscleForge.Mobile/bin/Debug/net10.0-android36.0/local.muscleforge.app-Signed.apk
```

The SDK/workload set are pinned by `global.json`. Build output is under `src/MuscleForge.Mobile/bin/Debug/`. Debug APKs embed their managed assemblies, so install a `*-Signed.apk` directly using `adb install -r /path/to/your-Signed.apk`. CI publishes `muscleforge-android-debug` APK artifacts.

Linux ARM64 hosts also need native replacements for the workload's x64 build tools. See the [ARM64 development toolchain](docs/android-arm64.md) for the configured environment and tool versions.

Debug builds use a development signing identity. Release signing, key backups, upgrade compatibility, and the signed personal APK will accompany the first usable release. No AAB or store account is required now.

## Connect the API

In Settings, enter the HTTPS server root (or proxy prefix), without `/api/v1`, credentials, query parameters, or fragments. Select **Save & connect**. Success confirms API reachability/version; it does not certify database or workout availability. A saved address starts as **Not checked** on a fresh launch; editing it clears the previous success state. Keep Tailscale connected on your phone when using a Tailscale server address.

Development and native acceptance use physical Android phones. A phone needs a reachable LAN/VPN hostname or IP; `localhost` refers to the phone itself. The server certificate must include the exact hostname/IP used. Emulator setup and verification are outside the project's scope.

Only HTTPS is accepted. Debug builds trust a user-installed development CA alongside system CAs. Release builds trust system CAs only. Configure a trusted certificate and device trust; certificate and hostname validation stay enabled.

Follow the [HTTPS development guide](docs/https-development.md) for certificate issuance, SDK/Docker/Visual Studio endpoints, phone trust, and wireless installation. The Android API client explicitly uses the native Android HTTP handler so the packaged network security configuration governs trust. Cookies and automatic redirects are disabled; configure the final HTTPS API address. See the [verification record](docs/https-verification.md) for actual checks and remaining manual observations.

After building, check each APK's compiled manifest and trust configuration with the Android SDK's `aapt2`:

```bash
scripts/check-android-network-policy.sh src/MuscleForge.Mobile/bin/Debug/net10.0-android36.0/local.muscleforge.app-Signed.apk Debug
scripts/check-android-network-policy.sh src/MuscleForge.Mobile/bin/Release/net10.0-android36.0/local.muscleforge.app-Signed.apk Release
```

Build the Release APK with `dotnet build src/MuscleForge.Mobile/MuscleForge.Mobile.csproj -c Release` before its check. The guard uses build-tools 36.0.0 under `ANDROID_HOME`, or an explicit `AAPT2` executable. Packaging checks do not establish runtime TLS behavior.

The API URL and appearance choice use Preferences. Credentials use SecureStorage; passwords are cleared from the sign-in view model after each attempt. Android backup is disabled. Sign out before changing the server URL. Sign-out removes local credentials; it does not revoke tokens on the server. No public registration is available: sign in with the account provisioned by the backend's owner environment settings.

## Contract and generated client

`contracts/openapi.json` is pinned from the backend. `contracts/source.json` records its backend commit, version, SHA-256, and generator version. Builds/generation never fetch a server or another repository.

```bash
scripts/generate-api-client.sh
scripts/generate-api-client.sh --check
```

NSwag.ConsoleCore 14.7.1 is pinned in the local .NET tool manifest. `nswag.json` and `nswag-fixture.json` generate the C# client/DTOs and test fixture; the shell wrapper checks the contract hash and generated-file drift. A small application facade handles per-request bearer headers and safe errors. Never hand-edit generated files. See [the contract workflow](docs/api-contract.md).

Update the snapshot deliberately from a specific backend commit, update its provenance/hash, regenerate, and review the diff. CI publishes the exact pinned contract and generated client under the frontend revision. MF-15 pins the MF-12 exercise contract and regenerates with the existing NSwag tool; authentication operations remain compatible.

## Checks and remaining setup

CI checks client drift, runs portable tests, builds Android separately, verifies that managed code is packaged for every APK ABI, and publishes signed debug APKs and resolved lockfiles. Tests cover HTTP behavior, session restoration, coordinated refresh, storage/cancellation failures, and navigation decisions. [Device smoke checks](tests/MuscleForge.Mobile.UiTests/README.md) are manual until Appium coverage is added. Portable navigation tests do not establish actual Android back-stack or SecureStorage behavior.

The first restore generates `packages.lock.json` files; commit them from your restore or CI artifacts. Existing lockfiles are enforced during CI restore.

The visual refresh has passed generated-client consistency, all 11 portable tests in Debug and Release, and full Debug and Release solution builds in the Linux ARM64 workspace. Earlier APK startup and Settings behavior were verified on a OnePlus 13 running Android 16; the refreshed UI still needs the [manual device smoke checks](tests/MuscleForge.Mobile.UiTests/README.md).

Manrope fonts are bundled under the [SIL Open Font License](src/MuscleForge.Mobile/Resources/Fonts/Manrope-OFL.txt); the license is also included in the APK assets.

MF-13 tooling uses pinned NSwag generation and a thin shell wrapper. The facade uses generated DTOs directly. Its 23-test baseline, isolated generation/build, Android packaging, and real API/PostgreSQL contract checks passed before MF-14. MF-14 adds the session and native sign-in implementation; current verification and outstanding device acceptance are recorded in [native authentication](docs/native-auth.md).
