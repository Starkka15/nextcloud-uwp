using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.Data.Xml.Dom;
using Windows.Storage.AccessCache;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace NextcloudUWP
{
    sealed partial class App : Application
    {
        public static Frame RootFrame { get; private set; }

        // Singleton MediaPlayer — survives page navigation, keeps audio in background
        private static Windows.Media.Playback.MediaPlayer _appMediaPlayer;
        public static Windows.Media.Playback.MediaPlayer AppMediaPlayer
        {
            get
            {
                if (_appMediaPlayer == null)
                {
                    _appMediaPlayer = new Windows.Media.Playback.MediaPlayer();
                    _appMediaPlayer.AudioCategory =
                        Windows.Media.Playback.MediaPlayerAudioCategory.Media;
                }
                return _appMediaPlayer;
            }
        }

        // True once the user has passed the biometric/PIN lock screen this session.
        public static bool IsUnlocked { get; set; } = true;

        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            this.Resuming   += OnResuming;
        }

        private void OnResuming(object sender, object e)
        {
            // Force re-authentication on next foreground visit when app lock is on.
            var settings = new Services.SettingsService();
            if (settings.AppLockEnabled)
                IsUnlocked = false;
        }

        // ── Launch ──────────────────────────────────────────────────────

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            EnsureRootFrame();

            if (RootFrame.Content == null)
            {
                var settings = new Services.SettingsService();
                settings.MigrateLegacyCredentials();

                if (settings.HasCredentials)
                {
                    _ = RegisterBackgroundTasksAsync(settings);
                    RootFrame.Navigate(typeof(Views.MainPage), e.Arguments);
                }
                else
                {
                    RootFrame.Navigate(typeof(Views.LoginPage));
                }
            }

            Window.Current.Activate();
        }

        // ── Protocol / share-target activation ──────────────────────────

        protected override void OnActivated(IActivatedEventArgs args)
        {
            base.OnActivated(args);
            EnsureRootFrame();

            switch (args.Kind)
            {
                case ActivationKind.Protocol:
                    HandleNcProtocol(((ProtocolActivatedEventArgs)args).Uri);
                    break;

                case ActivationKind.ShareTarget:
                    RootFrame.Navigate(typeof(Views.ShareTargetPage),
                        ((ShareTargetActivatedEventArgs)args).ShareOperation);
                    break;
            }

            Window.Current.Activate();
        }

        private static void HandleNcProtocol(Uri uri)
        {
            // nc://login?server={url}&user={user}&password={appPassword}
            // nc://login/v2/grant?server={url}&user={user}&password={appPassword}
            if (!uri.Host.Equals("login", StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                var q        = new Windows.Foundation.WwwFormUrlDecoder(uri.Query.TrimStart('?'));
                string Get(string key) { try { return q.GetFirstValueByName(key); } catch { return null; } }

                var server   = Get("server");
                var user     = Get("user");
                var password = Get("password");

                if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
                    return;

                var settings = new Services.SettingsService();
                settings.AddAccount(server, user, password);
                IsUnlocked = true;
                RootFrame.Navigate(typeof(Views.MainPage));
            }
            catch { }
        }

        private static void EnsureRootFrame()
        {
            if (RootFrame != null) return;
            RootFrame = new Frame();
            RootFrame.NavigationFailed += (s, ev) =>
                throw new Exception("Failed to load Page " + ev.SourcePageType.FullName);
            Window.Current.Content = RootFrame;
        }

        // ── Background task access ───────────────────────────────────────

        internal static async Task RegisterBackgroundTasksAsync(Services.SettingsService settings)
        {
            bool granted = await Services.BackgroundTaskManager.RequestAccessAsync();
            if (!granted) return;

            if (settings.NotificationsEnabled)
                Services.BackgroundTaskManager.RegisterNotificationPolling();
            else
                Services.BackgroundTaskManager.Unregister(Services.BackgroundTaskManager.NotificationTaskName);

            if (settings.AutoSyncEnabled && !string.IsNullOrEmpty(settings.AutoSyncFolderToken))
                Services.BackgroundTaskManager.RegisterAutoSync();
            else
                Services.BackgroundTaskManager.Unregister(Services.BackgroundTaskManager.SyncTaskName);
        }

        // ── In-process background task dispatcher ────────────────────────

        protected override async void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            base.OnBackgroundActivated(args);
            var deferral = args.TaskInstance.GetDeferral();
            try
            {
                var settings = new Services.SettingsService();
                var account  = settings.GetActiveAccount();
                if (account == null) return;

                var name = args.TaskInstance.Task.Name;

                if (name == Services.BackgroundTaskManager.NotificationTaskName)
                    await RunNotificationPollingAsync(settings, account);
                else if (name == Services.BackgroundTaskManager.SyncTaskName)
                    await RunAutoSyncAsync(settings, account);
            }
            catch { }
            finally { deferral.Complete(); }
        }

        private static async Task RunNotificationPollingAsync(
            Services.SettingsService settings, Models.UserAccount account)
        {
            var client = new Services.NextcloudClient();
            client.Configure(account.ServerUrl, account.Username, account.Password);

            // Always update the live tile with current quota.
            try
            {
                var user = await client.GetUserAsync();
                if (user != null && user.QuotaTotal > 0)
                    Services.TileService.UpdateTile(user.QuotaUsed, user.QuotaTotal);
            }
            catch { }

            if (!settings.NotificationsEnabled) return;

            try
            {
                var notifications = await client.GetNotificationsAsync();
                if (notifications.Count == 0) { Services.TileService.ClearBadge(); return; }

                int lastSeen = settings.LastSeenNotificationId;
                int newCount = 0;

                foreach (var n in notifications.Where(n => n.NotificationId > lastSeen))
                {
                    ShowToast(n.Subject, n.Message);
                    newCount++;
                }

                settings.LastSeenNotificationId = notifications.Max(n => n.NotificationId);

                if (newCount > 0)
                    Services.TileService.UpdateBadge(newCount);
                else
                    Services.TileService.ClearBadge();
            }
            catch { }
        }

        private static async Task RunAutoSyncAsync(
            Services.SettingsService settings, Models.UserAccount account)
        {
            if (!settings.AutoSyncEnabled) return;
            var token = settings.AutoSyncFolderToken;
            if (string.IsNullOrEmpty(token)) return;

            try
            {
                var folder = await StorageApplicationPermissions.FutureAccessList
                    .GetFolderAsync(token);
                var sync = new Services.SyncService();
                await sync.UploadFolderAsync(folder, settings.AutoUploadRemotePath);
            }
            catch { }
        }

        private static void ShowToast(string title, string body)
        {
            var xml = $@"<toast>
  <visual>
    <binding template='ToastGeneric'>
      <text>{XmlEscape(title)}</text>
      <text>{XmlEscape(body)}</text>
    </binding>
  </visual>
</toast>";
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            ToastNotificationManager.CreateToastNotifier()
                                    .Show(new ToastNotification(doc));
        }

        private static string XmlEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
