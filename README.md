# UNKCLUB Tool (PreInstallTool)

WPF setup and error-fix utility for the UNKCLUB emulator workflow.

## Requirements

- .NET 8 SDK
- Windows 10/11

## Build

```powershell
dotnet build PreInstallTool.sln -c Release
```

Output: `PreInstallTool/bin/Release/net8.0-windows/PreInstallTool.exe`

## Installer payloads

Place `UNKCLUB.exe` and other first-install files in [`Kurulum dosyaları/`](Kurulum%20dosyalar%C4%B1/README.md) before building. These files are excluded from Git.

## Auto-update

The app checks for updates on startup (background) and via **Check for updates** in the UI.

1. Compares the embedded version (`PreInstallTool.csproj` → `Version`) with GitHub Releases (`PreInstallTool.zip`).
2. Falls back to `version.json` on the default branch if the Releases API is unavailable.
3. Downloads the zip, replaces only app binaries in the install folder, and preserves:
   - `Installers/`
   - `install-config.json`
   - user settings in `%LocalAppData%\PreInstallTool\`

Before shipping, set your GitHub username in:

- `PreInstallTool/Services/UpdateConstants.cs` → `GitHubOwner`
- `version.json` → `downloadUrl`

## Publishing a release

### Option A — GitHub Actions (recommended)

1. Log in: `gh auth login`
2. Push this repository to GitHub.
3. Create and push a tag:

```powershell
git tag v1.0.1
git push origin v1.0.1
```

The [Release workflow](.github/workflows/release.yml) builds `PreInstallTool.zip`, creates a GitHub Release, and updates `version.json`.

### Option B — Manual

```powershell
dotnet publish PreInstallTool/PreInstallTool.csproj -c Release -o publish
Remove-Item publish/Installers -Recurse -Force -ErrorAction SilentlyContinue
Compress-Archive publish/* PreInstallTool.zip
gh release create v1.0.1 PreInstallTool.zip --title "UNKCLUB Tool v1.0.1"
```

Then bump `version` in `version.json` and `PreInstallTool.csproj`, commit, and push.

## Version bump

Edit in `PreInstallTool/PreInstallTool.csproj`:

```xml
<Version>1.0.1</Version>
<AssemblyVersion>1.0.1.0</AssemblyVersion>
<FileVersion>1.0.1.0</FileVersion>
<InformationalVersion>1.0.1</InformationalVersion>
```
