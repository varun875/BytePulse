# Contributing to BytePulse

Thanks for taking the time to help. This file covers how to build the project,
what the code expects of you, and how to get a change merged.

## Before you start

- For anything larger than a bug fix, please open an issue first so we can agree
  on the approach. It is much easier to discuss a design than to rewrite a pull
  request.
- Security problems should not be reported in a public issue. See
  [SECURITY.md](SECURITY.md).

## Building and testing

```powershell
dotnet build BytePulse.slnx -c Release
dotnet test  BytePulse.slnx -c Release
dotnet run --project src/BytePulse.App
```

The build treats warnings as errors and nullable reference types are enabled, so
a change that compiles locally will normally pass continuous integration.

## What the code expects

- **The engine has no third-party dependencies.** `BytePulse.Core` uses only the
  base class library and Win32. Please keep it that way; it keeps the engine
  portable and the licence unambiguous.
- **The engine does not reference the UI.** If the window needs something, put it
  in the engine as a plain API.
- **Public types are documented.** A one line summary is enough; the point is that
  the public surface is readable without opening the implementation.
- **Tests come with the change.** The engine is straightforward to test; a new
  scanner behaviour should have a test that would fail without it.
- **No per-entry allocation on the scan path.** Allocating for each file is the
  thing this project exists to avoid. If you need to add a field, consider a column.

## Style

The repository has an `.editorconfig`; formatting is enforced by it. Beyond that:

- Prefer clarity over cleverness, except in the storage layer where the whole
  point is the layout.
- Comments explain *why*, not *what*. If a reader can see what the code does, the
  comment should say why it is done that way.
- Keep unrelated changes out of a pull request.

## Icons

The images in `src/BytePulse.App/Assets` are generated from one drawing. Change the
drawing in `scripts/make-icons.ps1` and run it rather than editing the PNGs by hand, so
every size stays consistent:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\make-icons.ps1
```

## Benchmarks

If your change touches scanning, run the benchmarks before and after and include
both numbers:

```powershell
dotnet run -c Release --project tests/BytePulse.Benchmarks
```

They write a report to `BenchmarkDotNet.Artifacts`, which is not committed.

## Commit messages

A short summary line, then a paragraph explaining what changed and why, then how
it was verified. Please keep each commit to one idea.
