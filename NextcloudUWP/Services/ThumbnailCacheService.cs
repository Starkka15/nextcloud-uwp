using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;
using NextcloudUWP.Models;

namespace NextcloudUWP.Services
{
    public class ThumbnailCacheService
    {
        private const int ThumbSize = 64;

        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private string _serverUrl;
        private string _cacheDir;

        public void Configure(string serverUrl, string username, string password)
        {
            _serverUrl = serverUrl.TrimEnd('/');
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        }

        public async Task<BitmapImage> GetThumbnailAsync(CloudFile file)
        {
            if (string.IsNullOrEmpty(_serverUrl)) return null;
            if (!file.HasPreview && !file.IsImage) return null;

            try
            {
                var dir  = GetCacheDir();
                var key  = CacheKey(file.Path);
                var path = System.IO.Path.Combine(dir, key + ".jpg");

                if (File.Exists(path))
                    return await LoadBitmapAsync(path);

                var url = $"{_serverUrl}/index.php/apps/files/api/v1/thumbnail/{ThumbSize}/{ThumbSize}{Uri.EscapeUriString(file.Path)}";
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;

                var bytes = await resp.Content.ReadAsByteArrayAsync();
                File.WriteAllBytes(path, bytes);

                using (var ms = new MemoryStream(bytes))
                {
                    var bmp = new BitmapImage();
                    await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                    return bmp;
                }
            }
            catch { return null; }
        }

        private string GetCacheDir()
        {
            if (_cacheDir != null) return _cacheDir;
            _cacheDir = System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "thumbcache");
            Directory.CreateDirectory(_cacheDir);
            return _cacheDir;
        }

        private static string CacheKey(string path)
        {
            var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(path ?? "/"))
                .Replace('/', '_').Replace('+', '-').Replace('=', '~');
            return b64.Length > 80 ? b64.Substring(b64.Length - 80) : b64;
        }

        private static async Task<BitmapImage> LoadBitmapAsync(string filePath)
        {
            var bmp = new BitmapImage();
            using (var fs = File.OpenRead(filePath))
                await bmp.SetSourceAsync(fs.AsRandomAccessStream());
            return bmp;
        }
    }
}
