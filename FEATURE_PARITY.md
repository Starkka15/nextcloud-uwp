# Nextcloud UWP - Feature Parity Tracker

Target: Windows 10 Mobile Creators Update (Build 15063)

## Core File Management

| Android Feature | Status | UWP Approach |
|---|---|---|
| WebDAV file browsing | Done | WebDavClient (PROPFIND) |
| Upload/Download | Done | WebDavClient (PUT/GET) |
| Create folder | Done | WebDavClient (MKCOL) |
| Auth (basic) | Done | NextcloudClient + SettingsService |
| Share link creation | Done | NextcloudClient (OCS shares API) |
| Move/Copy/Delete | Done | WebDavClient (MOVE/COPY/DELETE) |

## Account & Authentication

| Android Feature | Status | UWP Approach |
|---|---|---|
| Multi-account | Not started | Account management UI + collection |
| OAuth2 / Login Flow v2 | Not started | WebView-based login flow |
| Passcode / biometric lock | Not started | Windows Hello or PIN |
| SAML / SSO | Not started | WebView auth |
| Deep link login (nc://) | Not started | Protocol activation in Package.appxmanifest |
| Nextcloud SSO for other apps | Not started | AppService (IPC) |

## Sync & Offline

| Android Feature | Status | UWP Approach |
|---|---|---|
| Offline file browsing | Not started | SQLite cache of file listings |
| Offline operation queue | Not started | SQLite queue + BackgroundTask |
| Two-way sync | Not started | BackgroundTask with TimeTrigger |
| Auto-upload (photos/videos) | Not started | BackgroundTask + MediaLibrary trigger |
| Conflict resolution | Not started | ContentDialog picker UI |
| Connectivity monitoring | Not started | NetworkInformation.NetworkStatusChanged |

## Media

| Android Feature | Status | UWP Approach |
|---|---|---|
| Audio playback | Not started | MediaPlayerElement + BackgroundMediaPlayer |
| Video playback | Not started | MediaPlayerElement + MediaElement |
| Background audio | Not started | BackgroundTask (audio) |
| Image preview (pinch-zoom) | Not started | ScrollViewer + DirectManipulation |
| GIF support | Not started | Animated image or WebView |
| SVG rendering | Not started | WebView (native SVG) |
| Image crop/rotate | Not started | Custom WriteableBitmap manipulation |
| Thumbnail caching | Not started | LocalFolder + in-memory BitmapImage cache |

## Sharing & Collaboration

| Android Feature | Status | UWP Approach |
|---|---|---|
| Share with user/group | Not started | OCS shares API |
| Share link management | Not started | OCS shares API |
| Share permissions editor | Not started | ContentDialog UI |
| File comments | Not started | OCS comments API |

## Content Editing

| Android Feature | Status | UWP Approach |
|---|---|---|
| Text file editing | Not started | WebView or native TextBox |
| Collabora/Nextcloud Office | Not started | WebView (EdgeHTML) |
| Markdown rendering | Not started | WebView with JS renderer or custom |
| PDF preview | Not started | WebView or PDF viewer API |

## Server Features

| Android Feature | Status | UWP Approach |
|---|---|---|
| Trash bin (delete/restore) | Not started | OCS trashbin API |
| Unified search | Not started | OCS unified search API |
| Server notifications | Not started | BackgroundTask polling + Toast |
| Capabilities detection | Not started | OCS capabilities API (already stubbed) |
| External links | Not started | WebView |
| AI Assistant | Not started | WebView or native chat UI |

## System Integration

| Android Feature | Status | UWP Approach |
|---|---|---|
| Live tile / dashboard widget | Not started | Adaptive tiles + BackgroundTask updater |
| Toast notifications | Not started | ToastNotificationManager |
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
| SQLite database (12 tables) | Not started | SQLite.Net-PCL (PackageReference added) |
| File metadata cache | Not started | FileEntity table |
| Upload queue persistence | Not started | UploadEntity table |
| Key-value settings | Done | SettingsService (ApplicationData) |
| Disk-based image cache | Not started | LocalFolder + LRU eviction |
