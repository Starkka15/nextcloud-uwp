using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Storage;
using NextcloudUWP.Models;
using NextcloudUWP.Services;

namespace NextcloudUWP.ViewModels
{
    public class MainViewModel
    {
        private static MainViewModel _instance;
        public static MainViewModel Instance => _instance ?? (_instance = new MainViewModel());

        private readonly WebDavClient          _webDav;
        private readonly NextcloudClient       _nextcloud;
        private readonly SettingsService       _settings;
        private readonly CacheService          _cache;
        private readonly ThumbnailCacheService _thumbs;
        private readonly OfflineQueueService   _queue;

        public string CurrentPath { get; private set; } = "/";
        public bool   IsOffline   { get; private set; }

        public MainViewModel()
        {
            _settings  = new SettingsService();
            _webDav    = new WebDavClient();
            _nextcloud = new NextcloudClient();
            _cache     = new CacheService();
            _thumbs    = new ThumbnailCacheService();
            _queue     = new OfflineQueueService();
            Reconfigure();
        }

        public void Reconfigure()
        {
            if (_settings.HasCredentials)
            {
                _webDav.Configure(_settings.ServerUrl, _settings.Username, _settings.Password);
                _nextcloud.Configure(_settings.ServerUrl, _settings.Username, _settings.Password);
                _thumbs.Configure(_settings.ServerUrl, _settings.Username, _settings.Password);
            }
        }

        // ── File listing (with offline cache fallback) ────────────────────────

        public async Task<List<CloudFile>> GetFilesAsync(string path)
        {
            CurrentPath = path;
            try
            {
                var files = await _webDav.ListFilesAsync(path);
                if (files?.Count > 0) files.RemoveAt(0);
                files = files ?? new List<CloudFile>();
                await _cache.SaveAsync(path, files);
                IsOffline = false;
                return files;
            }
            catch (HttpRequestException)  { IsOffline = true; }
            catch (TaskCanceledException) { IsOffline = true; }

            return await _cache.LoadAsync(path) ?? new List<CloudFile>();
        }

        public async Task LoadThumbnailsAsync(IEnumerable<CloudFile> files)
        {
            foreach (var file in files)
            {
                if (file.IsImage || file.HasPreview)
                {
                    var bmp = await _thumbs.GetThumbnailAsync(file);
                    if (bmp != null) file.ThumbnailBitmap = bmp;
                }
            }
        }

        // ── Upload / download ─────────────────────────────────────────────────

        public async Task UploadFileAsync(StorageFile file, string remotePath)
        {
            using (var ras = await file.OpenReadAsync())
            using (var stream = ras.AsStreamForRead())
            {
                var fullPath = $"{remotePath.TrimEnd('/')}/{file.Name}";
                bool ok;
                try
                {
                    ok = await _webDav.UploadFileAsync(fullPath, stream,
                        file.ContentType ?? "application/octet-stream");
                }
                catch (HttpRequestException)
                {
                    await _queue.EnqueueAsync(new OperationEntity
                    {
                        OperationType = "upload",
                        SourcePath    = fullPath,
                        LocalFilePath = file.Path
                    });
                    return;
                }
                if (!ok) throw new Exception("Upload failed.");
            }
        }

        public async Task CreateFolderAsync(string name, string parentPath)
        {
            var fullPath = $"{parentPath.TrimEnd('/')}/{name}";
            try
            {
                if (!await _webDav.CreateFolderAsync(fullPath))
                    throw new Exception("Failed to create folder.");
            }
            catch (HttpRequestException)
            {
                await _queue.EnqueueAsync(new OperationEntity
                {
                    OperationType = "mkdir",
                    SourcePath    = fullPath
                });
            }
        }

        public async Task<bool> DeleteFileAsync(CloudFile file)
        {
            try
            {
                return await _webDav.DeleteFileAsync(file.Path);
            }
            catch (HttpRequestException)
            {
                await _queue.EnqueueAsync(new OperationEntity
                {
                    OperationType = "delete",
                    SourcePath    = file.Path
                });
                return true;
            }
        }

        public async Task OpenFileAsync(CloudFile file)
        {
            var tempFolder = ApplicationData.Current.TemporaryFolder;
            var localFile  = await tempFolder.CreateFileAsync(
                file.Name, CreationCollisionOption.ReplaceExisting);

            using (var dl = await _webDav.DownloadFileAsync(file.Path))
            using (var ras = await localFile.OpenAsync(FileAccessMode.ReadWrite))
            using (var fs  = ras.AsStreamForWrite())
                await dl.CopyToAsync(fs);

            await Windows.System.Launcher.LaunchFileAsync(localFile);
        }

        public async Task DownloadToDeviceAsync(CloudFile file, StorageFile destFile)
        {
            using (var dl  = await _webDav.DownloadFileAsync(file.Path))
            using (var ras = await destFile.OpenAsync(FileAccessMode.ReadWrite))
            using (var fs  = ras.AsStreamForWrite())
                await dl.CopyToAsync(fs);
        }

        public async Task RenameAsync(CloudFile file, string newName)
        {
            var parentPath = file.Path.Contains("/")
                ? file.Path.Substring(0, file.Path.TrimEnd('/').LastIndexOf('/'))
                : "/";
            var destPath = $"{parentPath.TrimEnd('/')}/{newName}";
            try
            {
                if (!await _webDav.MoveFileAsync(file.Path, destPath))
                    throw new Exception("Rename failed.");
            }
            catch (HttpRequestException)
            {
                await _queue.EnqueueAsync(new OperationEntity
                {
                    OperationType = "rename",
                    SourcePath    = file.Path,
                    DestPath      = destPath
                });
            }
        }

        public async Task<bool> SetFavoriteAsync(CloudFile file, bool favorite)
        {
            var value = favorite ? "1" : "0";
            var path  = file.Path.StartsWith("/") ? file.Path : "/" + file.Path;
            var req   = new HttpRequestMessage(
                new HttpMethod("PROPPATCH"),
                $"{_settings.ServerUrl}/remote.php/dav/files/{_settings.Username}{path}");
            req.Content = new StringContent(
                $@"<?xml version=""1.0""?>
<d:propertyupdate xmlns:d=""DAV:"" xmlns:oc=""http://owncloud.org/ns"">
  <d:set><d:prop><oc:favorite>{value}</oc:favorite></d:prop></d:set>
</d:propertyupdate>",
                System.Text.Encoding.UTF8, "application/xml");
            var resp = await _nextcloud.GetRawHttpClient().SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        public async Task<string> CreateShareLinkAsync(CloudFile file)
            => await _nextcloud.CreateShareLinkAsync(file.Path);

        public async Task<List<CloudFile>> SearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<CloudFile>();
            return await _webDav.SearchAsync(query) ?? new List<CloudFile>();
        }

        // ── Trashbin ──────────────────────────────────────────────────────────

        public async Task<List<TrashbinFile>> ListTrashbinAsync()
            => await _webDav.ListTrashbinAsync();

        public async Task<bool> RestoreTrashbinFileAsync(TrashbinFile file)
            => await _webDav.RestoreTrashbinFileAsync(file.TrashbinPath, file.OriginalFilename);

        public async Task<bool> DeleteTrashbinPermanentlyAsync(TrashbinFile file)
            => await _webDav.DeleteTrashbinPermanentlyAsync(file.TrashbinPath);

        public async Task<bool> EmptyTrashbinAsync()
            => await _webDav.EmptyTrashbinAsync();

        // ── Copy / Move ───────────────────────────────────────────────────────

        public async Task<Stream> GetDownloadStreamAsync(CloudFile file)
            => await _webDav.DownloadFileAsync(file.Path);

        public async Task<bool> CopyAsync(CloudFile file, string destPath)
            => await _webDav.CopyFileAsync(file.Path, destPath, overwrite: false);

        public async Task<bool> MoveToFolderAsync(CloudFile file, string destFolderPath)
        {
            var destPath = $"{destFolderPath.TrimEnd('/')}/{file.Name}";
            return await _webDav.MoveFileAsync(file.Path, destPath, overwrite: false);
        }

        // ── Notifications / Activities ────────────────────────────────────────

        public async Task<List<Models.NextcloudNotification>> GetNotificationsAsync()
            => await _nextcloud.GetNotificationsAsync();

        public async Task<List<Models.NextcloudActivity>> GetActivitiesAsync()
            => await _nextcloud.GetActivitiesAsync();

        // ── Shares ────────────────────────────────────────────────────────────

        public async Task<List<ShareInfo>> GetSharesForFileAsync(CloudFile file)
            => await _nextcloud.GetSharesForFileAsync(file.Path);

        public async Task<ShareInfo> CreateUserShareAsync(CloudFile file, string userId, int permissions = 17)
            => await _nextcloud.CreateUserShareAsync(file.Path, userId, permissions);

        public async Task<ShareInfo> CreateGroupShareAsync(CloudFile file, string groupId, int permissions = 17)
            => await _nextcloud.CreateGroupShareAsync(file.Path, groupId, permissions);

        public async Task<bool> DeleteShareAsync(int shareId)
            => await _nextcloud.DeleteShareAsync(shareId);

        public async Task<bool> UpdateSharePermissionsAsync(int shareId, int permissions)
            => await _nextcloud.UpdateSharePermissionsAsync(shareId, permissions);

        public async Task<List<(string id, string displayName)>> SearchUsersAsync(string query)
            => await _nextcloud.SearchUsersAsync(query);

        public async Task<List<string>> SearchGroupsAsync(string query)
            => await _nextcloud.SearchGroupsAsync(query);

        // ── Comments ─────────────────────────────────────────────────────────

        public async Task<List<FileComment>> GetCommentsAsync(CloudFile file)
        {
            if (string.IsNullOrEmpty(file.RemoteId)) return new List<FileComment>();
            return await _nextcloud.GetCommentsAsync(file.RemoteId);
        }

        public async Task<bool> PostCommentAsync(CloudFile file, string message)
        {
            if (string.IsNullOrEmpty(file.RemoteId)) return false;
            return await _nextcloud.PostCommentAsync(file.RemoteId, message);
        }

        // ── Offline queue ─────────────────────────────────────────────────────

        public async Task<List<OperationEntity>> GetPendingOperationsAsync()
            => await _queue.GetPendingAsync();

        public async Task ProcessOfflineQueueAsync()
            => await _queue.ProcessQueueAsync(_webDav);

        // ── Two-way sync ──────────────────────────────────────────────────────

        public async Task<SyncResult> TwoWaySyncAsync(
            StorageFolder localFolder,
            string remotePath,
            IProgress<SyncProgress> progress = null,
            Func<string, Task<ConflictResolution>> onConflict = null)
        {
            var sync = new SyncService();
            return await sync.TwoWaySyncAsync(localFolder, remotePath, progress, onConflict);
        }
    }
}
