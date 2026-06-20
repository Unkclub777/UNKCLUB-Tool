# UNKCLUB Tool (PreInstallTool)

WPF setup and error-fix utility for the UNKCLUB emulator workflow.

## Download (customers)

**Download the latest release only from GitHub:**

https://github.com/Unkclub777/UNKCLUB-Tool/releases

Each release includes:

- `UNKCLUB Tool.exe` — single-file app (install payloads embedded inside)
- `UNKCLUB-Tool.zip` — same exe, for auto-update

No local `Dagitim` folder or desktop build copy is required for distribution.

## Requirements

- Windows 10/11
- For development: .NET 8 SDK

## Build (developers)

Place `UNKCLUB.exe` and other first-install files in [`Kurulum dosyaları/`](Kurulum%20dosyalar%C4%B1/README.md) before building. These payloads are excluded from Git.

```powershell
dotnet publish PreInstallTool/PreInstallTool.csproj -c Release -r win-x64
```

The publish step runs `PackEmbeddedResources` (MSBuild `ZipDirectory`) and embeds `embedded.bundle.zip` into the single exe.

## Auto-update

The app checks for updates on startup (background) and via **Check for updates** in the UI.

1. Compares the embedded version with the latest [GitHub Release](https://github.com/Unkclub777/UNKCLUB-Tool/releases).
2. Accepts `UNKCLUB-Tool.zip` or `UNKCLUB Tool.exe` as the release asset.
3. Falls back to `version.json` on the `master` branch if the Releases API is unavailable.
4. Downloads the update, replaces the running exe, and restarts.

Repository settings: `PreInstallTool/Services/UpdateConstants.cs`.

## Publishing a release

1. Bump version in `PreInstallTool/PreInstallTool.csproj` and `version.json`.
2. Commit and push to `master`.
3. Tag and push:

```powershell
git tag v1.1.1
git push origin master
git push origin v1.1.1
```

The [Release workflow](.github/workflows/release.yml) builds the single-file exe (with embedded bundle), creates a GitHub Release with the exe and zip, and updates `version.json`.

## Version bump

Edit in `PreInstallTool/PreInstallTool.csproj`:

```xml
<Version>1.1.1</Version>
<AssemblyVersion>1.1.1.0</AssemblyVersion>
<FileVersion>1.1.1.0</FileVersion>
<InformationalVersion>1.1.1</InformationalVersion>
```

Also update `version.json` and `PreInstallTool/app.manifest`.
