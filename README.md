# MuscleForge

A new Android workout-tracking app based on the mockups in Google Drive at `/projects/muscle-forge/mockups`. This branch is a clean starting point for the new app and does not include the former application or backend integration.

The current implementation is an early prototype: it supports creating workouts, selecting exercises, logging sets, a rest timer, and basic local history and progress. Data is stored on the device and may be lost if app data is cleared or the app is uninstalled. The visual design and features in the mockups are not yet complete; social functionality is not connected to a service.

## Build

Install .NET SDK 10.0.403, the matching `maui-android` workload, JDK 21, and Android SDK API 36/build tools 36.0.0. Then run:

```bash
dotnet restore src/MuscleForge.Mobile/MuscleForge.Mobile.csproj
dotnet build src/MuscleForge.Mobile/MuscleForge.Mobile.csproj -c Debug
```

The signed debug APK is produced under `src/MuscleForge.Mobile/bin/Debug/net10.0-android36.0/`. Linux ARM64 hosts may require native Android build-tool replacements; use an appropriately configured Android toolchain.

Manrope fonts are bundled under the [SIL Open Font License](src/MuscleForge.Mobile/Resources/Fonts/Manrope-OFL.txt).
