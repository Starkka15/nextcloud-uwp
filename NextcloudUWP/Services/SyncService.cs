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
        private readonly WebDavClient _webDav;
        private readonly SettingsService _settings;

        public SyncService()
        {
            _settings = new SettingsService();
            _webDav   = new WebDavClient();
            if (_settings.HasCredentials)
                _webDav.Configure(_settings.ServerUrl, _settings.Username, _settings.Password);
        }

        // ── Upload-only (auto-upload of new local photos/videos) ─────────────

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

            try { await _webDav.CreateFolderAsync(remotePath); } catch { }

            var extensions = new[]
            {
                ".jpg", ".jpeg", ".png", ".gif", ".bmp",
                ".mp4", ".mov", ".3gp", ".heic", ".webp"
            };
            var opts = new QueryOptions(CommonFileQuery.DefaultQuery, extensions)
            {
                FolderDepth = FolderDepth.Deep
            };
            var files = await sourceFolder.CreateFileQueryWithOptions(opts).GetFilesAsync();

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
                try
                {
                    using (var ras = await file.OpenAsync(FileAccessMode.Read))
                    using (var stream = ras.AsStreamForRead())
                        await _webDav.UploadFileAsync(
                            $"{remotePath.TrimEnd('/')}/{file.Name}", stream,
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

        // ── Two-way sync ─────────────────────────────────────────────────────

        public async Task<SyncResult> TwoWaySyncAsync(
            StorageFolder localFolder,
            string remotePath,
            IProgress<string> progress = null,
            Func<string, Task<ConflictResolution>> onConflict = null)
        {
            if (!_settings.HasCredentials)
                throw new InvalidOperationException("No account configured.");

            var result = new SyncResult();
            progress?.Report($"Syncing {remotePath}…");

            // Ensure remote folder exists
            try { await _webDav.CreateFolderAsync(remotePath); } catch { }

            // Get remote listing
            List<CloudFile> remoteFiles;
            try
            {
                remoteFiles = await _webDav.ListFilesAsync(remotePath);
                if (remoteFiles?.Count > 0) remoteFiles.RemoveAt(0); // strip parent entry
                remoteFiles = remoteFiles ?? new List<CloudFile>();
            }
            catch
            {
                result.Errors++;
                return result;
            }

            // Get local files
            var localFiles = await localFolder.GetFilesAsync();
            var localNames = new HashSet<string>(
                localFiles.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
            var remoteNames = new HashSet<string>(
                remoteFiles.Where(f => !f.IsFolder).Select(f => f.Name),
                StringComparer.OrdinalIgnoreCase);

            // Remote → local: download files not present locally
            foreach (var remote in remoteFiles.Where(f => !f.IsFolder))
            {
                if (localNames.Contains(remote.Name))
                {
                    // Both exist — check ETag vs local modification
                    var localFile = localFiles.FirstOrDefault(f =>
                        string.Equals(f.Name, remote.Name, StringComparison.OrdinalIgnoreCase));
                    if (localFile != null)
                    {
                        var localProps = await localFile.GetBasicPropertiesAsync();
                        if (localProps.DateModified.DateTime > remote.ModifiedDate)
                        {
                            // Local is newer — upload
                            progress?.Report($"↑ {remote.Name}");
                            try
                            {
                                using (var ras = await localFile.OpenAsync(FileAccessMode.Read))
                                using (var stream = ras.AsStreamForRead())
                                    await _webDav.UploadFileAsync(
                                        $"{remotePath.TrimEnd('/')}/{remote.Name}", stream,
                                        localFile.ContentType ?? "application/octet-stream");
                                result.Uploaded++;
                            }
                            catch { result.Errors++; }
                        }
                        else if (remote.ModifiedDate > localProps.DateModified.DateTime.AddSeconds(5))
                        {
                            // Remote is newer — conflict if local was also modified, else download
                            var cutoff = DateTime.UtcNow.AddDays(-1);
                            if (localProps.DateModified.DateTime > cutoff && onConflict != null)
                            {
                                var resolution = await onConflict(remote.Name);
                                if (resolution == ConflictResolution.KeepRemote)
                                    await DownloadFileAsync(remote, localFolder, progress, result);
                                else if (resolution == ConflictResolution.KeepLocal)
                                {
                                    progress?.Report($"↑ {remote.Name} (keep local)");
                                    try
                                    {
                                        using (var ras = await localFile.OpenAsync(FileAccessMode.Read))
                                        using (var stream = ras.AsStreamForRead())
                                            await _webDav.UploadFileAsync(
                                                $"{remotePath.TrimEnd('/')}/{remote.Name}", stream,
                                                localFile.ContentType ?? "application/octet-stream");
                                        result.Uploaded++;
                                    }
                                    catch { result.Errors++; }
                                }
                                else if (resolution == ConflictResolution.SaveBoth)
                                {
                                    // Rename local to conflict copy then download remote
                                    var conflictName = AddConflictSuffix(remote.Name);
                                    try { await localFile.RenameAsync(conflictName); } catch { }
                                    await DownloadFileAsync(remote, localFolder, progress, result);
                                }
                            }
                            else
                            {
                                await DownloadFileAsync(remote, localFolder, progress, result);
                            }
                        }
                        // else: same age, skip
                    }
                }
                else
                {
                    // Remote only → download
                    await DownloadFileAsync(remote, localFolder, progress, result);
                }
            }

            // Local → remote: upload files not present remotely
            foreach (var localFile in localFiles)
            {
                if (!remoteNames.Contains(localFile.Name))
                {
                    progress?.Report($"↑ {localFile.Name}");
                    try
                    {
                        using (var ras = await localFile.OpenAsync(FileAccessMode.Read))
                        using (var stream = ras.AsStreamForRead())
                            await _webDav.UploadFileAsync(
                                $"{remotePath.TrimEnd('/')}/{localFile.Name}", stream,
                                localFile.ContentType ?? "application/octet-stream");
                        result.Uploaded++;
                    }
                    catch { result.Errors++; }
                }
            }

            _settings.AutoUploadLastSync = DateTime.UtcNow.ToString("O");
            progress?.Report("Sync complete.");
            return result;
        }

        private async Task DownloadFileAsync(CloudFile remote, StorageFolder localFolder,
            IProgress<string> progress, SyncResult result)
        {
            progress?.Report($"↓ {remote.Name}");
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
            catch { result.Errors++; }
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

    public enum ConflictResolution { KeepRemote, KeepLocal, SaveBoth }
}
