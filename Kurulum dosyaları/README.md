Place installer payloads in this folder before building or packaging:

- `UNKCLUB.exe` — main emulator binary (copied to `Installers/App/` on build)
- Other first-install files — copied to `Installers/IlkKurulum/` on build

These files are **not** committed to Git (see root `.gitignore`). Keep them locally or distribute via a private channel.

After adding files here, build the solution:

```powershell
dotnet build PreInstallTool.sln -c Release
```
