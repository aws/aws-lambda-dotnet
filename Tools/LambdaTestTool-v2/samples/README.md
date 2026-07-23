# Lambda Test Tool v2 — Sample Projects

Runnable starter functions for the [AWS Lambda Test Tool](../README.md). Each is a minimal, self-contained project with its own README and a ready-to-use launch profile.

| Sample | What it shows | Emulator setup |
|--------|---------------|----------------|
| [`AddFunctionTopLevel`](AddFunctionTopLevel) | Top-level-statements function behind the API Gateway emulator (the [Quick Start](../README.md#quick-start)). | Lambda + API Gateway (`HttpV2`) |
| [`AddFunctionClassLibrary`](AddFunctionClassLibrary) | A class-library function with a pre-filled `Executable` launch profile. | Lambda + API Gateway (`HttpV2`) |
| [`SQSProcessor`](SQSProcessor) | An `SQSEvent` handler, testable via the SQS event source or the built-in `sqs.json` sample event. | Lambda (+ optional SQS event source) |
| [`ToUpperFunction`](ToUpperFunction) | A minimal, zero-dependency function for exploring the web UI and sample events. | Lambda only |

## Prerequisites

- .NET 10 SDK (the samples target `net10.0`). To build them with an older SDK, change `<TargetFramework>` to `net8.0` or `net9.0`.
- The test tool installed: `dotnet tool install -g amazon.lambda.testtool` (see the [main README](../README.md#prerequisites)).

Start with [`AddFunctionTopLevel`](AddFunctionTopLevel) if you're new — it's the lowest-friction path from install to a working invocation.
