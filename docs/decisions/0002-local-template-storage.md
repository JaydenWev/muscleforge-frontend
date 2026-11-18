# 0002 — Local templates before server synchronization

Date: 2026-11-18
Status: Accepted by the owner; implemented as the MF-20 storage foundation

The initial plan required server template endpoints before Android template editing. The owner changed that priority: templates are locally owned SQLite data and should work without those endpoints. Server synchronization, backup and restore are deferred to MF-96 after the app is further developed. MF-21 provides native local editing; MF-25 later extends the same populated schema for workout drafts.

Use immutable Mobile.Core models/interfaces and a portable Mobile.Storage adapter assembly. Mobile owns the app-private path and dependency registration. Keeping the production adapter portable lets tests exercise the same native SQLite implementation without a MAUI runtime, linked-source copies or a sibling backend checkout. Core remains independent of SQLite/MAUI, and the two-repository boundary is unchanged.

Stable local IDs/revisions, account/server partitioning, atomic writes and lossless canonical weight storage are required now. No speculative template sync protocol/outbox is introduced. Retain isolated templates across logout; deletion is a separate explicit action. A cached local workspace does not authorize server calls.

Workouts still start online from a validated snapshot of the phone-local template, with authoritative session saves/history on the server. A local template UUID must not become a required server-template foreign key. MF-22 owns the necessary additive session-provenance adaptation; the completed MF-19 migration is not rewritten.

See [the storage contract and evidence](../local-template-storage.md). Earlier architecture sections describing template APIs as an initial release gate or deferring offline template editing are historical and superseded by this decision. Server database backups remain a separate operations concern.
