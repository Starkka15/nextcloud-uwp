# Nextcloud UWP - Feature Parity Tracker

Target: Windows 10 Mobile, Creators Update (Build 15063)

## Completed

- [x] WebDAV file browsing (PROPFIND)
- [x] File upload/download (PUT/GET)
- [x] Create folder (MKCOL)
- [x] Basic auth (username/password)
- [x] Share link creation (OCS shares API)
- [x] Move/Copy/Delete files (MOVE/COPY/DELETE)
- [x] Settings persistence (ApplicationData)
- [x] File list UI with navigation

## In Progress

- [ ] Multi-account support (need account management UI)
- [ ] OAuth2 / Login Flow v2 (WebView-based)

## Not Started - Core Features

- [ ] E2E encryption (Bouncy Castle equivalent)
- [ ] Offline sync / operation queue (BackgroundTransferService)
- [ ] Auto-upload photos (BackgroundTask + MediaLibrary)
- [ ] Trash bin (OCS trashbin API)
- [ ] Unified search (OCS search API)
- [ ] Conflict resolution UI
- [ ] Two-way folder sync
- [ ] Passcode/biometric lock

## Not Started - Media

- [ ] Image preview with pinch-to-zoom (ScrollViewer + manipulation)
- [ ] Audio playback (MediaPlayerElement)
- [ ] Video playback (MediaPlayerElement)
- [ ] Background audio (MediaPlaybackBackgroundTask)
- [ ] GIF support
- [ ] SVG rendering
- [ ] Image crop/rotate
- [ ] Thumbnail caching (DiskLruCache equivalent)

## Not Started - Integration

- [ ] Contact backup/restore (Contact APIs)
- [ ] Calendar backup/restore (Calendar APIs)
- [ ] Collabora/Nextcloud Office editing (WebView)
- [ ] Text file editing (WebView)
- [ ] Markdown rendering
- [ ] External site rendering (WebView)
- [ ] AI Assistant (WebView or native chat UI)

## Not Started - Platform

- [ ] Live tile / dashboard widget (Adaptive tiles)
- [ ] Push notifications (tile/badge polling via BackgroundTask)
- [ ] Server notification management
- [ ] Document scanning (MediaCapture API)
- [ ] QR code login (ZXing.Net or camera)
- [ ] Deep link activation (protocol: nc://login)
- [ ] File type associations
- [ ] Share target contract (receive files from other apps)

## Architecture Mapping (Android → UWP)

| Android | UWP (15063) |
|---|---|
| Room Database | SQLite.Net-PCL |
| WorkManager | BackgroundTaskBuilder |
| Foreground Service | Extended Execution Session |
| Content Provider | AppService / File Picker contract |
| BroadcastReceiver | System events / Background tasks |
| AccountAuthenticator | WebAccountProvider |
| SharedPreferences | ApplicationData.Current.LocalSettings |
| Dagger 2 | Manual DI / Microsoft.Extensions.DependencyInjection |
| Jetpack ViewModel | INotifyPropertyChanged + MVVM |
| AndroidManifest | Package.appxmanifest |
| Media3/ExoPlayer | MediaPlayerElement |
| Glide/Coil | BitmapImage / custom loader |
| OkHttp | Windows.Web.Http.HttpClient |
| AndroidX WebKit | WebView (EdgeHTML on 15063) |
| OSMDroid | Bing Maps MapControl |
| EventBus | App-level events |
