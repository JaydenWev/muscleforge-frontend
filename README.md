# MuscleForge Frontend

Native .NET MAUI Android app for MuscleForge, a personal workout tracker. Companion repository: [muscleforge-backend](https://github.com/JaydenWev/muscleforge-backend).

## Current increment

The initial shell has Workout, Templates, History, and Settings tabs. Settings persists an HTTPS API base URL and checks `/api/v1/status` through a generated C# client. The view model, endpoint validation, and HTTP adapter are independent of MAUI.

Workout logging, authentication, SQLite drafts, and synchronization are future increments. See the [architecture baseline](docs/architecture.md) and [foundation decision](docs/decisions/0001-initial-foundation.md).

## Requirements

| Component | Pin |
| --- | --- |
| .NET SDK / workload set | 10.0.403 (`global.json`) |
| MAUI Controls | 10.0.120 |
| Java | Eclipse Temurin JDK 21.0.13 (CI) |
| Android platform / build tools | API 36 / 36.0.0 |
| Minimum Android | API 24, Android 7.0; confirm the owner's phone before release |
| Python | 3.11+; CI uses 3.12 |

Android is the only app target. `local.muscleforge.app` is a development application ID that makes no domain ownership claim. Finalize the ID and signing identity before the first signed personal release.

[MAUI 10 support ends May 11, 2027](https://dotnet.microsoft.com/en-us/platform/support/policy/maui), independently of .NET 10 LTS. [Android toolchain guidance](https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-10?view=net-maui-10.0) covers API 36 and JDK 21. Recheck support when upgrading.

## Build the platform-independent code

Only .NET and Python are needed; no Android workload, backend checkout, or database:

```bash
python3 scripts/generate-api-client.py --check
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
```

The SDK/workload set are pinned by `global.json`. Build output is under `src/MuscleForge.Mobile/bin/Debug/`. Install a debug `*-Signed.apk` using `adb install -r /path/to/your-Signed.apk`. CI publishes `muscleforge-android-debug` APK artifacts.

Debug builds use a development signing identity. Release signing, key backups, upgrade compatibility, and the signed personal APK will accompany the first usable release. No AAB or store account is required now.

## Connect the API

In Settings, enter the HTTPS server root (or proxy prefix), without `/api/v1`, credentials, query parameters, or fragments. Select **Save and check connection**. Success confirms API reachability/version; it does not certify database or workout availability.

An Android emulator reaches the host through `10.0.2.2`; a phone needs a reachable LAN/VPN hostname. `localhost` refers to the device itself. The server certificate must include the hostname/IP used. A localhost-only certificate will not validate for the emulator alias.

Only HTTPS is accepted. Debug builds trust a user-installed development CA alongside system CAs. Release builds trust system CAs only. Configure a trusted certificate and device trust; certificate and hostname validation stay enabled.

Only the API URL is stored in Preferences. Credentials will use SecureStorage when authentication is implemented. Android backup is disabled from the initial scaffold.

## Contract and generated client

`contracts/openapi.json` is pinned from the backend. `contracts/source.json` records its backend commit, version, SHA-256, and generator version. Builds/generation never fetch a server or another repository.

```bash
python3 scripts/generate-api-client.py
python3 scripts/generate-api-client.py --check
```

Generator v1 is committed in `scripts/generate-api-client.py`, has no third-party dependencies, and supports the initial public status operation and required string DTOs. Unsupported schemas fail explicitly. Add request/auth/error support or migrate to a pinned general-purpose generator with the next slice. Never hand-edit generated files.

Update the snapshot deliberately from a specific backend commit, update its provenance/hash, regenerate, and review the diff.

## Checks and remaining setup

CI checks client drift, runs portable tests, builds Android separately, and publishes debug APKs and resolved lockfiles. Tests cover HTTPS validation, path prefixes, invalid input, unavailable servers/retry, and incompatible API versions. [Device smoke checks](tests/MuscleForge.Mobile.UiTests/README.md) are manual until Appium coverage is added with the first authenticated flow.

The first restore generates `packages.lock.json` files; commit them from your restore or CI artifacts. Existing lockfiles are enforced during CI restore. The authoring workspace lacks .NET/Android tools, NuGet access, and Docker daemon access. Builds, .NET tests, lockfile resolution, and device behavior have not been verified there.
