# AGENTS.md

Guidance for AI coding agents working in **FluidCaching**, a multi-threaded Least Recently Used (LRU) cache
for .NET with async/await support. For the user-facing overview see [`README.md`](./README.md).

## Layout

- `FluidCaching/` — the library, multi-targeted `net10.0;net47` (the `FluidCaching` NuGet package).
- `FluidCaching.Sources/` — packs the C# files of `FluidCaching/` as a source-only NuGet package
  (`FluidCaching.Sources`). It does not compile anything itself.
- `FluidCaching.Specs/` — xUnit test suite (`net10.0` + `net472`).
- `FluidCaching.ApiVerificationTests/` — public API approval tests (`PublicApiGenerator` + Verify).
- `Samples/ExampleApp/` — a small console app that uses the cache.
- `Build/` — Nuke (Fallout) build automation.

## Build & test

```bash
./build.ps1        # or ./build.sh — the build used by CI (build, test, inspect, API checks, package scan, pack)
dotnet build FluidCaching.sln -c Debug
dotnet test FluidCaching.Specs -c Debug
```

- `TreatWarningsAsErrors` is on. The analyzers run **only** on `net10.0`, so fix issues there first.
- Tests run in Debug so FluentAssertions can report variable names. Keep coverage non-decreasing.

## Conventions

- C# style: follow the
  [csharp-guidelines skill](https://github.com/dennisdoomen/CSharpGuidelines/tree/main/Skills/csharp-guidelines);
  `.editorconfig` is authoritative (4-space indent, 130-col lines, braces on new lines).
- The files in `FluidCaching/` are also shipped as source code. Keep them compatible with **C# 10**:
  no collection expressions, no `System.Threading.Lock`, no primary constructors, no `field` keyword.
  Every file starts with `#nullable enable`. Public types use the `#if PUBLIC_FLUID_CACHING` pattern so they
  become `internal` in the source-only package.
- Keep the public API working on both `net47` and `net10.0`; XML-doc public members.
- Avoid new dependencies; never commit secrets. Add any new analyzer packages to `Directory.Build.props`
  conditioned on `net10.0` with `<PrivateAssets>all</PrivateAssets>`.

### Tests

- xUnit, Arrange-Act-Assert, scenarios grouped in nested classes (see `FluidCaching.Specs/FluidCacheSpecs.cs`),
  FluentAssertions for assertions.
- Tests should not use terms like "should" or "when". Instead, they should use a fact-based naming convention in
  snake casing.
- Test method names must describe observable behavior and must never start with or include the name of the method
  under test or any other code element.

## Public API changes

When the approval tests in `FluidCaching.ApiVerificationTests/` fail for an intended change, run
`AcceptApiChanges.ps1` / `AcceptApiChanges.sh` (or Rider's Verify Support) and commit the updated `ApprovedApi/` files.
