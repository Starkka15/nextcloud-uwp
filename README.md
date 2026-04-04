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
- Copy files between folders
- Move files between folders
- File info dialog (name, size, path, modified date)

### File Previews
- **Image viewer** — inline viewer with pinch-to-zoom and pan (ScrollViewer)
- **Video/Audio player** — `MediaPlayerElement` with transport controls, downloads to temp first
- **Text viewer** — plain text in Consolas, 512 KB cap

### Search
- Full-text DAV SEARCH across your entire Nextcloud
- Tap a result to open or navigate into it

### Sort
- Sort by name, date modified, or size
- Folders always appear above files regardless of sort order

### File Icons
- Dynamic Segoe MDL2 glyphs by file type (folder, image, video, audio, PDF, document, generic)

### Trash Bin
- List deleted files with original location and deletion date
- Restore individual files
- Delete permanently
- Empty entire trash

### Notifications
- Notifications page — full list from the OCS Notifications API
- **Background polling** (every 15 min) — shows toast for new notifications, updates badge count
- **Live tile** — displays current quota usage on medium and wide tiles

### Activities
- Activities page — full feed from the OCS Activity API (file changes, shares, comments)

### Accounts
- Multi-account support — add, remove, and switch accounts
- Account info displayed in Settings (display name, email, server, quota bar)
- Legacy single-account migration on first launch

### Auto Upload
- Pick any local folder (FolderPicker — camera roll, Downloads, etc.)
- Configurable Nextcloud destination path (default: `/Photos/AutoUpload`)
- Skips already-uploaded files (tracks last sync timestamp)
- Progress bar with live file count and final uploaded/skipped/failed summary
- **Background auto-sync** (every 30 min) — runs without the app open

### Background Tasks
- In-process background tasks (no separate WinRT component required)
- Notification polling: 15 min timer · internet condition · shows toasts · updates tile badge
- Auto-upload sync: 30 min timer · internet condition · uses persisted folder access token
- Toggles in Settings page with OS permission request on first enable

### UI
- Nextcloud blue (#0082c9) header throughout
- Favorite star shown inline in file list
- Context menu on hold/right-tap: Download, Rename, Favorite, Share, Copy, Move, Info, Delete
- Settings page with quota progress bar, background task toggles, and version info

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
│   ├── CloudFile.cs              — file/folder with type detection, icon glyphs, preview flags
│   ├── TrashbinFile.cs           — trashbin item model
│   ├── UserAccount.cs            — account with quota, display name, active flag
│   ├── NextcloudNotification.cs  — OCS notification model
│   └── NextcloudActivity.cs      — OCS activity model
├── Services/
│   ├── WebDavClient.cs           — PROPFIND, PUT, GET, DELETE, MOVE, COPY, MKCOL,
│   │                               PROPPATCH, SEARCH (DASL), trashbin operations
│   ├── NextcloudClient.cs        — OCS API: user info, capabilities, shares, notifications, activities
│   ├── SettingsService.cs        — multi-account storage, background task prefs, auto-upload state
│   ├── SyncService.cs            — auto-upload: scan folder, skip already-synced
│   ├── BackgroundTaskManager.cs  — register/unregister in-process background tasks
│   └── TileService.cs            — live tile XML (medium + wide), badge updates
├── ViewModels/
│   ├── MainViewModel.cs          — file ops, search, trashbin, favorites, sharing, copy/move
│   └── LoginViewModel.cs         — login flow with server validation
└── Views/
    ├── MainPage                  — file browser + context menu + sort
    ├── LoginPage                 — login / add-account
    ├── AccountsPage              — account switcher
    ├── SettingsPage              — account info, quota, auto-upload, background task toggles
    ├── SearchPage                — DAV search results
    ├── TrashbinPage              — trash list + restore/delete
    ├── ImagePreviewPage          — pinch-to-zoom image viewer
    ├── MediaPlayerPage           — video/audio player
    ├── TextViewerPage            — plain text viewer
    ├── NotificationsPage         — OCS notifications list
    └── ActivitiesPage            — OCS activity feed
```

---

## Planned

- **Passcode / PIN lock** — app-level lock screen
- **Login Flow v2** — QR / WebView-based auth for modern Nextcloud servers
- **Share with user/group** — internal share via OCS shares API
- **PDF viewer** — inline PDF rendering
- **Offline files** — mark files for offline access, sync queue
- **E2E encryption** — end-to-end encrypted folder support
- **Contact & calendar backup** — sync to Nextcloud Contacts/Calendar via CardDAV/CalDAV
- **Share target** — receive files from other apps via the share contract
- **Document scanning** — capture and upload via camera

---

## License

Apache 2.0
