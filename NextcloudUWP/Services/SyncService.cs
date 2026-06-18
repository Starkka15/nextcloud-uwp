using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;
using NextcloudUWP.Models;

namespace NextcloudUWP.Services
{
    public class SyncService
    {
        private const long DefaultMaxFileSize = 512 * 1024 * 1024;

        private readonly WebDavClient _webDav;
        private readonly SettingsService _settings;

        public SyncService()
        {
            _settings = new SettingsService();
            _webDav   = new WebDavClient();
            if (_settings.HasCredentials)
                _webDav.Configure(_settings.ServerUrl, _settings.Username, _settings.Password);
        }

        public long MaxFileSize
        {
            get
            {
                var val = _settings.SyncMaxFileSize;
                return val > 0 ? val : DefaultMaxFileSize;
            }
        }

        // ── Recursive folder upload ──────────────────────────────────────────

        public async Task<(int uploaded, int skipped, int failed)> UploadFolderAsync(
            StorageFolder sourceFolder,
            string remotePath,
            IProgress<(int done, int total)> progress = null)
        {
            if (!_settings.HasCredentials)
                throw new InvalidOperationException("No account configured.");

            var lastSync = DateTime.MinValue;
            var lastSyncStr = _settings.AutoUploadLastSync;
            if (!string.IsNullOrEmpty(lastSyncStr))
                DateTime.TryParse(lastSyncStr, out lastSync);

            var extensions = _settings.SyncFileExtensions;
            var opts = new QueryOptions(
                extensions.Count > 0
                    ? CommonFileQuery.DefaultQuery
                    : CommonFileQuery.DefaultQuery,
                extensions.Count > 0 ? extensions.ToArray() : new[] { "*" })
            {
                FolderDepth = FolderDepth.Deep
            };
            if (extensions.Count == 0) opts.FileTypeFilter.Add("*");

            var allFiles = await sourceFolder.CreateFileQueryWithOptions(opts).GetFilesAsync();
            var maxSize = MaxFileSize;

            var files = allFiles.Where(f =>
            {
                try
                {
                    var props = f.GetBasicPropertiesAsync().AsTask().Result;
                    return props.Size <= (ulong)maxSize;
                }
                catch { return false; }
            }).ToList();

            int uploaded = 0, skipped = 0, failed = 0, done = 0, total = files.Count;

            foreach (var file in files)
            {
                var props = await file.GetBasicPropertiesAsync();
                if (props.DateModified.DateTime <= lastSync)
                {
                    skipped++; done++;
                    progress?.Report((done, total));
                    continue;
                }

                var relativePath = GetRelativePath(sourceFolder, file);
                var destFolderPath = $"{remotePath.TrimEnd('/')}/{Path.GetDirectoryName(relativePath)?.Replace('\\', '/')}";

                try { await EnsureRemoteFolderAsync(destFolderPath); }
                catch (Exception ex) { DebugLogger.Log(nameof(SyncService), $"EnsureRemoteFolder {destFolderPath}: {ex.Message}"); }

                try
                {
                    var fullPath = $"{remotePath.TrimEnd('/')}/{relativePath.Replace('\\', '/')}";
                    using (var ras = await file.OpenAsync(FileAccessMode.Read))
                    using (var stream = ras.AsStreamForRead())
                        await _webDav.UploadFileAsync(fullPath, stream,
                            file.ContentType ?? "application/octet-stream");
                    uploaded++;
                }
                catch (Exception ex)
                {
                    DebugLogger.LogException(nameof(SyncService), ex);
                    failed++;
                }
                done++;
                progress?.Report((done, total));
            }

            _settings.AutoUploadLastSync = DateTime.UtcNow.ToString("O");
            return (uploaded, skipped, failed);
        }

        // ── Recursive two-way sync ────────────────────────────────────────────

        public async Task<SyncResult> TwoWaySyncAsync(
            StorageFolder localFolder,
            string remotePath,
            IProgress<SyncProgress> progress = null,
            Func<string, Task<ConflictResolution>> onConflict = null)
        {
            if (!_settings.HasCredentials)
                throw new InvalidOperationException("No account configured.");

            var result = new SyncResult();
            await TwoWaySyncFolderAsync(localFolder, remotePath, result, progress, onConflict);
            _settings.AutoUploadLastSync = DateTime.UtcNow.ToString("O");
            progress?.Report(new SyncProgress { Phase = "complete", CurrentFile = null });
            return result;
        }

        private async Task TwoWaySyncFolderAsync(
            StorageFolder localFolder,
            string remotePath,
            SyncResult result,
            IProgress<SyncProgress> progress,
            Func<string, Task<ConflictResolution>> onConflict,
            string relativePath = "")
        {
            progress?.Report(new SyncProgress
            {
                Phase = "scanning",
                CurrentFolder = string.IsNullOrEmpty(relativePath) ? "/" : relativePath
            });

            try { await EnsureRemoteFolderAsync(remotePath); }
            catch (Exception ex) { DebugLogger.Log(nameof(SyncService), $"EnsureRemoteFolder {remotePath}: {ex.Message}"); }

            List<CloudFile> remoteFiles;
            try
            {
                remoteFiles = await _webDav.ListFilesAsync(remotePath);
                if (remoteFiles?.Count > 0) remoteFiles.RemoveAt(0);
                remoteFiles = remoteFiles ?? new List<CloudFile>();
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(nameof(SyncService), ex);
                result.Errors++;
                return;
            }

            var localFiles = await localFolder.GetFilesAsync();
            var localFolders = await localFolder.GetFoldersAsync();
            var maxSize = MaxFileSize;

            var localNames = new HashSet<string>(
                localFiles.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
            var remoteFileNames = new HashSet<string>(
                remoteFiles.Where(f => !f.IsFolder).Select(f => f.Name),
                StringComparer.OrdinalIgnoreCase);

            // Remote → local: download files not present locally
            foreach (var remote in remoteFiles.Where(f => !f.IsFolder))
            {
                var displayPath = string.IsNullOrEmpty(relativePath)
                    ? remote.Name : $"{relativePath}/{remote.Name}";
                if (remote.Size > maxSize)
                {
                    result.Skipped++;
                    continue;
                }

                if (localNames.Contains(remote.Name))
                {
                    var localFile = localFiles.FirstOrDefault(f =>
                        string.Equals(f.Name, remote.Name, StringComparison.OrdinalIgnoreCase));
                    if (localFile != null)
                    {
                        var localProps = await localFile.GetBasicPropertiesAsync();
                        if (localProps.DateModified.DateTime > remote.ModifiedDate)
                        {
                            progress?.Report(new SyncProgress { Phase = "uploading", CurrentFile = displayPath });
                            try
                            {
                                await UploadLocalFileAsync(localFile, $"{remotePath.TrimEnd('/')}/{remote.Name}");
                                result.Uploaded++;
                            }
                            catch (Exception ex) { DebugLogger.LogException(nameof(SyncService), ex); result.Errors++; }
                        }
                        else if (remote.ModifiedDate > localProps.DateModified.DateTime.AddSeconds(5))
                        {
                            var cutoff = DateTime.UtcNow.AddDays(-1);
                            if (localProps.DateModified.DateTime > cutoff && onConflict != null)
                            {
                                var resolution = await onConflict(displayPath);
                                if (resolution == ConflictResolution.KeepRemote)
                                    await DownloadFileAsync(remote, localFolder, result);
                                else if (resolution == ConflictResolution.KeepLocal)
                                {
                                    progress?.Report(new SyncProgress { Phase = "uploading", CurrentFile = displayPath });
                                    try
                                    {
                                        await UploadLocalFileAsync(localFile, $"{remotePath.TrimEnd('/')}/{remote.Name}");
                                        result.Uploaded++;
                                    }
                                    catch (Exception ex) { DebugLogger.LogException(nameof(SyncService), ex); result.Errors++; }
                                }
                                else if (resolution == ConflictResolution.SaveBoth)
                                {
                                    var conflictName = AddConflictSuffix(remote.Name);
                                    try { await localFile.RenameAsync(conflictName); }
                                    catch (Exception ex) { DebugLogger.LogException(nameof(SyncService), ex); }
                                    await DownloadFileAsync(remote, localFolder, result);
                                }
                            }
                            else
                            {
                                await DownloadFileAsync(remote, localFolder, result);
                            }
                        }
                    }
                }
                else
                {
                    await DownloadFileAsync(remote, localFolder, result);
                }
            }

            // Local → remote: upload files not present remotely
            foreach (var localFile in localFiles)
            {
                var displayPath = string.IsNullOrEmpty(relativePath)
                    ? localFile.Name : $"{relativePath}/{localFile.Name}";
                if (!remoteFileNames.Contains(localFile.Name))
                {
                    var props = await localFile.GetBasicPropertiesAsync();
                    if (props.Size > (ulong)maxSize) { result.Skipped++; continue; }

                    progress?.Report(new SyncProgress { Phase = "uploading", CurrentFile = displayPath });
                    try
                    {
                        await UploadLocalFileAsync(localFile, $"{remotePath.TrimEnd('/')}/{localFile.Name}");
                        result.Uploaded++;
                    }
                    catch (Exception ex) { DebugLogger.LogException(nameof(SyncService), ex); result.Errors++; }
                }
            }

            // Recurse into subfolders
            var remoteFolderNames = new HashSet<string>(
                remoteFiles.Where(f => f.IsFolder).Select(f => f.Name),
                StringComparer.OrdinalIgnoreCase);

            foreach (var subFolder in localFolders)
            {
                var subRemote = $"{remotePath.TrimEnd('/')}/{subFolder.Name}";
                var subRelative = string.IsNullOrEmpty(relativePath)
                    ? subFolder.Name : $"{relativePath}/{subFolder.Name}";

                if (!remoteFolderNames.Contains(subFolder.Name))
                {
                    try { await EnsureRemoteFolderAsync(subRemote); }
                    catch (Exception ex) { DebugLogger.Log(nameof(SyncService), $"CreateFolder {subRemote}: {ex.Message}"); }
                }

                await TwoWaySyncFolderAsync(subFolder, subRemote, result, progress, onConflict, subRelative);
            }

            // Download remote-only subfolders
            foreach (var remoteSub in remoteFiles.Where(f => f.IsFolder))
            {
                if (!remoteFolderNames.Contains(remoteSub.Name)) continue;
                bool localExists = localFolders.Any(f =>
                    string.Equals(f.Name, remoteSub.Name, StringComparison.OrdinalIgnoreCase));
                if (!localExists)
                {
                    var subRelative = string.IsNullOrEmpty(relativePath)
                        ? remoteSub.Name : $"{relativePath}/{remoteSub.Name}";
                    var newLocalFolder = await localFolder.CreateFolderAsync(
                        remoteSub.Name, CreationCollisionOption.OpenIfExists);
                    var subRemote = $"{remotePath.TrimEnd('/')}/{remoteSub.Name}";
                    await TwoWaySyncFolderAsync(newLocalFolder, subRemote, result, progress, onConflict, subRelative);
                }
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private async Task EnsureRemoteFolderAsync(string path)
        {
            await _webDav.CreateFolderAsync(path);
        }

        private async Task UploadLocalFileAsync(StorageFile file, string remotePath)
        {
            using (var ras = await file.OpenAsync(FileAccessMode.Read))
            using (var stream = ras.AsStreamForRead())
                await _webDav.UploadFileAsync(remotePath, stream,
                    file.ContentType ?? "application/octet-stream");
        }

        private async Task DownloadFileAsync(CloudFile remote, StorageFolder localFolder,
            SyncResult result)
        {
            try
            {
                var localFile = await localFolder.CreateFileAsync(
                    remote.Name, CreationCollisionOption.ReplaceExisting);
                using (var downloadStream = await _webDav.DownloadFileAsync(remote.Path))
                using (var ras = await localFile.OpenAsync(FileAccessMode.ReadWrite))
                using (var fileStream = ras.AsStreamForWrite())
                    await downloadStream.CopyToAsync(fileStream);
                result.Downloaded++;
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(SyncService), ex); result.Errors++; }
        }

        private static string GetRelativePath(StorageFolder root, StorageFile file)
        {
            var rootPath = root.Path.TrimEnd('\\', '/');
            var filePath = file.Path;
            if (filePath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            {
                var rel = filePath.Substring(rootPath.Length).TrimStart('\\', '/');
                return rel;
            }
            return file.Name;
        }

        private static string AddConflictSuffix(string name)
        {
            var ext  = Path.GetExtension(name);
            var stem = Path.GetFileNameWithoutExtension(name);
            return $"{stem} (conflict {DateTime.Now:yyyyMMdd-HHmmss}){ext}";
        }
    }

    public class SyncResult
    {
        public int Uploaded   { get; set; }
        public int Downloaded { get; set; }
        public int Skipped    { get; set; }
        public int Errors     { get; set; }
        public override string ToString() =>
            $"↑{Uploaded} ↓{Downloaded} ={Skipped} ✕{Errors}";
    }

    public class SyncProgress
    {
        public string Phase        { get; set; }
        public string CurrentFile  { get; set; }
        public string CurrentFolder { get; set; }
    }

    public enum ConflictResolution { KeepRemote, KeepLocal, SaveBoth }
}
