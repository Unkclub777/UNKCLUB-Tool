# UNKCLUB Tool (PreInstallTool)

WPF setup and error-fix utility for the UNKCLUB emulator workflow.

## Download (customers)

**Download only `UNKCLUB Tool.exe` from GitHub Releases:**

https://github.com/Unkclub777/UNKCLUB-Tool/releases

Each release provides a single-file app (~155 MB self-contained .NET). Required installer payloads are downloaded automatically in the background on first launch — no separate zip download is needed.

An internet connection is required on first run while setup files are fetched silently.

## Requirements

- Windows 10/11
- Internet connection on first run
- For development: .NET 8 SDK

## Build (developers)

Place `UNKCLUB.exe` and other first-install files in [`Kurulum dosyaları/`](Kurulum%20dosyalar%C4%B1/README.md) before building. These payloads are excluded from Git.

```powershell
dotnet publish PreInstallTool/PreInstallTool.csproj -c Release -r win-x64
```

Publish produces a **small exe only** — installers are not embedded. For local debugging, the app uses `Installers/` beside the build output or `PreInstallTool/embedded-staging/` when present.

## Auto-update

The app checks for updates on startup (background) and via **Check for updates** in the UI.

1. Compares the running version with the latest [GitHub Release](https://github.com/Unkclub777/UNKCLUB-Tool/releases).
2. Downloads `UNKCLUB Tool.exe` only (small update).
3. Falls back to `version.json` on the `master` branch if the Releases API is unavailable.
4. Replaces the running exe and restarts; installer payloads refresh silently in the background when the bundle version changes.

Repository settings: `PreInstallTool/Services/UpdateConstants.cs`.

## Publishing a release (maintainers)

1. Bump version in `PreInstallTool/PreInstallTool.csproj`, `version.json`, and `PreInstallTool/app.manifest`.
2. Build the installer bundle locally (requires `Kurulum dosyaları/` payloads):

```powershell
.\PreInstallTool\pack-installers-bundle.ps1
```

3. Commit and push to `master`.
4. Tag and push (CI builds the exe and creates the release):

```powershell
git tag v1.2.1
git push origin master
git push origin v1.2.1
```

The [Release workflow](.github/workflows/release.yml) publishes `UNKCLUB Tool.exe` and `version.json` as the user-facing release assets. The `installers-bundle.zip` is a backend asset: CI attaches it when present in the workspace, otherwise upload it manually after CI:

```powershell
gh release upload v1.2.1 PreInstallTool/installers-bundle.zip --clobber
```

**Important:** Every release must include `installers-bundle.zip` on GitHub (not listed for end users) so the app can fetch installers silently via the Releases API.

## Dağıtım modeli (TR)

- **Müşteriler** yalnızca `UNKCLUB Tool.exe` indirir; kurulum dosyaları arka planda otomatik indirilir.
- **GitHub** kaynak kodu ve release dosyalarını barındırır; büyük kurulum dosyaları repoda değildir.
- **Geliştirme** için `Kurulum dosyaları/` klasörünü yerelde doldurun; Git'e eklenmez.

## Version bump

Edit in `PreInstallTool/PreInstallTool.csproj`:

```xml
<Version>1.2.1</Version>
<AssemblyVersion>1.2.1.0</AssemblyVersion>
<FileVersion>1.2.1.0</FileVersion>
<InformationalVersion>1.2.1</InformationalVersion>
```

Also update `version.json` and `PreInstallTool/app.manifest`.
