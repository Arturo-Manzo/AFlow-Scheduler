---
name: release-gates-tests-must-build
description: Release validation must build test assemblies before executing tests in Release configuration.
type: feedback
---
Rule: In release validation, do not run `dotnet test` with `--no-build` unless the test project was built in the same configuration first.

**Why:** The gate failed because only the main project was built in Release, so the test DLL did not exist under `AScheduler.Tests/bin/Release/net8.0`.

**How to apply:** In smoke and CI scripts, either remove `--no-build` for test runs or add an explicit Release build of the test project before running tests.
