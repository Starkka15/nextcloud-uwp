using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Newtonsoft.Json;
using NextcloudUWP.Models;

namespace NextcloudUWP.Services
{
    public class CacheService
    {
        private const string DirName = "filecache";

        private string GetDir()
        {
            var dir = Path.Combine(ApplicationData.Current.LocalFolder.Path, DirName);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string PathToKey(string path)
        {
            var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(path ?? "/"))
                .Replace('/', '_').Replace('+', '-').Replace('=', '~');
            return b64.Length > 80 ? b64.Substring(b64.Length - 80) : b64;
        }

        public async Task SaveAsync(string path, List<CloudFile> files)
        {
            try
            {
                var dir  = GetDir();
                var name = PathToKey(path) + ".json";
                var folder = await StorageFolder.GetFolderFromPathAsync(dir);
                var file = await folder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, JsonConvert.SerializeObject(files));
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(CacheService), ex); }
        }

        public async Task<List<CloudFile>> LoadAsync(string path)
        {
            try
            {
                var dir  = GetDir();
                var name = PathToKey(path) + ".json";
                if (!File.Exists(Path.Combine(dir, name))) return null;
                var folder = await StorageFolder.GetFolderFromPathAsync(dir);
                var file = await folder.GetFileAsync(name);
                var json = await FileIO.ReadTextAsync(file);
                return JsonConvert.DeserializeObject<List<CloudFile>>(json);
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(CacheService), ex); return null; }
        }

        public void Invalidate(string path)
        {
            try
            {
                var p = Path.Combine(GetDir(), PathToKey(path) + ".json");
                if (File.Exists(p)) File.Delete(p);
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(CacheService), ex); }
        }
    }
}
