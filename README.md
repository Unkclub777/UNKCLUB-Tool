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

Place `UNKCLUB.exe` and other first-install files in [`Kurulum dosyaları/`](Kurulum%20dosyalar%C4%B1/README.md) before building the **installers bundle** (prerequisites only). `UNKCLUB.exe` is **not** included in `installers-bundle.zip` — upload it as a separate GitHub release asset; the app downloads it at runtime.

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

The [Release workflow](.github/workflows/release.yml) publishes `UNKCLUB Tool.exe` and `version.json` as the user-facing release assets. Upload **`installers-bundle.zip`** (prerequisites only) and **`UNKCLUB.exe`** (emulator binary) as separate backend release assets:

```powershell
gh release upload v1.3.0 PreInstallTool/installers-bundle.zip UNKCLUB.exe --clobber
```

**Important:** Every release must include `installers-bundle.zip` and `UNKCLUB.exe` on GitHub (not listed for end users) so the app can fetch payloads silently.

The app downloads the bundle using direct release URLs first (`/releases/download/vTAG/installers-bundle.zip`), then falls back to the GitHub Releases API. **The repository must be public** (or `version.json` must point `installersBundleUrl` to a publicly reachable URL). Private repos return HTTP 404 for unauthenticated downloads and the GitHub API without a token.

## Dağıtım modeli (TR)

- **Müşteriler** yalnızca `UNKCLUB Tool.exe` indirir; kurulum dosyaları arka planda otomatik indirilir.
- **GitHub** kaynak kodu ve release dosyalarını barındırır; büyük kurulum dosyaları repoda değildir.
- **Geliştirme** için `Kurulum dosyaları/` klasörünü yerelde doldurun; Git'e eklenmez.

## Version bump

Edit in `PreInstallTool/PreInstallTool.csproj`:

```xml
<Version>1.3.0</Version>
<AssemblyVersion>1.3.0.0</AssemblyVersion>
<FileVersion>1.3.0.0</FileVersion>
<InformationalVersion>1.3.0</InformationalVersion>
```

Also update `version.json` and `PreInstallTool/app.manifest`.
