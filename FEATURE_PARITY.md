# Nextcloud UWP - Feature Parity Tracker

Target: Windows 10 Mobile Fall Creators Update (Build 16299)

## Core File Management

| Android Feature | Status | UWP Approach |
|---|---|---|
| WebDAV file browsing | Done | WebDavClient (PROPFIND) |
| Upload/Download | Done | WebDavClient (PUT/GET) |
| Create folder | Done | WebDavClient (MKCOL) |
| Auth (basic) | Done | NextcloudClient + SettingsService |
| Share link creation | Done | NextcloudClient (OCS shares API) |
| Move/Copy/Delete | Done | WebDavClient (MOVE/COPY/DELETE) |
| Share with user | Done | OCS shares API (shareType=0) |
| Share with group | Done | OCS shares API (shareType=1) |
| Share permissions editor | Done | ShareFilePage context menu |
| File comments | Done | CommentsPage + OCS comments DAV API |

## Account & Authentication

| Android Feature | Status | UWP Approach |
|---|---|---|
| Multi-account | Done | AccountsPage + SettingsService |
| OAuth2 / Login Flow v2 | Done | LoginFlowPage WebView + poll endpoint |
| Basic auth | Done | LoginPage username/password |
| Passcode / biometric lock | Not started | Windows Hello or PIN |
| SAML / SSO | Not started | WebView auth |
| Deep link login (nc://) | Not started | Protocol activation in Package.appxmanifest |
| Nextcloud SSO for other apps | Not started | AppService (IPC) |

## Sync & Offline

| Android Feature | Status | UWP Approach |
|---|---|---|
| Offline file browsing | Done | CacheService (JSON in LocalFolder) |
| Offline operation queue | Done | OfflineQueueService — queues delete/rename/move/upload |
| Two-way sync | Done | SyncService.TwoWaySyncAsync — upload local-only, download remote-only |
| Auto-upload (photos/videos) | Done | SyncService.UploadFolderAsync + BackgroundTask |
| Conflict resolution | Done | ConflictResolution enum — KeepRemote/KeepLocal/SaveBoth |
| Connectivity monitoring | Not started | NetworkInformation.NetworkStatusChanged |

## Media

| Android Feature | Status | UWP Approach |
|---|---|---|
| Audio playback | Done | MediaPlayerPage + App.AppMediaPlayer singleton |
| Video playback | Done | MediaPlayerPage + App.AppMediaPlayer singleton |
| Background audio | Done | App.AppMediaPlayer singleton + backgroundMediaPlayback capability + SMTC |
| Image preview (pinch-zoom) | Done | ImagePreviewPage (ScrollViewer + DirectManipulation) |
| GIF support | Done | WebViewPage (EdgeHTML renders animated GIFs natively) |
| SVG rendering | Done | WebViewPage (EdgeHTML renders SVG natively) |
| PDF preview | Done | WebViewPage (EdgeHTML renders PDF natively) |
| Markdown rendering | Done | WebViewPage inline Markdown→HTML converter |
| Image crop/rotate | Not started | Custom WriteableBitmap manipulation |
| Thumbnail caching | Done | ThumbnailCacheService — LocalFolder/thumbcache/ |

## Sharing & Collaboration

| Android Feature | Status | UWP Approach |
|---|---|---|
| Share with user/group | Done | ShareFilePage + OCS shares API |
| Share link management | Done | ShareFilePage — list/delete existing shares |
| Share permissions editor | Done | Update permissions via OCS PUT |
| File comments | Done | CommentsPage + WebDAV comments endpoint |

## Content Editing

| Android Feature | Status | UWP Approach |
|---|---|---|
| Text file editing | Done | TextViewerPage edit mode — PUT on save |
| Markdown rendering | Done | WebViewPage inline renderer |
| PDF preview | Done | WebViewPage (EdgeHTML) |
| SVG rendering | Done | WebViewPage (EdgeHTML) |
| Collabora/Nextcloud Office | Not started | WebView pointing to server editor URL |

## Server Features

| Android Feature | Status | UWP Approach |
|---|---|---|
| Trash bin (delete/restore) | Done | TrashbinPage + WebDavClient trashbin methods |
| Unified search | Done | SearchPage + WebDavClient DAVSEARCH |
| Server notifications | Done | Background polling + toast + badge + NotificationsPage |
| Activity feed | Done | ActivitiesPage + NextcloudClient |
| Capabilities detection | Partial | GetCapabilitiesAsync stubbed |
| External links | Not started | WebView |
| AI Assistant | Not started | WebView or native chat UI |

## System Integration

| Android Feature | Status | UWP Approach |
|---|---|---|
| Live tile / dashboard widget | Done | TileService (quota + badge) |
| Toast notifications | Done | Background polling → ToastNotificationManager |
| File picker contract | Not started | FileOpenPicker / FolderPicker integration |
| Share target (receive files) | Not started | ShareTarget declaration in manifest |
| Contact backup/restore | Not started | Windows.ApplicationModel.Contacts |
| Calendar backup/restore | Not started | Windows.ApplicationModel.Appointments |
| Camera / document scanning | Not started | MediaCapture API |
| QR code scanner | Not started | MediaCapture + ZXing.Net |

## Security

| Android Feature | Status | UWP Approach |
|---|---|---|
| E2E encryption setup | Not started | Windows.Security.Cryptography equivalent |
| E2E encrypted upload/download | Not started | Crypto stream wrapper |
| Certificate pinning | Not started | HttpClientHandler.ServerCertificateCustomValidationCallback |

## Data & Storage

| Android Feature | Status | UWP Approach |
|---|---|---|
| Offline file listing cache | Done | CacheService (JSON files in LocalFolder/filecache/) |
| Offline operation queue | Done | OfflineQueueService (JSON in LocalFolder) |
| Thumbnail cache | Done | ThumbnailCacheService (LocalFolder/thumbcache/) |
| Key-value settings | Done | SettingsService (ApplicationData) |
| Upload queue persistence | Done | OfflineQueueService |
