# Repository Guidelines

## Project Structure & Module Organization

LimitBar is a .NET 8 Windows taskbar overlay app with a translucent hover card.
- `LimitBar.Desktop/`: Windows Forms taskbar overlay, WPF popup, overlay lifecycle, refresh scheduling, notifications, preferences, and optional startup.
- `LimitBar.Core/`: shared usage models, provider integrations, and refresh service. Keep Windows UI APIs out of this library.
- `limitbar-cli/`: console entry point for live usage diagnostics.
- `LimitBar.Checks/`: dependency-free offline provider and rendering checks.
- `LimitBar.Widget/` and `LimitBar.Host/`: retained Windows Widgets experiments; not dependencies of the desktop app.

`bin/`, `obj/`, `artifacts/`, and generated desktop previews are build output. Do not commit credentials or generated files.

## Build, Test, and Development Commands

Run from the repository root with the .NET 8 SDK:
- `dotnet run --project LimitBar.Desktop/LimitBar.Desktop.csproj`: launch the taskbar overlay and meter. `run-desktop.bat` does the same.
- `dotnet run --project limitbar-cli/limitbar-cli.csproj`: inspect live provider usage.
- `dotnet run --project LimitBar.Checks/LimitBar.Checks.csproj`: run offline provider checks.
- `dotnet run --project LimitBar.Desktop/LimitBar.Desktop.csproj -- --self-test`: check desktop rendering, popup rendering and reset semantics with synthetic data.
- `powershell -NoProfile -File scripts/verify.ps1`: build the desktop solution filter and run both checks.
- `powershell -NoProfile -File scripts/release.ps1`: build the self-contained installer, ZIP, and checksums.

Use `LimitBar.slnf` for normal development; the full solution includes unfinished legacy widget projects. Packaging lives in `packaging/`; screenshots and setup instructions live in `docs/`.

## Coding Style & Naming Conventions

Use four-space indentation, file-scoped namespaces, and braces on separate lines. Use PascalCase for types, methods, and properties; camelCase for parameters and locals; `_camelCase` for private fields. Prefer standard .NET facilities over new dependencies. Nullable reference types and implicit usings are enabled. No custom formatter is configured.

## Testing Guidelines

Run both check commands after relevant changes. Add focused assertions to the existing check executables; no test framework or coverage threshold is configured. Never treat an elapsed reset timestamp as confirmed replenishment. Missing windows are unavailable; failed readings must be labeled stale. Test shutdown, overlay click-to-open, hover dismissal, and multi-monitor positioning manually for UI changes. Live provider checks require sign-in.

## Commit & Pull Request Guidelines

Use concise imperative subjects, such as `Hide unavailable session limits`. Describe behavior, validation results, and known limitations; include screenshots with sample data for visual changes. Follow `CONTRIBUTING.md`. Keep generated installers, credentials, and project-local skills out of commits.
