using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using NextcloudUWP.Models;
using NextcloudUWP.Services;

namespace NextcloudUWP.ViewModels
{
    public class MainViewModel
    {
        private readonly WebDavClient _webDav;
        private readonly NextcloudClient _nextcloud;
        private readonly SettingsService _settings;

        public string CurrentPath { get; private set; } = "/";

        public MainViewModel()
        {
            _settings = new SettingsService();
            _webDav = new WebDavClient();
            _nextcloud = new NextcloudClient();
            Reconfigure();
        }

        public void Reconfigure()
        {
            if (_settings.HasCredentials)
            {
                _webDav.Configure(_settings.ServerUrl, _settings.Username, _settings.Password);
                _nextcloud.Configure(_settings.ServerUrl, _settings.Username, _settings.Password);
            }
        }

        public async Task<List<CloudFile>> GetFilesAsync(string path)
        {
            CurrentPath = path;
            var files = await _webDav.ListFilesAsync(path);
            if (files != null && files.Count > 0)
                files.RemoveAt(0);
            return files ?? new List<CloudFile>();
        }

        public async Task UploadFileAsync(StorageFile file, string remotePath)
        {
            using (var ras = await file.OpenReadAsync())
            using (var stream = ras.AsStreamForRead())
            {
                var fullPath = $"{remotePath.TrimEnd('/')}/{file.Name}";
                var ok = await _webDav.UploadFileAsync(fullPath, stream,
                    file.ContentType ?? "application/octet-stream");
                if (!ok) throw new Exception("Upload failed.");
            }
        }

        public async Task CreateFolderAsync(string name, string parentPath)
        {
            var fullPath = $"{parentPath.TrimEnd('/')}/{name}";
            if (!await _webDav.CreateFolderAsync(fullPath))
                throw new Exception("Failed to create folder.");
        }

        public async Task<bool> DeleteFileAsync(CloudFile file)
        {
            return await _webDav.DeleteFileAsync(file.Path);
        }

        public async Task OpenFileAsync(CloudFile file)
        {
            var tempFolder = ApplicationData.Current.TemporaryFolder;
            var localFile = await tempFolder.CreateFileAsync(file.Name,
                CreationCollisionOption.ReplaceExisting);

            using (var downloadStream = await _webDav.DownloadFileAsync(file.Path))
            using (var ras = await localFile.OpenAsync(FileAccessMode.ReadWrite))
            using (var fileStream = ras.AsStreamForWrite())
            {
                await downloadStream.CopyToAsync(fileStream);
            }
            await Windows.System.Launcher.LaunchFileAsync(localFile);
        }

        public async Task DownloadToDeviceAsync(CloudFile file, StorageFile destFile)
        {
            using (var downloadStream = await _webDav.DownloadFileAsync(file.Path))
            using (var ras = await destFile.OpenAsync(FileAccessMode.ReadWrite))
            using (var fileStream = ras.AsStreamForWrite())
            {
                await downloadStream.CopyToAsync(fileStream);
            }
        }

        public async Task RenameAsync(CloudFile file, string newName)
        {
            var parentPath = file.Path.Contains("/")
                ? file.Path.Substring(0, file.Path.TrimEnd('/').LastIndexOf('/'))
                : "/";
            var destPath = $"{parentPath.TrimEnd('/')}/{newName}";
            if (!await _webDav.MoveFileAsync(file.Path, destPath))
                throw new Exception("Rename failed.");
        }

        public async Task<bool> SetFavoriteAsync(CloudFile file, bool favorite)
        {
            var value = favorite ? "1" : "0";
            var path = file.Path.StartsWith("/") ? file.Path : "/" + file.Path;
            var request = new System.Net.Http.HttpRequestMessage(
                new System.Net.Http.HttpMethod("PROPPATCH"),
                $"{_settings.ServerUrl}/remote.php/dav/files/{_settings.Username}{path}");
            request.Content = new System.Net.Http.StringContent(
                $@"<?xml version=""1.0""?>
<d:propertyupdate xmlns:d=""DAV:"" xmlns:oc=""http://owncloud.org/ns"">
  <d:set><d:prop><oc:favorite>{value}</oc:favorite></d:prop></d:set>
</d:propertyupdate>",
                System.Text.Encoding.UTF8, "application/xml");
            var response = await _nextcloud.GetRawHttpClient().SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        public async Task<string> CreateShareLinkAsync(CloudFile file)
        {
            return await _nextcloud.CreateShareLinkAsync(file.Path);
        }

        public async Task<List<CloudFile>> SearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<CloudFile>();
            var results = await _webDav.SearchAsync(query);
            return results ?? new List<CloudFile>();
        }

        public async Task<List<TrashbinFile>> ListTrashbinAsync()
        {
            return await _webDav.ListTrashbinAsync();
        }

        public async Task<bool> RestoreTrashbinFileAsync(TrashbinFile file)
        {
            return await _webDav.RestoreTrashbinFileAsync(
                file.TrashbinPath, file.OriginalFilename);
        }

        public async Task<bool> DeleteTrashbinPermanentlyAsync(TrashbinFile file)
        {
            return await _webDav.DeleteTrashbinPermanentlyAsync(file.TrashbinPath);
        }

        public async Task<bool> EmptyTrashbinAsync()
        {
            return await _webDav.EmptyTrashbinAsync();
        }

        public async Task<System.IO.Stream> GetDownloadStreamAsync(CloudFile file)
        {
            return await _webDav.DownloadFileAsync(file.Path);
        }

        public async Task<bool> CopyAsync(CloudFile file, string destPath)
        {
            return await _webDav.CopyFileAsync(file.Path, destPath, overwrite: false);
        }

        public async Task<bool> MoveToFolderAsync(CloudFile file, string destFolderPath)
        {
            var destPath = $"{destFolderPath.TrimEnd('/')}/{file.Name}";
            return await _webDav.MoveFileAsync(file.Path, destPath, overwrite: false);
        }

        public async Task<List<Models.NextcloudNotification>> GetNotificationsAsync()
        {
            return await _nextcloud.GetNotificationsAsync();
        }

        public async Task<List<Models.NextcloudActivity>> GetActivitiesAsync()
        {
            return await _nextcloud.GetActivitiesAsync();
        }
    }
}
