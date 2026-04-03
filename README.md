# Nextcloud UWP

A Nextcloud client for **Windows 10 Mobile**, built from scratch in C# / XAML using UWP and .NET Native.

Tested on: **Lumia 1520** · Build 10.0.15254.603 (Mobile 1709) · ARM32

---

## Features

### File Management
- Browse files and folders with full back-navigation
- Upload files via file picker
- Download files to device (FileSavePicker)
- Open files in external apps (downloads to temp, then launches)
- Create new folders
- Rename files and folders
- Delete (moves to Nextcloud trash)
- Favorite / unfavorite (star indicator in file list)
- Share — creates a public link and copies it to clipboard

### Search
- Full-text DAV SEARCH across your entire Nextcloud
- Tap a result to open or navigate into it

### Trash Bin
- List deleted files with original location and deletion date
- Restore individual files
- Delete permanently
- Empty entire trash

### Accounts
- Multi-account support — add, remove, and switch accounts
- Account info displayed in Settings (display name, email, server, quota bar)
- Legacy single-account migration on first launch

### Auto Upload
- Pick any local folder (FolderPicker — camera roll, Downloads, etc.)
- Configurable Nextcloud destination path (default: `/Photos/AutoUpload`)
- Skips already-uploaded files (tracks last sync timestamp)
- Progress bar with live file count and final uploaded/skipped/failed summary

### UI
- Nextcloud blue (#0082c9) header throughout
- Favorite star shown inline in file list
- Context menu on hold/right-tap: Download, Rename, Favorite, Share, Delete
- Settings page with quota progress bar

---

## Building

### Requirements
- Visual Studio 2022+ with:
  - UWP development workload
  - **MSVC ARM build tools** (for ARM32 release builds)
- Windows SDK 10.0.16299.0
- NuGet packages restore automatically

### Debug build (x86/x64)
Open `NextcloudUWP.sln`, select **Debug | x86**, press F5.

### ARM32 release (for device deployment)
1. Build **Release | ARM** in Visual Studio (`Rebuild All`)
2. Run `PackAndInstallARM.ps1` from the repo root — this packs, signs, and copies the `.appx` to `Z:\W10M-Dependencies\` for Device Portal install

The script patches the manifest MinVersion from 16299 → 15063 so the app installs on W10M Mobile 1709 (build 15254).

### Dependencies (ARM32 device)
Install via Device Portal before sideloading:
- `Microsoft.NET.Native.Framework.2.1.appx`
- `Microsoft.NET.Native.Runtime.2.1.appx`
- `Microsoft.VCLibs.ARM.14.00.appx` (if not already present)

Use `GetNativeRuntime21ARM.ps1` to download the .NET Native packages from NuGet automatically.

---

## Architecture

```
NextcloudUWP/
├── Models/
│   ├── CloudFile.cs          — file/folder with computed display properties
│   ├── TrashbinFile.cs       — trashbin item model
│   └── UserAccount.cs        — account with quota, display name, active flag
├── Services/
│   ├── WebDavClient.cs       — PROPFIND, PUT, GET, DELETE, MOVE, COPY,
│   │                           SEARCH (DASL), trashbin operations
│   ├── NextcloudClient.cs    — OCS API: user info, capabilities, share links
│   ├── SettingsService.cs    — multi-account storage (JSON in LocalSettings)
│   └── SyncService.cs        — auto-upload: scan folder, skip already-synced
├── ViewModels/
│   ├── MainViewModel.cs      — file ops, search, trashbin, favorites, sharing
│   └── LoginViewModel.cs     — login flow with server validation
└── Views/
    ├── MainPage              — file browser + context menu
    ├── LoginPage             — login / add-account
    ├── AccountsPage          — account switcher
    ├── SettingsPage          — account info, quota, auto-upload
    ├── SearchPage            — DAV search
    └── TrashbinPage          — trash list + restore/delete
```

---

## Planned

- **Image previewer** — inline image viewer with pinch-to-zoom
- **Media playback** — audio and video via `MediaPlayerElement`
- **Offline files** — mark files for offline access, sync queue
- **Background sync** — periodic sync via background task (manifest/activation issues on W10M, paused)
- **E2E encryption** — end-to-end encrypted folder support
- **Text/Markdown editor** — basic editing via WebView
- **Contact & calendar backup** — sync to Nextcloud Contacts/Calendar via CardDAV/CalDAV
- **Live tile** — quota or recent files on the Start tile
- **Share target** — receive files from other apps via the share contract
- **QR login** — scan QR code from web interface (Login Flow v2)
- **Document scanning** — capture and upload via camera

---

## License

Apache 2.0
