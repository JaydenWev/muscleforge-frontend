# Android builds on Linux ARM64

The .NET 10 SDK runs on Linux ARM64, but its Android workload includes x64 host executables and native libraries. Installing the Android SDK alone leaves builds failing with `XA0111` or `Exec format error`. The upstream work is tracked in [dotnet/android#11184](https://github.com/dotnet/android/issues/11184).

## Configured development environment

This development machine has a user-local toolchain under `~/.local/share/muscleforge/android-arm64`, alongside Android SDK API 36 in `~/Android/Sdk` and Temurin JDK 21.0.13. Its environment is loaded by `~/.profile` and `~/.bashrc`. For an existing terminal:

```bash
source ~/.config/muscleforge/android-env.sh
dotnet build src/MuscleForge.Mobile/MuscleForge.Mobile.csproj -c Debug
dotnet build src/MuscleForge.Mobile/MuscleForge.Mobile.csproj -c Release -m:1
```

The environment exports `JAVA_HOME`, `ANDROID_HOME`, `Aapt2ToolPath`, `ZipAlignToolPath`, `AndroidBinUtilsDirectory`, and the native library search path. SDK and workload versions remain pinned by `global.json`.

| Component | Installed version/source |
| --- | --- |
| AAPT2 | 2.20-13193326, native ARM64 build |
| Mono.Unix and LibZipSharp native libraries | ARM64 builds from [DotnetAndroidArm64Shims 36.1.53](https://github.com/SuperJMN/DotnetAndroidArm64Shims/releases/tag/36.1.53) |
| LLVM and LLD | Debian ARM64 packages, 19.1.7 |
| GNU assemblers | Debian 2.44, ARM64 and x86-64 targets, both running on ARM64 |
| zipalign | Debian ARM64 package, 10.0.0+r36-1.1 |
| ADB | Debian ARM64 package, 34.0.5 |

The original AAPT2, Mono.Unix, and LibZipSharp binaries in workload pack `36.1.118` match the SHA-256 fingerprints for shim release `36.1.53` in its [compatibility manifest](https://github.com/SuperJMN/DotnetAndroidArm64Shims/blob/main/compatibility.json). The downloaded shim archive and Debian packages were checksum-verified before installation. The replacements live in the user tool directory and are selected through environment variables.

Release AOT compilation also invokes Android-prefixed assembler and linker names. The tool directory supplies `aarch64-linux-android-*` and `x86_64-linux-android-*` aliases backed by the native GNU assemblers and LLVM tools. These support both Android ABIs selected by the project.

When upgrading the workload, compare its host binary fingerprints with the shim compatibility manifest and update the user toolchain accordingly. The x64 Linux CI runner uses the tools distributed by the workload directly.
