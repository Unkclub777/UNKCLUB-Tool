# UNKCLUB Tool (PreInstallTool)

WPF setup and error-fix utility for the UNKCLUB emulator workflow.

## Download (customers)

**Download the latest release only from GitHub:**

https://github.com/Unkclub777/UNKCLUB-Tool/releases

Each release includes:

- `UNKCLUB Tool.exe` — single-file app shell (~155 MB self-contained .NET; installer payloads not embedded)
- `installers-bundle.zip` — prerequisite installers and config (~100 MB; downloaded automatically on first run)
- `version.json` — update metadata

On first launch the exe downloads `installers-bundle.zip` from the same GitHub Release into `%LocalAppData%\UNKCLUB-Tool\`. Later app updates replace only the exe; the bundle is refreshed when the app version changes.

## Requirements

- Windows 10/11
- Internet connection on first run (to fetch installer bundle)
- For development: .NET 8 SDK

## Build (developers)

Place `UNKCLUB.exe` and other first-install files in [`Kurulum dosyaları/`](Kurulum%20dosyalar%C4%B1/README.md) before building. These payloads are excluded from Git.

```powershell
dotnet publish PreInstallTool/PreInstallTool.csproj -c Release -r win-x64
```

Publish produces a **small exe only** — installers are not embedded. For local debugging, the app uses `Installers/` beside the build output or `PreInstallTool/embedded-staging/` when present.

To create the release installer bundle locally:

```powershell
.\PreInstallTool\pack-installers-bundle.ps1
```

This writes `PreInstallTool/installers-bundle.zip` for upload to GitHub Releases.

## Auto-update

The app checks for updates on startup (background) and via **Check for updates** in the UI.

1. Compares the running version with the latest [GitHub Release](https://github.com/Unkclub777/UNKCLUB-Tool/releases).
2. Downloads `UNKCLUB Tool.exe` only (small update).
3. Falls back to `version.json` on the `master` branch if the Releases API is unavailable.
4. Replaces the running exe and restarts; installer bundle is refreshed on next start when the app version changes.

Repository settings: `PreInstallTool/Services/UpdateConstants.cs`.

## Publishing a release

1. Bump version in `PreInstallTool/PreInstallTool.csproj`, `version.json`, and `PreInstallTool/app.manifest`.
2. Build the installer bundle locally (requires `Kurulum dosyaları/` payloads):

```powershell
.\PreInstallTool\pack-installers-bundle.ps1
```

3. Commit and push to `master`.
4. Tag and push (CI builds the small exe and creates the release):

```powershell
git tag v1.2.0
git push origin master
git push origin v1.2.0
```

The [Release workflow](.github/workflows/release.yml) uploads `UNKCLUB Tool.exe` and `version.json`. If `PreInstallTool/installers-bundle.zip` exists in the runner workspace it is attached automatically; otherwise upload it manually after CI:

```powershell
gh release upload v1.2.0 PreInstallTool/installers-bundle.zip --clobber
```

**Important:** Every release must include `installers-bundle.zip` so first-time users and updated clients can fetch installers.

## Dağıtım modeli (TR)

- **GitHub** yalnızca kaynak kodu ve release dosyalarını barındırır; büyük kurulum dosyaları repoda değildir.
- **EXE** küçük boyutlu dağıtılır; kurulum dosyaları ilk çalıştırmada veya sürüm güncellemesinde `installers-bundle.zip` olarak indirilir.
- **Geliştirme** için `Kurulum dosyaları/` klasörünü yerelde doldurun; Git’e eklenmez.

## Version bump

Edit in `PreInstallTool/PreInstallTool.csproj`:

```xml
<Version>1.2.0</Version>
<AssemblyVersion>1.2.0.0</AssemblyVersion>
<FileVersion>1.2.0.0</FileVersion>
<InformationalVersion>1.2.0</InformationalVersion>
```

Also update `version.json` and `PreInstallTool/app.manifest`.
