using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using NextcloudUWP.Models;
using NextcloudUWP.Services;

namespace NextcloudUWP.Views
{
    public sealed partial class LoginFlowPage : Page
    {
        private string _pollEndpoint;
        private string _pollToken;
        private string _serverUrl;
        private CancellationTokenSource _cts;
        private bool _isAddAccount;

        public LoginFlowPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var param = e.Parameter as LoginFlowParams;
            if (param == null) { Frame.GoBack(); return; }

            _serverUrl    = param.ServerUrl;
            _isAddAccount = param.IsAddAccount;

            await StartLoginFlowAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _cts?.Cancel();
        }

        private async Task StartLoginFlowAsync()
        {
            ErrorOverlay.Visibility = Visibility.Collapsed;
            WaitOverlay.Visibility  = Visibility.Collapsed;
            HeaderRing.IsActive     = false;
            HeaderStatus.Text       = "";

            var client = new NextcloudClient();
            var init   = await client.InitLoginFlowAsync(_serverUrl);

            if (init == null)
            {
                ErrorText.Text          = "Server does not support Login Flow v2. Use username/password login.";
                ErrorOverlay.Visibility = Visibility.Visible;
                return;
            }

            _pollEndpoint = init.PollEndpoint;
            _pollToken    = init.PollToken;

            LoginWebView.Navigate(new Uri(init.LoginUrl));
        }

        private void LoginWebView_NavigationCompleted(WebView sender,
            WebViewNavigationCompletedEventArgs args)
        {
            // Login page loaded — start polling silently in the header, don't cover the form
            HeaderRing.IsActive = true;
            HeaderStatus.Text   = "Waiting for authorization…";
            _cts = new CancellationTokenSource();
            _ = PollForCredentialsAsync(_cts.Token).ContinueWith(t =>
            {
                if (t.Exception != null) DebugLogger.LogException(nameof(LoginFlowPage), t.Exception);
            });
        }

        private async Task PollForCredentialsAsync(CancellationToken token)
        {
            var client = new NextcloudClient();

            for (int i = 0; i < 120 && !token.IsCancellationRequested; i++)
            {
                await Task.Delay(2000, token).ContinueWith(_ => { });
                if (token.IsCancellationRequested) return;

                try
                {
                    var creds = await client.PollLoginFlowAsync(_pollEndpoint, _pollToken);
                    if (creds != null)
                    {
                        await Windows.ApplicationModel.Core.CoreApplication.MainView
                            .CoreWindow.Dispatcher.RunAsync(
                                Windows.UI.Core.CoreDispatcherPriority.Normal,
                                async () => await OnCredentialsReceivedAsync(creds));
                        return;
                    }
                }
                catch (Exception ex) { DebugLogger.LogException(nameof(LoginFlowPage), ex); }
            }

            // Timed out
            await Windows.ApplicationModel.Core.CoreApplication.MainView
                .CoreWindow.Dispatcher.RunAsync(
                    Windows.UI.Core.CoreDispatcherPriority.Normal,
                    () =>
                    {
                        HeaderRing.IsActive     = false;
                        HeaderStatus.Text       = "";
                        ErrorText.Text          = "Login timed out. Please try again.";
                        ErrorOverlay.Visibility = Visibility.Visible;
                    });
        }

        private async Task OnCredentialsReceivedAsync(LoginFlowCredentials creds)
        {
            _cts?.Cancel();
            HeaderRing.IsActive = false;
            HeaderStatus.Text   = "Signed in!";

            // Fetch user info then save account
            var ncClient = new NextcloudClient();
            ncClient.Configure(creds.Server, creds.LoginName, creds.AppPassword);

            UserAccount account = null;
            try { account = await ncClient.GetUserAsync(); } catch (Exception ex) { DebugLogger.LogException(nameof(LoginFlowPage), ex); }

            var settings = new SettingsService();
            settings.AddAccount(
                creds.Server,
                creds.LoginName,
                creds.AppPassword,
                displayName:  account?.DisplayName,
                email:        account?.Email,
                quotaUsed:    account?.QuotaUsed  ?? 0,
                quotaTotal:   account?.QuotaTotal ?? 0);

            if (_isAddAccount && Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(MainPage));
        }

        private async void RetryButton_Click(object sender, RoutedEventArgs e)
            => await StartLoginFlowAsync();

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }

    public class LoginFlowParams
    {
        public string ServerUrl    { get; set; }
        public bool   IsAddAccount { get; set; }
    }
}
