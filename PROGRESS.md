# Nextcloud UWP - Progress Log

## Project Setup
- Created F:\nextcloud-uwp with NextcloudUWP.sln targeting Windows 10 16299 (1709)
- Remember: 1709 on PC != 1709 on W10M (phone has fewer features)
- Cloned nextcloud-android to F:\nextcloud-android for reference

## What's Built (Phase 1 — Core)
- LoginPage: server URL + username/password auth
- MainPage: file browser, back navigation, upload, create folder, sort (6 modes), context menu (9 actions)
- WebDavClient: PROPFIND, MKCOL, PUT, GET, DELETE, MOVE, COPY, SEARCH, trashbin ops
- NextcloudClient: OCS API — user info, capabilities, shares, notifications, activities, comments, user/group search, Login Flow v2
- SettingsService: multi-account with active account switching, auto-upload settings
- CloudFile/UserAccount/TrashbinFile/ShareInfo/FileComment/OperationEntity models

## What's Built (Phase 2 — Features)
- AccountsPage: multi-account list/switch/add/remove
- SearchPage: DAVSEARCH full-text search
- TrashbinPage: list/restore/delete/empty
- ImagePreviewPage: download + BitmapImage + pinch-to-zoom (0.5–10x)
- MediaPlayerPage: download to temp + App.AppMediaPlayer singleton, SMTC lock screen controls, background audio
- TextViewerPage: download + display + **edit mode** (save back via WebDAV PUT)
- WebViewPage: PDF/SVG/GIF via EdgeHTML + Markdown inline renderer
- ShareFilePage: list shares, create public link, add user/group share, delete, permission display
- CommentsPage: list comments + post new comment
- LoginFlowPage: Nextcloud Login Flow v2 WebView + credential poll
- NotificationsPage, ActivitiesPage: list feeds
- SettingsPage: account info, quota bar, auto-upload folder picker, background task toggles, sign out
- ThumbnailCacheService: downloads Nextcloud preview thumbnails, caches in LocalFolder/thumbcache/
- CacheService: caches folder listings as JSON in LocalFolder/filecache/ for offline access
- OfflineQueueService: queues failed operations (upload/delete/rename/move/mkdir) for later retry
- SyncService: upload-only auto-upload + two-way sync with conflict resolution
- BackgroundTaskManager: timer tasks every 15 min (notifications) + 30 min (auto-sync)
- TileService: live tile (quota bar) + badge (unread notification count)
- App.xaml.cs: in-process background task dispatcher, App.AppMediaPlayer singleton

## Architecture Notes
- MVVM-ish (Views, ViewModels, Services, Models)
- Offline-first: GetFilesAsync falls back to CacheService on HttpRequestException
- Operations that fail offline are queued in OfflineQueueService
- SQLite.Net-PCL referenced but not used — replaced by JSON-file cache (no platform adapter needed)
- No E2E encryption, no certificate pinning, no contact/calendar sync

## Remaining Gaps
- Image crop/rotate
- Collabora/Nextcloud Office (just a WebView to server URL — easy when needed)
- Certificate pinning
- E2E encryption
- Share target contract (receive files from other apps)
- QR code login
- Passcode/biometric lock
- Deep link nc:// activation
- Contact/Calendar backup
- Connectivity monitoring (NetworkInformation events)

## Known Bugs Fixed
- Newtonsoft.Json 13.0.3 → 10.0.3 (reverted back — compile errors with 10.0.3 on NETFX_CORE)
- UserAccount.Id property missing
- JToken.Value<bool>() → ToObject<bool>()
- HttpRequestHeaders.Depth → Headers.Add("Depth", "1")
- StorageFile.OpenStreamForReadAsync → OpenReadAsync().AsStreamForRead()
- CloudFile.Path double-pathing on folder navigation
- OpenFile UriFormatException — downloads via ViewModel first

## Git
- Repo initialized at F:\nextcloud-uwp
- .gitignore excludes .vs/, bin/, obj/, *.user, *.suo
