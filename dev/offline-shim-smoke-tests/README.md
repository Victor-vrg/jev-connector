# Offline shim smoke tests (dev-only, not part of the deliverable)

This folder exists for one reason: the sandbox this connector was originally written in has no
network access to `nuget.org` (only npm/pip/GitHub-class registries are reachable there), so the
real `OutSystems.ExternalLibraries.SDK` and `xunit` packages referenced by `src/` and `tests/`
could not be restored to prove the code compiles and behaves correctly.

To verify the logic anyway, this folder reproduces just the public attribute/interface surface
those packages expose — `OSInterfaceAttribute`, `OSActionAttribute`, `ILogger`, etc. — as local,
dependency-free stand-ins, and runs the same scenarios as `tests/` (request shape, response
mapping, retry/backoff, 401/422/429/529 handling) as a small executable in `SmokeTests/`.

**You do not need this to build or use the connector.** On a normal machine with internet access,
`src/` and `tests/` restore against the real NuGet packages exactly as any other .NET project
would — just run `scripts/build-and-package.sh` from the repo root. Keep this folder only if you
want to re-verify the logic offline; otherwise it's safe to delete before publishing to Forge.

To run it: `dotnet run --project SmokeTests`.
