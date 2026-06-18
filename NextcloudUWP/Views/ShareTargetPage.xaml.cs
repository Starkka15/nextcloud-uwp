using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using NextcloudUWP.Services;

namespace NextcloudUWP.Views
{
    public sealed partial class ShareTargetPage : Page
    {
        private ShareOperation _shareOperation;
        private List<StorageFile> _sharedFiles;

        public ShareTargetPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _shareOperation = e.Parameter as ShareOperation;
            if (_shareOperation == null) return;

            _sharedFiles = new List<StorageFile>();

            try
            {
                if (_shareOperation.Data.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await _shareOperation.Data.GetStorageItemsAsync();
                    foreach (var item in items)
                        if (item is StorageFile sf)
                            _sharedFiles.Add(sf);
                }
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(ShareTargetPage), ex); }

            if (_sharedFiles.Count == 0)
            {
                SharedFilesText.Text = "No files received.";
                UploadButton.IsEnabled = false;
                return;
            }

            SharedFilesText.Text = _sharedFiles.Count == 1
                ? $"File: {_sharedFiles[0].Name}"
                : $"{_sharedFiles.Count} files:\n" + string.Join("\n", _sharedFiles.Select(f => "  • " + f.Name));
        }

        private async void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_sharedFiles == null || _sharedFiles.Count == 0) return;

            var destFolder = DestFolderBox.Text?.Trim() ?? "/";
            if (!destFolder.StartsWith("/")) destFolder = "/" + destFolder;

            UploadButton.IsEnabled   = false;
            CancelButton.IsEnabled   = false;
            UploadRing.IsActive      = true;
            StatusText.Visibility    = Visibility.Visible;
            StatusText.Text          = "Uploading…";

            var settings = new SettingsService();
            if (!settings.HasCredentials)
            {
                StatusText.Text = "Not signed in. Open Nextcloud and sign in first.";
                UploadRing.IsActive = false;
                return;
            }

            var dav = new WebDavClient();
            dav.Configure(settings.ServerUrl, settings.Username, settings.Password);

            int ok = 0, fail = 0;
            foreach (var file in _sharedFiles)
            {
                try
                {
                    using (var ras    = await file.OpenReadAsync())
                    using (var stream = ras.AsStreamForRead())
                    {
                        var remotePath = $"{destFolder.TrimEnd('/')}/{file.Name}";
                        var uploaded   = await dav.UploadFileAsync(remotePath, stream,
                            file.ContentType ?? "application/octet-stream");
                        if (uploaded) ok++; else fail++;
                    }
                    StatusText.Text = $"Uploaded {ok} / {_sharedFiles.Count}…";
                }
                catch { fail++; }
            }

            UploadRing.IsActive = false;
            StatusText.Text = fail == 0
                ? $"Done — {ok} file(s) uploaded to {destFolder}"
                : $"Done — {ok} uploaded, {fail} failed";

            _shareOperation.ReportCompleted();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _shareOperation?.ReportError("Cancelled by user.");
        }
    }
}
