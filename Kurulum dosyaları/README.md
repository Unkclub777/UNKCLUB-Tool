Place first-install prerequisite payloads in this folder before building or packaging:

- vcredist, DirectX (`dxwebsetup.exe`), and other `.exe` / `.msi` installers — copied to `Installers/IlkKurulum/` on build and included in `installers-bundle.zip`
- **Do not** place `UNKCLUB.exe` here — it is distributed as a separate GitHub release asset and downloaded at runtime

These files are **not** committed to Git (see root `.gitignore`). Keep them locally or distribute via a private channel.

After adding files here, build and pack the bundle:

```powershell
dotnet build PreInstallTool.sln -c Release
.\PreInstallTool\pack-installers-bundle.ps1
```

Upload `installers-bundle.zip` and `UNKCLUB.exe` as separate assets on the GitHub release.
