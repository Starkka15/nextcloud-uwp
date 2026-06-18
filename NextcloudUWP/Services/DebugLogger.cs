using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace NextcloudUWP.Services
{
    public static class DebugLogger
    {
        private const string LogFile = "debug.log";
        private static readonly object _lock = new object();

        public static void Log(string source, string message)
        {
            try
            {
                lock (_lock)
                {
                    var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [{source}] {message}";
                    var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, LogFile);
                    File.AppendAllText(path, line + Environment.NewLine);
                }
            }
            catch { }
        }

        public static void LogException(string source, Exception ex)
        {
            Log(source, $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
