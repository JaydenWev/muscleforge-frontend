# MuscleForge Frontend

The native Android app for MuscleForge, a personal workout tracker.

Planned stack: .NET MAUI 10, C# and XAML, MVVM, and SQLite.

## Status

Repository initialized. Application implementation has not started.

## Architecture

See [the architecture baseline](docs/architecture.md) for requirements, repository boundaries, and implementation milestones.

The frontend owns native interaction, local drafts, synchronization, and API consumption. It builds independently using a pinned backend OpenAPI contract and a generated C# client.

Companion repository: [muscleforge-backend](https://github.com/JaydenWev/muscleforge-backend).
