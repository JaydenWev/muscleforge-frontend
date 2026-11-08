# API contract and generated client

The backend owns `contracts/openapi.json`. This repository deliberately stores its own reviewed copy, together with `contracts/source.json`: backend repository, full commit, document version, API path version, exact SHA-256 and generator version. Portable builds require only this checkout, .NET and Python; they never fetch a server, sibling checkout or moving latest contract.

## Generate and verify

```bash
python3 -m unittest discover -s tests/contract_tooling -v
python3 scripts/generate-api-client.py
python3 scripts/generate-api-client.py --check
dotnet test tests/MuscleForge.Mobile.UnitTests/MuscleForge.Mobile.UnitTests.csproj -c Release
```

Generator v2 is the committed, dependency-free Python tool. Generation checks the pinned hash and metadata; `--check` additionally compares the generated C# bytes. The fixture client used by the portable tests is also generated and checked from its committed fixture. Never edit generated C# directly.

The supported subset covers operation-level bearer authentication, required JSON request bodies, JSON `200` responses, the shared API problem schemas, scalar path/query parameters, strings, UUIDs, timestamps, integers, numeric formats, booleans, string enums, arrays, nested objects and typed maps. Numeric string/int64 unions support the framework's `expiresIn`. Optional nonnullable properties are omitted when unset; optional nullable properties may carry explicit null. Unsupported unions, complex parameter serialization, other success statuses or unrecognized metadata fail generation and need an explicit implementation/test change. This is a bounded generator, not a general OpenAPI implementation or runtime request validator.

The client keeps the existing `GetApiStatusAsync(Uri, CancellationToken)` method and adds login, refresh and current-account calls. Pass a normalized base URI ending in `/`; a configured path prefix is retained. Authenticated calls take a bearer token and set it on the individual request. The client owns no credentials, storage, refresh coordination or sign-in UI; these belong to MF-14. Keep HTTPS validation in the application and platform networking layers.

Failures throw `ApiException`, derived from `HttpRequestException`, with HTTP status, a typed `ApiProblem` when parseable, and `RetryAfter`. The exception message contains only status. Responses are streamed; problem parsing is bounded to 64 KiB and respects cancellation. Malformed, oversized or non-JSON error bodies do not become exception messages. Callers should map stable problem codes to appropriate user messages and avoid logging credentials or untrusted problem text.

## Coordinated update across two repositories

1. Prepare the backend PR with endpoint/DTO changes, regenerate its snapshot, run the exporter drift and compatibility checks, and review changes against its committed baseline. Do not overwrite the baseline merely to silence a failure.
2. Select an exact backend commit or its workflow contract artifact. Verify its manifest and snapshot hash. Copy the reviewed snapshot deliberately into this repository, update every corresponding provenance field, regenerate, and review/test the client diff in a separate frontend PR. Record both PRs and commit revisions in the handoff.
3. For additive APIs, deploy the compatible backend before distributing an app that calls the new API. Preserve existing paths and response guarantees for installed clients. A breaking compatibility result needs an explicit version/migration decision, not an automatic baseline refresh.

The route version (`/api/v1`) identifies the HTTP API boundary. OpenAPI `info.version` currently identifies the development document (`0.1.0-dev`); repository commit/release revisions identify exact server or client builds. These values are distinct. MF-47 owns supported released-version matrices and release policy; no production release support promise is introduced here.

Backend CI publishes the checked snapshot with a manifest carrying API/document/OpenAPI versions, source revision and hash in an artifact named for that revision. Frontend CI runs generator tests, drift checks and portable tests, and uploads the exact pinned snapshot, provenance, generator and generated production client under the frontend source revision. Workflow artifacts are review/build evidence; they are not a deployment or a moving latest-contract source. Android sign-in and actual device credential-storage checks remain MF-14.
