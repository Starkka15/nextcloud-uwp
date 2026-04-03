# Changelog

All notable changes to Nextcloud UWP are documented here.
Versioning follows [Semantic Versioning](https://semver.org/).

---

## [1.0.0] — 2026-04-02

Initial release. Tested on Lumia 1520 (ARM32), W10M build 10.0.15254.603.

### Added
- **File browser** — PROPFIND-based folder listing with back navigation, sorted folders-first
- **Upload** — file picker → PUT to current folder
- **Download** — FileSavePicker for saving files to device
- **Open** — download to temp folder, launch in external app
- **Create folder** — MKCOL with inline name dialog
- **Rename** — MOVE to same parent with new name, via context menu
- **Delete** — DELETE via context menu with confirmation
- **Favorites** — PROPPATCH `oc:favorite`, star indicator in file list, toggle via context menu
- **Share link** — OCS shares API, public link shown in dialog with clipboard copy
- **Context menu** — hold/right-tap on any file: Download, Rename, Favorite, Share, Delete
- **Search** — DASL SEARCH across all files, results tap to open or navigate
- **Trash bin** — list, restore (MOVE to restore endpoint), delete permanently, empty trash
- **Multi-account** — add/remove/switch accounts; JSON-serialized list in LocalSettings
- **Settings page** — display name, username, email, server, quota bar (refreshes from server)
- **Auto upload** — pick any local folder, configurable Nextcloud destination, skips unchanged files, progress bar with uploaded/skipped/failed count
- **Login** — server URL validation, OCS user fetch on login, display name + email + quota stored
- **Legacy migration** — single-account credential migration on first launch
- **ARM32 build pipeline** — `PackAndInstallARM.ps1` packs, signs, patches manifest MinVersion for W10M

### Fixed
- `CommonFileQuery.OrderByDate` throws `E_INVALIDARG` on non-library folders (FolderPicker result) — switched to `DefaultQuery`

### Known Limitations
- Background sync not working (UWP background task manifest constraints on W10M)
- No offline file support
- No media preview (images open in external viewer)
- DASL search requires Nextcloud server with WebDAV SEARCH enabled
