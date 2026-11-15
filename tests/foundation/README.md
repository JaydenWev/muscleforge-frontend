# Isolated foundation server

This Compose project is for synthetic data only. It consumes an explicitly supplied backend image and has no source or project reference to another checkout. Build the image separately from the intended backend revision, for example from its root:

```bash
docker build --label org.opencontainers.image.revision=18fbebfc471313a009a27d1bf2f78e81152b5a65 -t muscleforge-api:mf16-synthetic .
```

Prepare a development PFX for the exact hostname used by the test runner and phone, and arrange CA trust on each client. Use a synthetic certificate and password. The backend's `docs/authentication.md` and frontend's `docs/https-development.md` describe certificate setup. Supply an absolute PFX path readable by the API container. Do not mount personal certificates or keys.

Set these shell environment variables to synthetic values: `MF_TEST_API_IMAGE`, `MF_TEST_DB_PASSWORD` (use letters and digits so it is safe in an Npgsql connection string), `MF_TEST_EMAIL`, `MF_TEST_PASSWORD`, `MF_TEST_PFX_PATH`, and `MF_TEST_PFX_PASSWORD`. The owner password must meet the backend's documented Identity policy. Optionally set `MF_TEST_DB_PORT`, `MF_TEST_HTTPS_PORT`, and `MF_TEST_BIND_ADDRESS`; defaults bind PostgreSQL and HTTPS to loopback on 15432 and 8443. To test a physical phone, choose a reachable bind address and restrict access to your test network. Avoid port 7443 if another server uses it.

From the frontend root, explicitly bypass any ambient Compose `.env` file:

```bash
docker compose --env-file /dev/null -p muscleforge-mf16-synthetic -f tests/foundation/compose.yaml up -d --wait database
```

From the independently checked-out backend root, apply migrations to this synthetic database before starting the API. The database port below assumes the default; adjust it if overridden. These commands require the pinned .NET SDK and restored EF tool:

```bash
dotnet tool restore
ConnectionStrings__Database="Host=127.0.0.1;Port=${MF_TEST_DB_PORT:-15432};Database=mf_test;Username=mf_test;Password=${MF_TEST_DB_PASSWORD}" dotnet ef database update --project src/MuscleForge.Infrastructure --startup-project src/MuscleForge.Api --configuration Release -- --environment Development
```

Return to the frontend root and start the API; it provisions the configured synthetic account and seeds exercises:

```bash
docker compose --env-file /dev/null -p muscleforge-mf16-synthetic -f tests/foundation/compose.yaml up -d api
```

Check `/health/ready` over the trusted HTTPS hostname and port. Set `MF_TEST_API_URL` to that HTTPS root and run the test project as described in `docs/foundation-verification.md`. API status alone does not prove migration, authentication, or catalog readiness. This development-only deployment does not persist Data Protection keys across container replacement and is not a production configuration.

When finished, remove only this named synthetic project and its disposable database volume:

```bash
docker compose --env-file /dev/null -p muscleforge-mf16-synthetic -f tests/foundation/compose.yaml down -v
```

Do not use this cleanup command on a project containing data you need. On 2026-11-13 this project was used with a synthetic PostgreSQL database and the pinned backend image for the real-API check recorded in `docs/foundation-verification.md`.
