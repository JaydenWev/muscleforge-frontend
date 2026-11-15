# Foundation verification

Baseline: frontend `e3241c2adec6d755422758dc1f5b907a4079be1e`, backend `18fbebfc471313a009a27d1bf2f78e81152b5a65`. On 2026-11-13 SDK 10.0.403 on Linux ARM64 built the portable tests in Release with warnings as errors and passed all 78 tests, zero skips. The separate real-API project built with zero warnings/errors. Its first controlled-server run reached authentication, refresh, creation and search, but failed when the test reused a create-only editor for an edit. After correcting the test to open an existing-exercise editor, the rerun passed 1/1 with zero skips against the pinned API and real PostgreSQL. The runner used normal CA trust and the server returned healthy readiness. The test exercised sign-in, restoration, coordinated refresh, creation, editing, search, archive and sign-out. Its report is `TestResults/mf16-real-api.trx` in the verification workspace.

```bash
dotnet build tests/MuscleForge.Mobile.UnitTests/MuscleForge.Mobile.UnitTests.csproj -c Release -warnaserror -m:1 -p:UseSharedCompilation=false
dotnet tests/MuscleForge.Mobile.UnitTests/bin/Release/net10.0/MuscleForge.Mobile.UnitTests.dll -trx /tmp/mf16-mobile.trx
dotnet build tests/MuscleForge.RealApiTests/MuscleForge.RealApiTests.csproj -c Release -warnaserror -m:1 -p:UseSharedCompilation=false
```

The real-API project references only frontend projects. Against a fresh, migrated, seeded, isolated PostgreSQL-backed HTTPS API, set `MF_TEST_API_URL` to its trusted HTTPS root and `MF_TEST_EMAIL` and `MF_TEST_PASSWORD` to its synthetic owner credentials, then run:

```bash
dotnet tests/MuscleForge.RealApiTests/bin/Release/net10.0/MuscleForge.RealApiTests.dll -trx /tmp/mf16-real-api.trx
```

The test signs in, restores and refreshes a session, creates/edits/searches/archives an exercise and signs out. It mutates that account and requires normal certificate validation; use synthetic data only. Setup and migration are independent backend operations, not build dependencies. The in-memory credential store tests portable orchestration, not Android SecureStorage.

On 2026-11-15, [hosted frontend CI run 43729568715](https://github.com/JaydenWev/muscleforge-frontend/actions/runs/43729568715) passed the core and Android Debug/Release jobs. The pinned backend's complete local PostgreSQL integration run passed all 117 tests with zero failures/skips and a captured exit code of 0; its [hosted backend CI run](https://github.com/JaydenWev/muscleforge-backend/actions/runs/43729569269) also passed. These results supersede the earlier incomplete backend-suite checkpoint without claiming native acceptance.

## Physical-phone acceptance — 2026-11-15

On a OnePlus 13 (CPH2653, Android 16/API 36), the development-signed Debug APK from frontend `e3241c2adec6d755422758dc1f5b907a4079be1e` (SHA-256 `58dabcc18264401acdd0ad92d4b07aabda80072da59d0bd75033dca0037cb638`) was exercised against the isolated HTTPS API at `jadyn-server:7443`, running backend `18fbebfc471313a009a27d1bf2f78e81152b5a65` (`mf16-foundation` image) and its existing PostgreSQL database. The phone trusted the installed development user CA; the app's normal Android HTTP handler and certificate validation were used. A synthetic owner account was used.

Observed results: the trusted endpoint connected; synthetic-owner sign-in opened the protected Shell; the catalog showed seeded Barbell Bench Press with total-load guidance; creating `MF16PhoneNov15` with Dumbbell / PerDumbbell succeeded; exact search returned the created exercise with one-dumbbell guidance; editing persisted `MF16PhoneNov15Editedv`; archive confirmation removed it from default results and Show archived displayed it as ARCHIVED. Force-stop and relaunch restored the signed-in protected Shell using the real Android SecureStorage-backed credential.

Sign-out returned to native Sign in. Android Back followed by reopening showed a transient restoration state; a subsequent force-stop/cold start settled on Sign in without restoring the protected Shell. This verifies the observed sign-out and credential clearing across cold restart; exhaustive Back-stack behavior was not established. No emulator work was performed. Injected SecureStorage failure cases remain covered only by portable tests and were not exercised on the phone. The PostgreSQL/Testcontainers and automated results above remain separate evidence; native behavior is not inferred from them.
