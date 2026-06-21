# UNKCLUB Tool — User Guide (English)

**Version:** 1.6.0  
**Last updated:** June 2026

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [System Requirements](#2-system-requirements)
3. [Download & First Launch](#3-download--first-launch)
4. [Main Window Overview](#4-main-window-overview)
5. [Choosing a Mode](#5-choosing-a-mode)
6. [Reseller Selection](#6-reseller-selection)
7. [Language Selection](#7-language-selection)
8. [Defender Status Panel](#8-defender-status-panel)
9. [Starting Installation](#9-starting-installation)
10. [After Reboot (Auto-Resume)](#10-after-reboot-auto-resume)
11. [Desktop Output](#11-desktop-output)
12. [restore-services.ps1](#12-restore-servicesps1)
13. [Troubleshooting](#13-troubleshooting)
14. [FAQ](#14-faq)
15. [Support & Updates](#15-support--updates)

---

## 1. Introduction

**UNKCLUB Tool** is a Windows setup and repair utility for the UNKCLUB emulator workflow. It guides you through:

- Installing required system components (Visual C++ runtimes, DirectX, and related prerequisites)
- Preparing your PC for the emulator (Windows Defender adjustments, service configuration, and related steps)
- Fixing common **Riot Vanguard** and **Riot Client** issues
- Downloading and placing **UNKCLUB.exe** on your Desktop

You only need to download **one file** — `UNKCLUB Tool.exe`. Everything else (installer bundle and UNKCLUB.exe) is fetched automatically from GitHub in the background when needed.

> **Important:** Run the tool **as Administrator** for best results. Many steps require elevated permissions.

---

## 2. System Requirements

| Requirement | Details |
|-------------|---------|
| **Operating system** | Windows 10 (build 10240 or later) or Windows 11 (64-bit) |
| **Administrator rights** | Strongly recommended — required for most install steps |
| **Internet connection** | Required on first launch and during installation (GitHub downloads) |
| **Disk space** | At least **500 MB** free on the drive where `%LocalAppData%` is located |
| **Riot software** | Riot Client and Vanguard (for Valorant or related games) should be installed for the Vanguard repair flow |
| **GitHub release assets** | The official GitHub repository must be **public**, with release files available (see [Troubleshooting](#13-troubleshooting)) |

---

## 3. Download & First Launch

### Where to download

Download **only** `UNKCLUB Tool.exe` from the official GitHub Releases page:

**https://github.com/Unkclub777/UNKCLUB-Tool/releases**

- The file is a self-contained application (~155 MB). No separate ZIP or installer bundle download is needed.
- Do **not** download `installers-bundle.zip` or `UNKCLUB.exe` manually — the tool downloads these for you.

### First launch steps

1. Save `UNKCLUB Tool.exe` to a folder you can find easily (for example, Downloads or Desktop).
2. **Right-click** the file and choose **Run as administrator**.
3. If Windows SmartScreen appears, choose **More info** → **Run anyway** (the tool may adjust SmartScreen during setup).
4. On first launch, a **loading overlay** appears while the tool downloads required setup files (`installers-bundle.zip`) from GitHub. This happens silently in the background.
5. When the main window opens, the tool also **checks for updates** automatically. If a newer version is available, you will see an update dialog.

> **Tip:** Keep the tool connected to the internet until the loading overlay disappears and the main window is fully ready.

---

## 4. Main Window Overview

The window is divided into a **left panel** and a **main area**.

### Left panel — Defender Status

- Title: **Defender Status**
- A list of Windows security features with colored indicators:
  - **Green** = On / Enabled
  - **Red** = Off / Disabled
- Features shown include: Windows Defender Service, Real-Time Protection, Behavior Monitoring, Network Protection, Cloud Protection, PUA Protection, Tamper Protection, SmartScreen (App/File), and Edge SmartScreen.
- **Refresh** button — updates the status list at any time.
- **Last updated** timestamp below the title.

### Main area — top section

- **UNKCLUB Tool** title and tagline: *Emulator setup and error fix tool*
- Short instruction text: *Select reseller and setup mode, then start the installation.*
- **Version number** (for example, `UNKCLUB Tool v1.6.0`)
- **Language** dropdown (top-right)

### Main area — Reseller Selection

Two large selectable cards side by side:

- **UNKCLUB** — UNKCLUB.exe is downloaded automatically to your Desktop folder.
- **Other Reseller** — You provide your own reseller `.exe` file.

The selected card is highlighted with a colored border.

### Main area — Setup Mode

Two selectable cards:

- **First Install** — Full first-time setup.
- **Error Fix** — Repairs Vanguard and Riot Client issues.

### Main area — Status & progress

- Description of the selected mode
- **Current step** label (visible while installation is running)
- Status message and progress bar

### Main area — Log panel

- Scrollable log of everything the tool does
- **Copy Log** — copies the full log to the clipboard (useful for support)
- **Save Log** — saves the log to a `.txt` file

### Main area — action buttons (bottom)

| Button | Purpose |
|--------|---------|
| **Start Installation** | Begins the selected setup mode (after pre-flight checks) |
| **Cancel** | Stops a running installation |
| **Check for Updates** | Manually checks GitHub for a newer version |
| **Launch Main Program** | Appears after successful completion (if configured) |

### Post-reboot banner

After your PC restarts and the tool resumes automatically, an **amber banner** appears at the top:

> *Installation is continuing after reboot...*

---

## 5. Choosing a Mode

Select **one** setup mode before clicking **Start Installation**.

### First Install

Use this when setting up UNKCLUB on a PC **for the first time**.

**What it does (summary):**

1. Stops non-essential user applications
2. Disables third-party Windows services (Microsoft and Vanguard/Riot services are excluded)
3. Sets Vanguard and Riot services to start automatically
4. Lowers UAC (User Account Control) to “Never notify”
5. Disables Windows Defender and SmartScreen (optional — skipped if Defender is not present)
6. Checks Vanguard installation status
7. Installs prerequisites (Visual C++, DirectX, etc.) — **skips already installed items**
8. Stops Vanguard and Riot, removes the Vanguard folder, launches Riot Client for Vanguard reinstall
9. **Restarts your PC** (you confirm before restart)
10. After reboot — verifies Vanguard, stops services again, downloads **UNKCLUB.exe**, creates shortcuts, and launches UNKCLUB

### Error Fix

Use this when UNKCLUB was already set up but you have **Vanguard or Riot Client errors**, or the emulator is not working correctly.

**How it differs from First Install:**

- Reinstalls prerequisite components **every time** (does not skip already installed items)
- Does **not** change UAC level
- Otherwise follows the same Vanguard repair and post-reboot flow

Both modes **continue automatically after reboot** — you do not need to click **Start Installation** again for phase 2.

---

## 6. Reseller Selection

Choose your reseller **before** selecting a mode and starting installation.

### UNKCLUB (default)

- The tool downloads **UNKCLUB.exe** from GitHub during the final phase.
- File is placed in: `Desktop\unkclub(new)\UNKCLUB.exe`
- A desktop shortcut **UNKCLUB.lnk** is created (configured to **Run as administrator**).

### Other Reseller

- The tool **does not** download UNKCLUB.exe.
- You must place your reseller’s `.exe` file in the `Desktop\unkclub(new)` folder yourself.
- A dialog reminds you to do this before the final launch step.
- The tool launches whichever `.exe` it finds in that folder (as administrator).

Your reseller choice is remembered for the next time you open the tool.

---

## 7. Language Selection

Use the **Language** dropdown in the top-right corner of the main window.

Available languages:

| Language | Display name |
|----------|----------------|
| Turkish | Türkçe |
| English | English |
| French | Français |
| Portuguese (Brazil) | Português (Brasil) |
| Chinese (Simplified) | 中文 (简体) |
| Korean | 한국어 |
| German | Deutsch |

Changing the language updates all labels, buttons, log messages, and dialogs immediately. Your choice is saved for future sessions.

---

## 8. Defender Status Panel

The left panel shows a live snapshot of Windows security settings.

### When to use it

- **Before installation** — see which Defender features are currently active.
- **During installation** — the tool disables Defender as part of setup; watch the indicators turn red (Off).
- **After installation** — confirm Defender features are disabled as expected.
- **Any time** — click **Refresh** to re-read the current state.

### What the colors mean

- **Green dot** — feature is **On / Enabled**
- **Red dot** — feature is **Off / Disabled**

### Security notice during install

Before disabling Defender, the tool adds **exclusions** for its own folders (app directory, Installers, Desktop `unkclub(new)`, etc.) to reduce false virus warnings. This is normal and allows installation to proceed safely.

---

## 9. Starting Installation

Follow these steps in order:

### Step 1 — Prepare

1. Close important applications and save your work (the PC will restart).
2. Ensure Riot Client / Valorant is installed if you need the Vanguard repair flow.
3. Run **UNKCLUB Tool.exe** as **Administrator**.

### Step 2 — Configure

1. Select your **Reseller** (UNKCLUB or Other Reseller).
2. Select **Setup Mode** (First Install or Error Fix).
3. Optionally change **Language**.

### Step 3 — Start

Click **Start Installation**.

### Pre-flight checks (automatic)

Before any install step runs, the tool verifies:

| Check | If it fails |
|-------|-------------|
| Running as administrator | Warning — you can continue, but many steps may fail |
| Internet connection (github.com) | **Blocks** installation — fix connection and retry |
| Disk space (≥ 500 MB in LocalAppData) | **Blocks** installation |
| `installers-bundle.zip` reachable on GitHub | **Blocks** installation |
| `UNKCLUB.exe` reachable on GitHub | **Blocks** installation |

If checks fail, a dialog lists the problems. Click **OK** to retry or **Cancel** to abort.

### Phase 1 — Before reboot (what you will see)

The log panel shows each step as it runs. Key moments where **you** may need to act:

1. **Vanguard check** — if Vanguard is missing, a warning appears; install Vanguard via Valorant/Riot Client if needed.
2. **Prerequisite installers** — Visual C++, DirectX, etc. may show installer windows briefly; the tool handles most of this automatically.
3. **Riot Client launch** — Riot Client opens. **Complete the Vanguard reinstall prompt** in Riot Client.
4. **Restart confirmation** — the tool asks: *Have you completed the Vanguard installation and been prompted to restart?* Click **Yes** to proceed.
5. **PC restart** — you receive a 10-second warning. Save all work. The PC restarts automatically.

During phase 1, the tool also saves **restore-services.ps1** and **disabled-services.json** to your Desktop `unkclub(new)` folder (see [Section 12](#12-restore-servicesps1)).

### Phase 2 — After reboot (automatic)

See [Section 10](#10-after-reboot-auto-resume).

---

## 10. After Reboot (Auto-Resume)

When your PC restarts after phase 1:

1. **UNKCLUB Tool** starts automatically (registered during phase 1).
2. The amber **post-reboot banner** appears: *Installation is continuing after reboot...*
3. Phase 2 steps run automatically:
   - Verify Vanguard services (vgk, vgc) — wait up to 3 minutes if needed
   - Stop Vanguard services again
   - Stop Riot Client processes
   - Download and deploy **UNKCLUB.exe** (UNKCLUB reseller only)
   - Create **UNKCLUB.lnk** shortcuts (Run as administrator)
   - Launch **UNKCLUB.exe** as administrator
4. A **success dialog** appears when complete. The Desktop `unkclub(new)` folder opens in File Explorer.
5. A **Windows toast notification** confirms: *Installation complete — UNKCLUB setup finished successfully.*

### If auto-resume does not work

- Open **UNKCLUB Tool** manually **as Administrator**.
- Select the same **Reseller** and **Error Fix** (or **First Install**) mode.
- Click **Start Installation** — the tool detects pending post-reboot state and continues phase 2.

> **Note:** If auto-start registration failed, the log will show a warning. Manual restart of the tool is required in that case.

---

## 11. Desktop Output

After a successful installation, your Desktop contains:

### Folder: `unkclub(new)`

Located at your actual Desktop path (including OneDrive Desktop if you use OneDrive sync):

```
Desktop\unkclub(new)\
├── UNKCLUB.exe              ← Emulator application (UNKCLUB reseller)
├── UNKCLUB.lnk              ← Shortcut (Run as administrator)
├── restore-services.ps1     ← Service restore script
└── disabled-services.json   ← Record of disabled services
```

### Desktop shortcut

- **UNKCLUB.lnk** on the Desktop root (and inside `unkclub(new)`)
- Configured to **Run as administrator**
- Points to `UNKCLUB.exe` in the `unkclub(new)` folder

### Other Reseller

If you chose **Other Reseller**, place your reseller `.exe` in `unkclub(new)` before the final step. The tool launches that file instead of downloading UNKCLUB.exe.

---

## 12. restore-services.ps1

During installation, UNKCLUB Tool **disables third-party (non-Microsoft) Windows services** to ensure a clean setup. Vanguard and Riot services are **not** disabled.

### What is created

After the “Disable non-Microsoft services” step, the tool saves two files in `Desktop\unkclub(new)\`:

| File | Purpose |
|------|---------|
| `disabled-services.json` | List of every service that was disabled, with original startup type |
| `restore-services.ps1` | PowerShell script to re-enable those services |

### When to use it

Run `restore-services.ps1` **after installation is fully complete** if you want to restore third-party services (antivirus helpers, vendor utilities, game launchers, etc.) to their previous startup settings.

You do **not** need to run it for UNKCLUB to work. It is optional and intended for users who want their PC’s service configuration back to normal.

### How to run it

1. Open File Explorer and go to `Desktop\unkclub(new)\`.
2. **Right-click** `restore-services.ps1` → **Run with PowerShell**.
3. If prompted, run **as Administrator** (required for `sc config` commands).
4. The script reads `disabled-services.json` and restores each service’s original start type.
5. When finished, you will see: *Service restore completed.*

### If the script fails

- Ensure `disabled-services.json` is in the **same folder** as the script.
- Run PowerShell **as Administrator**.
- If a service no longer exists on your PC, that entry is skipped with a message.

---

## 13. Troubleshooting

### Setup files download failed (HTTP 404)

**Symptoms:** Loading overlay fails; pre-flight check reports *GitHub release asset unreachable*; message mentions HTTP 404.

**Causes & fixes:**

| Cause | Fix |
|-------|-----|
| GitHub repository is **private** | Repository must be **public** for unauthenticated downloads |
| `installers-bundle.zip` missing from release | Maintainer must upload it to the GitHub release |
| `UNKCLUB.exe` missing from release | Maintainer must upload it to the GitHub release |
| Wrong or outdated tool version | Update to the latest `UNKCLUB Tool.exe` from Releases |

### No internet / cannot reach github.com

- Check your network connection and firewall.
- Ensure GitHub is not blocked by your ISP, VPN, or corporate proxy.
- Click **OK** in the pre-flight dialog to retry after fixing connectivity.

### Not running as administrator

**Symptoms:** Pre-flight warning *Not running as administrator*; install steps fail with permission errors.

**Fix:** Close the tool. Right-click `UNKCLUB Tool.exe` → **Run as administrator**.

### OneDrive Desktop path

If your Desktop is synced via OneDrive, the `unkclub(new)` folder is created on your **OneDrive Desktop**, not `C:\Users\<you>\Desktop`. The tool detects OneDrive automatically.

**To find the folder:**

- Open File Explorer → **Desktop** (under OneDrive in the sidebar), or
- After successful install, File Explorer opens the folder automatically.

### Low disk space

Pre-flight requires at least **500 MB** free where `%LocalAppData%` is stored (usually `C:`). Free up space and retry.

### Vanguard not installed or incomplete

**Symptoms:** Warning dialog *Riot Vanguard does not appear to be properly installed*.

**Fix:** Install or repair Vanguard through Valorant or Riot Client first, then run UNKCLUB Tool again.

### Riot Client not found

Ensure Riot Client is installed in one of the standard locations:

- `C:\Riot Games\Riot Client\`
- `C:\Program Files\Riot Games\Riot Client\`
- `C:\Program Files (x86)\Riot Games\Riot Client\`

### Installation failed mid-way

1. Read the **Log** panel for the failed step name and error.
2. Click **Copy Log** or **Save Log** and send the log to support.
3. Try **Error Fix** mode if **First Install** failed partway through.

### Auto-resume after reboot did not start

1. Log in to Windows.
2. Run **UNKCLUB Tool** as Administrator manually.
3. Select the same mode and click **Start Installation**.

### Defender still shows green (enabled) after disable step

Some Defender features (especially Tamper Protection) may resist changes on certain Windows editions. The tool logs a warning and continues. Use the **Refresh** button on the Defender panel to verify current state.

### Update download failed

- Check internet connection.
- Click **Check for Updates** to retry manually.
- Download the latest `UNKCLUB Tool.exe` directly from GitHub Releases.

---

## 14. FAQ

**Q: Do I need to download installers-bundle.zip or UNKCLUB.exe separately?**  
A: No. Download only `UNKCLUB Tool.exe`. Everything else is fetched automatically.

**Q: Will my PC restart during setup?**  
A: Yes. Both First Install and Error Fix restart your PC once during the Vanguard repair phase. Phase 2 continues automatically after reboot.

**Q: Is it safe to disable Windows Defender?**  
A: The tool disables Defender temporarily for installation compatibility. Re-enable Defender manually in Windows Security after setup if you wish. Exclusions are added first to reduce false positives.

**Q: What is the difference between First Install and Error Fix?**  
A: First Install is for new setups (skips already installed prerequisites, adjusts UAC). Error Fix reinstalls all prerequisites every time and focuses on repairing Vanguard/Riot issues.

**Q: I use a third-party reseller emulator. Which option do I choose?**  
A: Select **Other Reseller** and place your reseller `.exe` in `Desktop\unkclub(new)` before the final step.

**Q: Where is UNKCLUB.exe after installation?**  
A: `Desktop\unkclub(new)\UNKCLUB.exe` (or your OneDrive Desktop equivalent).

**Q: Do I need to run restore-services.ps1?**  
A: Only if you want third-party Windows services re-enabled after installation. It is optional.

**Q: Can I run the tool without Valorant/Riot installed?**  
A: The Vanguard repair steps require Riot Client. Install Riot/Valorant first for the full flow to work.

**Q: How do I update UNKCLUB Tool?**  
A: The tool checks for updates on startup. You can also click **Check for Updates**. Updates download only the new `UNKCLUB Tool.exe` and restart the app.

**Q: The tool says it is already running. What do I do?**  
A: Only one instance can run at a time. Close the existing window or end `UNKCLUB Tool.exe` in Task Manager, then reopen.

---

## 15. Support & Updates

### Official download

**https://github.com/Unkclub777/UNKCLUB-Tool/releases**

Always download from the official GitHub Releases page to get the genuine, latest version.

### Auto-update (v1.6.0+)

- On startup, UNKCLUB Tool compares your version with the latest GitHub Release.
- If an update is available, a dialog offers to download and install it.
- Only `UNKCLUB Tool.exe` is downloaded (~155 MB); installer payloads refresh silently if the bundle version changed.
- Use **Check for Updates** in the bottom button bar at any time.

### When contacting support

Include:

1. Your **UNKCLUB Tool version** (shown under the title, e.g. v1.6.0)
2. **Setup mode** used (First Install or Error Fix)
3. **Reseller** selection (UNKCLUB or Other Reseller)
4. **Windows version** (Settings → System → About)
5. **Log file** — use **Copy Log** or **Save Log** from the main window

### Version 1.6.0 highlights

- Service restore script (`restore-services.ps1`) saved after install
- Desktop shortcut with **Run as administrator**
- Opens `unkclub(new)` folder on successful completion
- Windows toast notification on success
- Pre-flight checks before installation starts
- Defender status panel with refresh
- Language selector (7 languages)
- Log **Copy** and **Save** buttons
- Auto-update on startup
- First-start loading overlay while setup files download

---

*© UNKCLUB — This guide is for end users. For developer and release documentation, see the project README.*
