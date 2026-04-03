using System;
using Windows.ApplicationModel;
using Windows.Storage;
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

        public SettingsPage()
        {
            this.InitializeComponent();
            var v = Package.Current.Id.Version;
            VersionText.Text = $"Nextcloud UWP v{v.Major}.{v.Minor}.{v.Build}";
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            // Populate auto-upload settings
            RemotePathBox.Text = _settings.AutoUploadRemotePath;
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
            catch { }
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
                var used = FormatSize(quotaUsed);
                var total = FormatSize(quotaTotal);
                var pct = (double)quotaUsed / quotaTotal * 100.0;
                QuotaText.Text = $"{used} of {total} used";
                QuotaBar.Value = pct;
                QuotaPanel.Visibility = Visibility.Visible;
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

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }
    }
}
