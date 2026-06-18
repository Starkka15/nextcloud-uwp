using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Newtonsoft.Json;
using NextcloudUWP.Models;

namespace NextcloudUWP.Services
{
    public class OfflineQueueService
    {
        private const string FileName = "offline_queue.json";
        private static int _idSeed;

        private string FilePath => Path.Combine(
            ApplicationData.Current.LocalFolder.Path, FileName);

        private async Task<List<OperationEntity>> LoadAsync()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<OperationEntity>();
                var folder = await StorageFolder.GetFolderFromPathAsync(
                    ApplicationData.Current.LocalFolder.Path);
                var file = await folder.GetFileAsync(FileName);
                var json = await FileIO.ReadTextAsync(file);
                var ops = JsonConvert.DeserializeObject<List<OperationEntity>>(json)
                    ?? new List<OperationEntity>();
                if (ops.Count > 0)
                    _idSeed = Math.Max(_idSeed, ops.Max(o => o.Id) + 1);
                return ops;
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(OfflineQueueService), ex); return new List<OperationEntity>(); }
        }

        private async Task PersistAsync(List<OperationEntity> ops)
        {
            try
            {
                var folder = await StorageFolder.GetFolderFromPathAsync(
                    ApplicationData.Current.LocalFolder.Path);
                var file = await folder.CreateFileAsync(FileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, JsonConvert.SerializeObject(ops));
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(OfflineQueueService), ex); }
        }

        public async Task EnqueueAsync(OperationEntity op)
        {
            var ops = await LoadAsync();
            op.Id       = _idSeed++;
            op.QueuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            op.Status   = "pending";
            ops.Add(op);
            await PersistAsync(ops);
        }

        public async Task<List<OperationEntity>> GetPendingAsync()
        {
            var ops = await LoadAsync();
            return ops.FindAll(o => o.Status == "pending");
        }

        public async Task MarkCompleteAsync(int id)
        {
            var ops = await LoadAsync();
            ops.RemoveAll(o => o.Id == id);
            await PersistAsync(ops);
        }

        public async Task MarkFailedAsync(int id, string error)
        {
            var ops = await LoadAsync();
            var op  = ops.Find(o => o.Id == id);
            if (op != null) { op.Status = "failed"; op.ErrorMessage = error; }
            await PersistAsync(ops);
        }

        public async Task ClearFailedAsync()
        {
            var ops = await LoadAsync();
            ops.RemoveAll(o => o.Status == "failed");
            await PersistAsync(ops);
        }

        public async Task ProcessQueueAsync(WebDavClient dav)
        {
            var pending = await GetPendingAsync();
            foreach (var op in pending)
            {
                try
                {
                    bool ok = false;
                    switch (op.OperationType)
                    {
                        case "delete":
                            ok = await dav.DeleteFileAsync(op.SourcePath); break;
                        case "rename":
                        case "move":
                            ok = await dav.MoveFileAsync(op.SourcePath, op.DestPath); break;
                        case "mkdir":
                            ok = await dav.CreateFolderAsync(op.SourcePath); break;
                        case "upload":
                            if (!string.IsNullOrEmpty(op.LocalFilePath) &&
                                File.Exists(op.LocalFilePath))
                            {
                                using (var s = File.OpenRead(op.LocalFilePath))
                                    ok = await dav.UploadFileAsync(op.SourcePath, s,
                                        "application/octet-stream");
                            }
                            break;
                    }
                    if (ok) await MarkCompleteAsync(op.Id);
                    else    await MarkFailedAsync(op.Id, "Operation returned false");
                }
                catch (Exception ex) { await MarkFailedAsync(op.Id, ex.Message); }
            }
        }
    }
}
