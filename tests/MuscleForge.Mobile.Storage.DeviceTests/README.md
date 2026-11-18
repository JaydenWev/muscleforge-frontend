# Android storage probe

This optional, separate application exercises the production SQLite adapter with synthetic data in its own private app directory. It does not access the MuscleForge app database, credentials, or network. Use a physical Android device; the probe is not included in the solution or CI.

With the repository's pinned .NET SDK, Android workload, JDK and Android SDK configured, restore and build an arm64 Release APK. The probe enables full trimming and AOT; it does not disable either to make SQLite pass:

```bash
dotnet restore tests/MuscleForge.Mobile.Storage.DeviceTests/MuscleForge.Mobile.Storage.DeviceTests.csproj
dotnet build tests/MuscleForge.Mobile.Storage.DeviceTests/MuscleForge.Mobile.Storage.DeviceTests.csproj -c Release --no-restore -warnaserror
```

Install `bin/Release/net10.0-android36.0/android-arm64/local.muscleforge.storagechecks-Signed.apk` from this project directory on the chosen arm64 device. For example, with the appropriate device serial and signed APK path:

```bash
adb -s "$DEVICE_SERIAL" install -r "$SIGNED_APK"
adb -s "$DEVICE_SERIAL" shell am start -n local.muscleforge.storagechecks/local.muscleforge.storagechecks.MainActivity --es phase seed
```

Confirm `MF20 seed PASS` on the device or in the probe-only `MF20StorageChecks` logcat tag. Seed refuses to overwrite an existing probe database. Stop the probe process, then start verification in a fresh process:

```bash
adb -s "$DEVICE_SERIAL" shell am force-stop local.muscleforge.storagechecks
adb -s "$DEVICE_SERIAL" shell am start -n local.muscleforge.storagechecks/local.muscleforge.storagechecks.MainActivity --es phase verify
```

Confirm `MF20 verify PASS`. Failure displays only the exception type. Verification modifies and archives the synthetic graph, so a repeat requires uninstalling this separate probe package and reinstalling it before seed:

```bash
adb -s "$DEVICE_SERIAL" uninstall local.muscleforge.storagechecks
```

The probe checks native SQLite persistence, scoping, precision, conflict handling and archival in this Android consumer. It does not establish the behavior of the future template screens.
