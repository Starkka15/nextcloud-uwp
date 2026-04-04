using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

namespace NextcloudUWP.Services
{
    public static class BackgroundTaskManager
    {
        public const string NotificationTaskName = "NextcloudNotificationPolling";
        public const string SyncTaskName         = "NextcloudAutoSync";

        // Call once on first launch (or when user enables a toggle).
        // Returns false if the OS denied access (e.g. battery saver always-on).
        public static async Task<bool> RequestAccessAsync()
        {
            var status = await BackgroundExecutionManager.RequestAccessAsync();
            return status == BackgroundAccessStatus.AlwaysAllowed
                || status == BackgroundAccessStatus.AllowedSubjectToSystemPolicy;
        }

        public static void RegisterNotificationPolling()
        {
            if (IsRegistered(NotificationTaskName)) return;
            var builder = new BackgroundTaskBuilder
            {
                Name = NotificationTaskName,
                IsNetworkRequested = true
                // No TaskEntryPoint → in-process task (routed via App.OnBackgroundActivated)
            };
            builder.SetTrigger(new TimeTrigger(15, false)); // minimum 15 min on UWP
            builder.AddCondition(new SystemCondition(SystemConditionType.InternetAvailable));
            builder.Register();
        }

        public static void RegisterAutoSync()
        {
            if (IsRegistered(SyncTaskName)) return;
            var builder = new BackgroundTaskBuilder
            {
                Name = SyncTaskName,
                IsNetworkRequested = true
            };
            builder.SetTrigger(new TimeTrigger(30, false)); // every 30 min
            builder.AddCondition(new SystemCondition(SystemConditionType.InternetAvailable));
            builder.Register();
        }

        public static void Unregister(string name)
        {
            var task = BackgroundTaskRegistration.AllTasks.Values
                .FirstOrDefault(t => t.Name == name);
            task?.Unregister(false);
        }

        private static bool IsRegistered(string name)
            => BackgroundTaskRegistration.AllTasks.Values.Any(t => t.Name == name);
    }
}
