# AccSaber Test Suite

## Overview

`AccSaber.Tests` is an xUnit test project targeting .NET Framework 4.8. It references the plugin and Beat Saber/Unity assemblies.

The suite currently contains **17 tests** across four test classes.

## Test coverage

### `APIHandlerTests` (8 tests)

These tests exercise `APIHandler` through an injected in-memory `HttpMessageHandler`. They cover the shared client's timeout property, string/JSON/byte response handling, request method and URI forwarding, no retry for client errors, retry and recovery after a server error, cancellation before a request is sent, and concurrent domain health checks. The concurrency test verifies one shared health ping under contention and successful results across domains. The collection disables parallelization because the handler and health-cache state are static.

### `ModelInterfaceTests` (2 tests)

These are source-analysis checks over C# files under `AccSaber/Models`; model types are not loaded at runtime. They check that discovered model classes implement `IModel` directly or through their inheritance/interface hierarchy, and that concrete classes have `[UsedImplicitly]`. Comments and string/character literals are stripped before scanning. This lightweight scanner handles the declarations needed by the current models but is not a full C# parser or compiler-semantic analysis.

### `AccSaberStoreTests` (4 tests)

These verify that assigning `CurrentRankedMap` raises its update event, and that the score WebSocket listener connects to the configured endpoint, parses and dispatches text score messages, reassembles fragmented text frames, and propagates cancellation while disposing the socket. The listener tests use a scripted in-memory socket and do not connect to a live service.

### `ScoreSubmissionTests` (2 tests)

These call `ScoreCounter.InitializeInternal` with test substitutes to check eligibility flow: an unranked map outside a campaign stops before subscribing, while an unranked campaign map passes the eligibility gate and requests gameplay event subscription. Unity-dependent behavior is overridden by the test subclass. These tests do not exercise submission over HTTP or the full gameplay flow.

## Running the tests

From the repository root, run:

```powershell
dotnet test AccSaber.Tests\AccSaber.Tests.csproj
```

To run one class, add a filter such as:

```powershell
dotnet test AccSaber.Tests\AccSaber.Tests.csproj --filter "FullyQualifiedName~APIHandlerTests"
```

Replace `APIHandlerTests` with `ModelInterfaceTests`, `AccSaberStoreTests`, or `ScoreSubmissionTests` to select another class.

## Requirements and limitations

- The project targets .NET Framework 4.8 and needs a compatible test runtime.
- The plugin's Beat Saber/Unity references must be available through the configured reference directory or `BeatSaberDir` setting.
- Model checks are textual source scans, so complex syntax may not be interpreted like a Roslyn compiler analysis.
- HTTP and WebSocket tests use in-memory substitutes; they do not validate live network or service behavior.
- Score submission coverage currently focuses on initialization eligibility and does not cover the end-to-end submission path.
