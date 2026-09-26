# Contributing to LimitBar

Small, focused contributions are welcome. Use Windows 11 and the .NET 8 SDK. Fork and clone the repository, then:

```powershell
dotnet build LimitBar.slnf
dotnet run --project LimitBar.Desktop
powershell -NoProfile -File scripts/verify.ps1
```

The checks use synthetic responses and do not need provider credentials. Running the app normally does query signed-in providers.

## Where things live

- `LimitBar.Desktop/`: Windows Forms taskbar overlay, WPF hover card, scheduling, and settings.
- `LimitBar.Core/`: provider integrations, models, and refresh orchestration.
- `LimitBar.Checks/`: standalone offline checks.
- `limitbar-cli/`: live console diagnostics.
- `scripts/`, `packaging/`, `.github/workflows/`: checks, release packaging, and CI.
- `docs/`: Windows instructions and real-control screenshots.
- `LimitBar.Host/`, `LimitBar.Widget/`: legacy experiments, excluded from the desktop solution filter.

Keep provider logic out of UI code and Windows APIs out of Core.

## Make a change

Use four-space indentation, PascalCase public names, and `_camelCase` fields. Follow `.editorconfig`. Prefer framework features over extra dependencies. Fix the cause of a bug and add a focused assertion to the relevant existing check executable.

For UI changes, include before/after screenshots and check 100%, 150%, and 200% scaling where available, hover dismissal, pointer movement into the popup, taskbar dragging, and keyboard interaction. Generate sample-data screenshots with:

```powershell
dotnet run --project LimitBar.Desktop -- --screenshots
```

Never include real account identifiers, tokens, credential files, or raw authenticated API responses.

## Send a pull request

Describe the problem, the change, and how you checked it. Link a related issue if one exists. Keep unrelated cleanup separate. Use an imperative commit subject such as `Hide unavailable session limits`.

All pull requests should pass `scripts/verify.ps1`. Explain any manual checks you could not run. Screenshots are expected for visible UI changes.

Open an issue before building a large feature or adding a dependency. Reports about supported provider APIs, accessibility, Windows compatibility, and install/update behavior are especially useful.

## Releases and conduct

Maintainers follow [the release guide](docs/releases.md). Binaries belong in release assets, not source commits.

Be respectful, specific, and constructive. Discuss the code and behavior, not the person. Contributions are provided under the project's [MIT license](LICENSE).
