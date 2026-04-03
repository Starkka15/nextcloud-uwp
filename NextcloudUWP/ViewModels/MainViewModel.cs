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
            {
                files.RemoveAt(0);
            }

            return files ?? new List<CloudFile>();
        }

        public async Task UploadFileAsync(StorageFile file, string remotePath)
        {
            using (var randomAccessStream = await file.OpenReadAsync())
            using (var stream = randomAccessStream.AsStreamForRead())
            {
                var fullPath = $"{remotePath.TrimEnd('/')}/{file.Name}";
                var contentType = file.ContentType ?? "application/octet-stream";
                var success = await _webDav.UploadFileAsync(fullPath, stream, contentType);
                if (!success)
                {
                    throw new Exception("Upload failed.");
                }
            }
        }

        public async Task CreateFolderAsync(string name, string parentPath)
        {
            var fullPath = $"{parentPath.TrimEnd('/')}/{name}";
            var success = await _webDav.CreateFolderAsync(fullPath);
            if (!success)
            {
                throw new Exception("Failed to create folder.");
            }
        }

        public async Task<bool> DeleteFileAsync(CloudFile file)
        {
            return await _webDav.DeleteFileAsync(file.Path);
        }

        public async Task OpenFileAsync(CloudFile file)
        {
            var tempFolder = ApplicationData.Current.TemporaryFolder;
            var localFile = await tempFolder.CreateFileAsync(file.Name, CreationCollisionOption.ReplaceExisting);

            using (var downloadStream = await _webDav.DownloadFileAsync(file.Path))
            using (var randomAccessStream = await localFile.OpenAsync(FileAccessMode.ReadWrite))
            using (var fileStream = randomAccessStream.AsStreamForWrite())
            {
                await downloadStream.CopyToAsync(fileStream);
            }

            await Windows.System.Launcher.LaunchFileAsync(localFile);
        }

        public async Task<bool> ToggleFavoriteAsync(CloudFile file)
        {
            return await _nextcloud.CreateShareLinkAsync(file.Path);
        }
    }
}
