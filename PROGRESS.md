# Nextcloud UWP - Progress Log

## Project Setup
- Created F:\nextcloud-uwp with NextcloudUWP.sln targeting Windows 10 16299 (1709)
- Forced to upgrade from 15063 (Creators Update) to 16299 by Visual Studio
- Remember: 1709 on PC != 1709 on W10M (phone has fewer features)
- Cloned nextcloud-android to F:\nextcloud-android for reference

## What's Built
- LoginPage: server URL + username/password auth
- MainPage: file browser with ListView, back navigation, upload, create folder
- WebDavClient: PROPFIND, MKCOL, PUT, GET, DELETE, MOVE, COPY
- NextcloudClient: OCS API (user info, capabilities, shares)
- SettingsService: credential persistence via ApplicationData
- CloudFile/UserAccount models
- Placeholder assets copied from nextcloud-android

## Bugs Fixed So Far
- Newtonsoft.Json 13.0.3 → 10.0.3 (netstandard 2.0 CS0012 errors)
- UserAccount.Id property missing
- JToken.Value<bool>() → ToObject<bool>()
- HttpRequestHeaders.Depth → Headers.Add("Depth", "1")
- StorageFile.OpenStreamForReadAsync → OpenReadAsync().AsStreamForRead()
- SettingsPage not found → pointed to LoginPage
- ButtonPrimary style doesn't exist in UWP → removed
- CloudFile.Path double-pathing on folder navigation → strip WebDAV prefix, store relative path
- OpenFile UriFormatException → now downloads via ViewModel first

## Current Issues (unresolved)
- File open/download still hitting errors (needs investigation)
- Issue.txt will be used for build/runtime errors going forward

## Architecture Notes
- MVVM-ish pattern (Views, ViewModels, Services, Models)
- SQLite.Net-PCL referenced but not yet used
- No offline support, no background tasks yet

## Feature Parity
- See FEATURE_PARITY.md and FEATURES.md for full Android→UWP mapping
- Core file ops done, big gaps: media playback, offline sync, E2E, trash bin, notifications

## Git
- Repo initialized at F:\nextcloud-uwp (no commits yet)
- .gitignore excludes .vs/, bin/, obj/, *.user, *.suo
- Git user: Starkka15/Starkka15@gmail.com (configured)

## Reference Repos
- F:\nextcloud-android (nextcloud/android)
- F:\mullvadvpn-app (mullvad/mullvadvpn-app)
