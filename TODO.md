# nextcloud-uwp — Issues & Improvements

## Critical
- [ ] Passwords stored in plaintext in LocalSettings JSON — migrate to `Windows.Security.Credentials.PasswordVault`
- [ ] Certificate pinning not enforced — `CertPinningService` exists but `WebDavClient`/`NextcloudClient` never validate against it
- [ ] No HTTPS enforcement — `http://` server URLs accepted without warning on LoginPage

## High
- [ ] `OfflineQueueService._idSeed` resets to 1 on restart — duplicate IDs possible with existing queued items
- [ ] Upload failures not properly queued — `OperationEntity.LocalFilePath` not set, so queued uploads always fail on retry
- [ ] Silent empty `catch { }` blocks everywhere — at minimum log to DebugLog or add exception tracing

## Medium
- [ ] `FormatSize()` copy-pasted across 5 files (`CloudFile.cs`, `UserAccount.cs`, `TrashbinFile.cs`, `TileService.cs`, `SettingsPage.xaml.cs`) — extract to shared utility class
- [ ] `XmlEscape()` misses `"` and `'` — exists in `App.xaml.cs` and `TileService.cs`, could break toast XML and XML attributes
- [ ] Duplicate `MainViewModel`/service instances — every page `new`s its own; no shared state or singleton
- [ ] Fire-and-forget async calls — `_ = SomeAsyncMethod()` without error handling in `App.xaml.cs`, `MainPage.xaml.cs`, `LoginFlowPage.xaml.cs`
- [ ] ShareFilePage `PermSwitch` toggle not wired — decorative only, does not update permissions
- [ ] No `CancellationToken` support on network operations — can't cancel when navigating away
- [ ] Markdown renderer XSS risk — `javascript:` URLs not sanitized in `WebViewPage`
- [ ] No localization — all UI strings hardcoded English

## Low
- [ ] `SearchPage` uses hardcoded `Glyph="&#xE7C3;"` for all results instead of binding to `IconGlyph`
- [ ] Placeholder project GUID `{A1B2C3D4-...}` in `.sln`
- [ ] Version mismatch — Assembly/manifest say 1.0.0.0 but CHANGELOG shows v1.0.0 and git tag is v1.2.0
- [ ] SQLite.Net-PCL NuGet package referenced but unused — should be removed from `.csproj`
- [ ] nc:// protocol handler receives credentials in clear URI — warn users about risk or prefer Login Flow v2