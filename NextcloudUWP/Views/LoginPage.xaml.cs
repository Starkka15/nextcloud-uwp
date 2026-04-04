using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using NextcloudUWP.ViewModels;

namespace NextcloudUWP.Views
{
    public sealed partial class LoginPage : Page
    {
        private LoginViewModel _viewModel;
        private bool _isAddAccountMode;

        public LoginPage()
        {
            this.InitializeComponent();
            _viewModel = new LoginViewModel();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _isAddAccountMode = e.Parameter as string == "addAccount";
            if (_isAddAccountMode)
            {
                // Show a back button / "Add Account" heading
                TitleText.Text = "Add Account";
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            LoginProgress.IsActive = true;
            LoginButton.IsEnabled = false;

            var serverUrl = ServerUrlBox.Text?.Trim();
            var username  = UsernameBox.Text?.Trim();
            var password  = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(serverUrl) ||
                string.IsNullOrWhiteSpace(username)  ||
                string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please fill in all fields.");
                return;
            }

            if (!serverUrl.StartsWith("https://") && !serverUrl.StartsWith("http://"))
                serverUrl = "https://" + serverUrl;

            try
            {
                var success = await _viewModel.LoginAsync(serverUrl, username, password);
                if (success)
                {
                    if (_isAddAccountMode && Frame.CanGoBack)
                        Frame.GoBack();
                    else
                        Frame.Navigate(typeof(MainPage));
                }
                else
                {
                    ShowError("Login failed. Please check your credentials.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Connection error: {ex.Message}");
            }
            finally
            {
                LoginProgress.IsActive = false;
                LoginButton.IsEnabled = true;
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
            LoginProgress.IsActive = false;
            LoginButton.IsEnabled = true;
        }
    }
}
