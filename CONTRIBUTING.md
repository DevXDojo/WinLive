# Contributing to WinLive

Thank you for helping improve WinLive. Contributions of code, tests, documentation, translations, and UI refinements are welcome.

## Code of Conduct

Everyone participating in this project must follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Before You Start

Search [existing issues](https://github.com/DevXDojo/WinLive/issues) before opening a new one. For a substantial feature, architecture change, new dependency, or change to media-file handling, open an issue first so the approach and scope can be discussed.

To prepare a branch:

```powershell
git clone https://github.com/YOUR_USERNAME/WinLive.git
cd WinLive
git remote add upstream https://github.com/DevXDojo/WinLive.git
git switch -c feature/short-description
```

Use a focused branch and keep unrelated changes out of the same pull request.

## Development Setup

WinLive is a Windows 11, x64 application built with C#, .NET 8, WinUI 3, and the Windows App SDK.

Prerequisites:

- Windows 11 x64 (build 22621 or later)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- The Windows App SDK development workload in Visual Studio 2022
- Git

Restore, test, and run the unpackaged development build from the repository root:

```powershell
dotnet restore WinLive.sln
dotnet test WinLive.sln
dotnet run --project .\src\WinLive\WinLive.csproj
```

The last command runs a self-contained executable without installing WinLive.

## Repository Layout

- `src/WinLive/` — WinUI application, XAML views, view models, localization, and core media-library code
- `src/WinLive/Core/` — media discovery, state persistence, duplicate detection, export, and decoder boundaries
- `src/WinLive/ViewModels/` — gallery state, image loading, and thumbnail caching
- `tests/WinLive.Tests/` — xUnit tests for the platform-independent core code
- `imgs/` — images used by the repository documentation
- `artifacts/` — generated build and packaging output; do not commit generated artifacts

## Making Changes

### C# and XAML

- Keep nullable reference types enabled and do not introduce compiler warnings; the repository treats warnings as errors.
- Follow the existing file-scoped namespace and naming style. Prefer small, focused types and methods.
- Keep asynchronous file and media operations cancellable. Do not block the UI thread with synchronous I/O or decoding.
- Keep UI behavior in the WinUI layer and testable media-library behavior in `Core` where practical.
- Preserve accessibility, keyboard navigation, high-DPI behavior, and light/dark theme support when changing XAML.
- Do not commit secrets, signing private keys, machine-specific paths, generated packages, or build output.

### Media and Local Data Safety

WinLive is a read-only library with respect to the media folders selected by users. Changes must not modify, rename, move, or write metadata into source media files.

When working on Live Photos, treat the HEIC still image and its paired MOV file as one logical item. Copy/export operations must preserve both files and must not overwrite an existing destination file. User-created state and preferences belong under `%LOCALAPPDATA%\WinLive`, not beside the source media.

### Localization

The application supports English and Simplified Chinese at runtime. Add or update strings in both languages, keep terminology consistent, and verify that longer translated text fits the UI. Update both `README.md` and `README_zh.md` when a documentation change affects shared content.

### Dependencies

Keep new dependencies to a minimum. In the pull request, explain why a dependency is needed, its license, and whether it adds native binaries or affects package size. Update the relevant project file and include tests for behavior that depends on it.

## Testing

Run the full automated test suite before submitting a pull request:

```powershell
dotnet test WinLive.sln
```

For application or packaging changes, also verify the x64 build:

```powershell
dotnet build .\src\WinLive\WinLive.csproj -p:Platform=x64
```

Add or update xUnit tests in `tests/WinLive.Tests/` for changes to core behavior. Tests must be deterministic, clean up temporary files, and must not depend on media or paths that exist only on the contributor's computer.

Manually verify the affected user flow on Windows 11. Depending on the change, include representative checks for HEIC images, HEIC+MOV Live Photos, ordinary images or videos, an empty folder, nested folders, duplicate names during export, cancellation, and both application languages. Include screenshots or a short recording for visible UI changes.

## MSIX Packaging and Signing

Generate an MSIX package only when the change needs packaging validation:

```powershell
dotnet build .\src\WinLive\WinLive.csproj -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true
```

The development workflow uses a local self-signed certificate. Install `WinLive-Development.cer` into `Cert:\CurrentUser\TrustedPeople` before installing that development package. Never commit a certificate private key. Public releases must use an organization-controlled certificate and an appropriate timestamped signature.

## FFmpeg and Third-Party License Compliance

The application currently references `FFmpeg.LGPL`, which places dynamically linked FFmpeg DLLs in the build output. The package declares LGPL-3.0-or-later, while FFmpeg components can have different licensing depending on their build configuration. Before updating FFmpeg, adding codecs, or distributing a WinLive binary:

1. Verify the exact license and configuration of the FFmpeg build. Do not assume that every FFmpeg build is LGPL-only.
2. Keep FFmpeg dynamically linked and retain the original library names and copyright notices.
3. Include the applicable GNU LGPL and GPL license texts with the distributed binary. The NuGet build target copies the DLLs but does not copy its `LICENSE.txt` automatically.
4. Provide the complete, exact corresponding FFmpeg source code, any local changes, and the build/configuration information for the DLLs being distributed. Keep this source available alongside the corresponding release.
5. Preserve users' ability to replace the FFmpeg DLLs with an interface-compatible modified version and do not prohibit reverse engineering for debugging such modifications.
6. Keep the FFmpeg attribution in both READMEs accurate. Add equivalent attribution to any download page or in-app legal/about view introduced later.
7. Review every other library compiled into the FFmpeg build for its own license and any applicable patent obligations.

Consult the official [FFmpeg License and Legal Considerations](https://ffmpeg.org/legal.html) checklist for every release. A README attribution alone is not sufficient for binary distribution. If the licensing of a proposed build is unclear, do not publish it until it has been reviewed.

## Commits and Pull Requests

Write concise, imperative commit subjects that explain the change, for example `Fix Live Photo export collisions` or `Update Chinese gallery labels`. Rebase or merge the current upstream branch before requesting review, and resolve conflicts in your branch.

A pull request should include:

- What changed and why
- A link to the related issue, when applicable
- Automated and manual test results
- Screenshots or a recording for UI changes
- Notes about localization, dependencies, packaging, data migration, or compatibility impact

Before submitting, confirm that:

- [ ] The solution restores, builds, and tests successfully
- [ ] New or changed core behavior has test coverage
- [ ] Source media remains untouched
- [ ] English and Simplified Chinese content is updated where required
- [ ] Documentation reflects user-visible or contributor-facing changes
- [ ] No secrets, private keys, machine-specific files, or generated artifacts are included
- [ ] New or updated dependencies and bundled binaries have been license-reviewed

Keep the pull request focused and respond to review feedback with additional commits. Maintainers may ask you to rebase or squash before merge.

## Bug Reports, Feature Requests, and Security Issues

Open normal bug reports and feature requests in [GitHub Issues](https://github.com/DevXDojo/WinLive/issues). Include clear reproduction steps, expected and actual behavior, the WinLive version, Windows version, relevant media formats, logs, and screenshots where useful. Do not attach private photos or other sensitive media; create a minimal non-sensitive sample when possible.

Do not report vulnerabilities in a public issue. Follow the private reporting instructions in the [Security Policy](SECURITY.md).

## License

By contributing, you agree that your contribution is licensed under the repository's [GPL-3.0 license](LICENSE). You must have the right to submit all code, assets, translations, and other material included in your contribution.
