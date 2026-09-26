# Maintaining a release

## Build locally

Use Windows and the .NET 8 SDK. Update `Directory.Build.props` and `CHANGELOG.md`, then run:

```powershell
powershell -NoProfile -File scripts/release.ps1
```

This downloads the pinned Inno Setup compiler into `artifacts/tools/inno` using its portable mode, checks its SHA-256 digest, builds `LimitBar.slnf`, runs both offline check executables, and publishes a self-contained `win-x64` app. No compiler installation is made globally.

Outputs are under `artifacts/releases/<version>/`:

- `LimitBar-<version>-win-x64-setup.exe`
- `LimitBar-<version>-win-x64-portable.zip`
- `SHA256SUMS.txt`
- `app/`: unpackaged application files, for inspection only

The script refuses to overwrite an existing version directory. Move a failed or superseded local build aside before retrying. To use your own compiler, pass `-Compiler 'C:\path\to\ISCC.exe'`.

Do not enable trimming: this desktop app uses WPF and Windows Forms. Self-contained releases must be rebuilt to incorporate .NET security updates.

## Test before publishing

1. Run `scripts/verify.ps1`.
2. Install and uninstall the EXE in a clean Windows user/VM with no .NET runtime preinstalled.
3. Check Start menu launch, optional startup, same-directory upgrade, and uninstall cleanup. Preferences should survive upgrades/uninstall; provider credentials must remain untouched.
4. Test both providers, missing sign-ins, expired credentials, and missing session windows.
5. Check display scaling, hover/click dismissal, primary taskbar placement, fullscreen behavior, and sleep/resume.
6. Inspect release archives for credentials, local paths, and accidental build-tool files.

The desktop render checks are useful regression coverage; they do not replace interactive Windows testing.

## GitHub release workflow

Push a version tag matching `Directory.Build.props`, for example `v0.1.0-beta.1`. The release workflow builds on Windows and creates a **draft** GitHub release with the installer, ZIP, and checksums. It marks versions containing `-` as prereleases. Review the draft, attach release notes, and publish it.

Pull requests run the Windows CI workflow with read-only repository permissions. Release creation runs only for version tags. Do not place secrets or signing certificates in the repository.

The first community release is unsigned. Describe that clearly in the download instructions. If signing is added, sign binaries and the installer before generating checksums, and document the publisher identity.

## Screenshots and launch assets

Run `dotnet run --project LimitBar.Desktop -- --screenshots` to render the actual popup with sample data and regenerate the original app icon. Review every image before committing. `docs/assets/overview.png` is a composed product overview, not a capture of a user's desktop.

The optional Brag skill is local development tooling, ignored by Git and excluded from installers. It is not required to build or contribute to LimitBar.
