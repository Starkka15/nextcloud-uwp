using System;
using System.Collections.Generic;
using System.Linq;
using Windows.ApplicationModel;
using Windows.Storage;
using NextcloudUWP;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using NextcloudUWP.Services;

namespace NextcloudUWP.Views
{
    public sealed partial class SettingsPage : Page
    {
        private readonly SettingsService _settings = new SettingsService();
        private StorageFolder _selectedFolder;
        private bool _loadingSettings; // prevents toggle handlers firing during init

        public SettingsPage()
        {
            this.InitializeComponent();
            var v = Package.Current.Id.Version;
            VersionText.Text = $"Nextcloud UWP v{v.Major}.{v.Minor}.{v.Build}";
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            // Load background task toggles without triggering handlers
            _loadingSettings = true;
            NotificationsToggle.IsOn = _settings.NotificationsEnabled;
            AutoSyncToggle.IsOn      = _settings.AutoSyncEnabled;
            AppLockToggle.IsOn       = _settings.AppLockEnabled;
            _loadingSettings = false;

            // Show current pinned thumbprint if any
            var pinned = _settings.PinnedCertThumbprint;
            CertThumbprintText.Text = string.IsNullOrEmpty(pinned)
                ? "No cert pinned"
                : "Pinned: " + pinned;

            // Show saved folder path if any
            var token = _settings.AutoSyncFolderToken;
            if (!string.IsNullOrEmpty(token) &&
                StorageApplicationPermissions.FutureAccessList.ContainsItem(token))
            {
                try
                {
                    var folder = await StorageApplicationPermissions.FutureAccessList
                        .GetFolderAsync(token);
                    _selectedFolder = folder;
                    SelectedFolderText.Text = folder.Path;
                    UploadNowButton.IsEnabled = true;
                }
                catch (Exception ex) { DebugLogger.LogException(nameof(SettingsPage), ex); }
            }

            // Populate auto-upload settings
            RemotePathBox.Text = _settings.AutoUploadRemotePath;

            // Populate sync settings
            var maxSize = _settings.SyncMaxFileSize;
            MaxFileSizeBox.Text = maxSize > 0 ? (maxSize / (1024 * 1024)).ToString() : "512";
            var extensions = _settings.SyncFileExtensions;
            FileExtensionsBox.Text = extensions.Count > 0 ? string.Join(",", extensions) : "";

            var lastSync = _settings.AutoUploadLastSync;
            LastSyncText.Text = string.IsNullOrEmpty(lastSync)
                ? "Never synced"
                : $"Last synced: {DateTime.Parse(lastSync).ToLocalTime():MMM dd, yyyy HH:mm}";

            var account = _settings.GetActiveAccount();
            if (account == null) return;

            ShowAccountInfo(
                account.DisplayName ?? account.Username,
                account.Username,
                account.Email,
                account.ServerUrl,
                account.QuotaUsed,
                account.QuotaTotal);

            try
            {
                var client = new NextcloudClient();
                client.Configure(account.ServerUrl, account.Username, account.Password);
                var fresh = await client.GetUserAsync();
                if (fresh != null)
                {
                    account.DisplayName = fresh.DisplayName ?? account.DisplayName;
                    account.Email = fresh.Email ?? account.Email;
                    account.QuotaUsed = fresh.QuotaUsed;
                    account.QuotaTotal = fresh.QuotaTotal;
                    _settings.SaveAccount(account);

                    ShowAccountInfo(
                        account.DisplayName ?? account.Username,
                        account.Username,
                        account.Email,
                        account.ServerUrl,
                        account.QuotaUsed,
                        account.QuotaTotal);
                }
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(SettingsPage), ex); }
        }

        private void ShowAccountInfo(string displayName, string username, string email,
                                     string serverUrl, long quotaUsed, long quotaTotal)
        {
            AccountLoadingRing.IsActive = false;

            DisplayNameText.Text = displayName;
            DisplayNameText.Visibility = Visibility.Visible;

            UsernameText.Text = username;
            UsernameText.Visibility = Visibility.Visible;

            if (!string.IsNullOrEmpty(email))
            {
                EmailText.Text = email;
                EmailText.Visibility = Visibility.Visible;
            }

            ServerText.Text = serverUrl;
            ServerText.Visibility = Visibility.Visible;

            if (quotaTotal > 0)
            {
                var used = FormatHelper.FormatSize(quotaUsed);
                var total = FormatHelper.FormatSize(quotaTotal);
                var pct = (double)quotaUsed / quotaTotal * 100.0;
                QuotaText.Text = $"{used} of {total} used";
                QuotaBar.Value = pct;
                QuotaPanel.Visibility = Visibility.Visible;

                // Keep live tile up to date whenever the user visits settings.
                TileService.UpdateTile(quotaUsed, quotaTotal);
            }

            SignOutButton.Visibility = Visibility.Visible;
        }

        // ── Auto upload ──────────────────────────────────────────────────

        private void RemotePathBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var path = RemotePathBox.Text?.Trim();
            if (!string.IsNullOrEmpty(path))
                _settings.AutoUploadRemotePath = path.StartsWith("/") ? path : "/" + path;
        }

        private void MaxFileSizeBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(MaxFileSizeBox.Text?.Trim(), out int mb) && mb > 0)
                _settings.SyncMaxFileSize = mb * 1024L * 1024;
            else if (string.IsNullOrWhiteSpace(MaxFileSizeBox.Text))
                _settings.SyncMaxFileSize = 0;
        }

        private void FileExtensionsBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var text = FileExtensionsBox.Text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                _settings.SyncFileExtensions = new List<string>();
                return;
            }
            var extensions = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ext => ext.Trim().StartsWith(".") ? ext.Trim() : "." + ext.Trim())
                .ToList();
            _settings.SyncFileExtensions = extensions;
        }

        private async void PickFolder_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add("*");

            var folder = await picker.PickSingleFolderAsync();
            if (folder == null) return;

            _selectedFolder = folder;
            SelectedFolderText.Text = folder.Path;
            UploadNowButton.IsEnabled = true;

            // Persist so the background sync task can access it without a picker.
            var existingToken = _settings.AutoSyncFolderToken;
            if (string.IsNullOrEmpty(existingToken))
                _settings.AutoSyncFolderToken = StorageApplicationPermissions.FutureAccessList.Add(folder);
            else
                StorageApplicationPermissions.FutureAccessList.AddOrReplace(existingToken, folder);
        }

        private async void UploadNow_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFolder == null) return;

            // Save remote path first
            var remotePath = RemotePathBox.Text?.Trim();
            if (string.IsNullOrEmpty(remotePath)) remotePath = "/Photos/AutoUpload";
            if (!remotePath.StartsWith("/")) remotePath = "/" + remotePath;
            _settings.AutoUploadRemotePath = remotePath;

            UploadNowButton.IsEnabled = false;
            PickFolderButton.IsEnabled = false;
            UploadProgress.Visibility = Visibility.Visible;
            UploadProgress.IsIndeterminate = true;
            UploadStatusText.Text = "Starting...";
            UploadStatusText.Visibility = Visibility.Visible;

            try
            {
                var sync = new SyncService();
                var progress = new Progress<(int done, int total)>(p =>
                {
                    if (p.total > 0)
                    {
                        UploadProgress.IsIndeterminate = false;
                        UploadProgress.Value = (double)p.done / p.total * 100.0;
                    }
                    UploadStatusText.Text = $"{p.done} / {p.total} files processed...";
                });

                var (uploaded, skipped, failed) = await sync.UploadFolderAsync(
                    _selectedFolder, remotePath, progress);

                UploadStatusText.Text = $"Done: {uploaded} uploaded, {skipped} skipped, {failed} failed";
                LastSyncText.Text = $"Last synced: {DateTime.Now:MMM dd, yyyy HH:mm}";
            }
            catch (Exception ex)
            {
                UploadStatusText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                UploadProgress.IsIndeterminate = false;
                UploadProgress.Value = 100;
                UploadNowButton.IsEnabled = true;
                PickFolderButton.IsEnabled = true;
            }
        }

        // ── Background tasks ─────────────────────────────────────────────

        private async void NotificationsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loadingSettings) return;
            _settings.NotificationsEnabled = NotificationsToggle.IsOn;
            await App.RegisterBackgroundTasksAsync(_settings);
            BgTaskStatusText.Text = NotificationsToggle.IsOn
                ? "Notification polling enabled (≈15 min interval)"
                : "Notification polling disabled";
        }

        private async void AutoSyncToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loadingSettings) return;
            _settings.AutoSyncEnabled = AutoSyncToggle.IsOn;
            if (AutoSyncToggle.IsOn && string.IsNullOrEmpty(_settings.AutoSyncFolderToken))
            {
                BgTaskStatusText.Text = "Pick a source folder below to enable auto-upload.";
                AutoSyncToggle.IsOn = false;
                _settings.AutoSyncEnabled = false;
                return;
            }
            await App.RegisterBackgroundTasksAsync(_settings);
            BgTaskStatusText.Text = AutoSyncToggle.IsOn
                ? "Auto-upload enabled (≈30 min interval)"
                : "Auto-upload disabled";
        }

        // ── Security ─────────────────────────────────────────────────────

        private void AppLockToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loadingSettings) return;
            _settings.AppLockEnabled = AppLockToggle.IsOn;
            if (!AppLockToggle.IsOn)
                App.IsUnlocked = true; // clear any pending lock state
        }

        private CertPinningService.CertInfo _checkedCert;

        private async void CheckCertButton_Click(object sender, RoutedEventArgs e)
        {
            var serverUrl = _settings.ServerUrl;
            if (string.IsNullOrEmpty(serverUrl)) return;

            CertRing.IsActive     = true;
            CertStatusText.Text   = "Checking…";
            PinCertButton.IsEnabled = false;
            _checkedCert = null;

            try
            {
                var info = await CertPinningService.GetCertInfoAsync(serverUrl);
                if (info == null)
                {
                    CertStatusText.Text = "Could not retrieve certificate.";
                    return;
                }
                _checkedCert = info;
                CertStatusText.Text =
                    $"Subject: {info.Subject}\n" +
                    $"Issuer:  {info.Issuer}\n" +
                    $"Expiry:  {info.Expiry:yyyy-MM-dd}\n" +
                    $"Valid:   {(info.IsValid ? "Yes" : "No — self-signed or untrusted")}\n" +
                    $"SHA-256: {info.Thumbprint}";
                PinCertButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                CertStatusText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                CertRing.IsActive = false;
            }
        }

        private void PinCertButton_Click(object sender, RoutedEventArgs e)
        {
            if (_checkedCert == null) return;
            _settings.PinnedCertThumbprint = _checkedCert.Thumbprint;
            CertThumbprintText.Text = "Pinned: " + _checkedCert.Thumbprint;
            CertStatusText.Text = "Certificate pinned. App will warn if it changes.";
            PinCertButton.IsEnabled = false;
        }

        private void ClearPinButton_Click(object sender, RoutedEventArgs e)
        {
            _settings.PinnedCertThumbprint = null;
            CertThumbprintText.Text = "No cert pinned";
            CertStatusText.Text = "Pin cleared.";
        }

        // ── Sign out ─────────────────────────────────────────────────────

        private async void SignOut_Click(object sender, RoutedEventArgs e)
        {
            var account = _settings.GetActiveAccount();
            if (account == null) return;

            var dialog = new ContentDialog
            {
                Title = "Sign out",
                Content = $"Remove {account.Username} from {account.ServerUrl}?",
                PrimaryButtonText = "Sign out",
                SecondaryButtonText = "Cancel"
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            _settings.RemoveAccount(account.ServerUrl, account.Username);

            if (!_settings.HasCredentials)
            {
                Frame.Navigate(typeof(LoginPage));
                Frame.BackStack.Clear();
            }
            else
            {
                Frame.Navigate(typeof(MainPage));
                Frame.BackStack.Clear();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
