using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;

namespace NextcloudUWP.Services
{
    public class SyncService
    {
        private readonly WebDavClient _webDav;
        private readonly SettingsService _settings;

        public SyncService()
        {
            _settings = new SettingsService();
            _webDav = new WebDavClient();
            if (_settings.HasCredentials)
                _webDav.Configure(_settings.ServerUrl, _settings.Username, _settings.Password);
        }

        /// <summary>
        /// Uploads files from <paramref name="sourceFolder"/> that are newer than
        /// the last sync timestamp into <paramref name="remotePath"/> on Nextcloud.
        /// Pass onProgress(uploaded, total) for UI feedback; may be null.
        /// </summary>
        public async Task<(int uploaded, int skipped, int failed)> UploadFolderAsync(
            StorageFolder sourceFolder,
            string remotePath,
            IProgress<(int done, int total)> progress = null)
        {
            if (!_settings.HasCredentials)
                throw new InvalidOperationException("No account configured.");

            var lastSyncStr = _settings.AutoUploadLastSync;
            var lastSync = DateTime.MinValue;
            if (!string.IsNullOrEmpty(lastSyncStr))
                DateTime.TryParse(lastSyncStr, out lastSync);

            // Ensure the target folder exists
            try { await _webDav.CreateFolderAsync(remotePath); } catch { }

            var extensions = new string[]
            {
                ".jpg", ".jpeg", ".png", ".gif", ".bmp",
                ".mp4", ".mov", ".3gp", ".heic", ".webp"
            };
            // CommonFileQuery.DefaultQuery works with any folder (picked or library).
            // OrderByDate only works on indexed/library folders and throws E_INVALIDARG otherwise.
            var queryOptions = new QueryOptions(CommonFileQuery.DefaultQuery, extensions)
            {
                FolderDepth = FolderDepth.Deep
            };

            var query = sourceFolder.CreateFileQueryWithOptions(queryOptions);
            var files = await query.GetFilesAsync();

            int uploaded = 0, skipped = 0, failed = 0, done = 0;
            int total = files.Count;

            foreach (var file in files)
            {
                var props = await file.GetBasicPropertiesAsync();
                if (props.DateModified.DateTime <= lastSync)
                {
                    skipped++;
                    done++;
                    progress?.Report((done, total));
                    continue;
                }

                try
                {
                    using (var ras = await file.OpenAsync(FileAccessMode.Read))
                    using (var stream = ras.AsStreamForRead())
                        await _webDav.UploadFileAsync(
                            $"{remotePath.TrimEnd('/')}/{file.Name}",
                            stream,
                            file.ContentType ?? "application/octet-stream");
                    uploaded++;
                }
                catch { failed++; }

                done++;
                progress?.Report((done, total));
            }

            _settings.AutoUploadLastSync = DateTime.UtcNow.ToString("O");
            return (uploaded, skipped, failed);
        }
    }
}
