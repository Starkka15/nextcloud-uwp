using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using NextcloudUWP.Services;
using NextcloudUWP.ViewModels;

namespace NextcloudUWP.Views
{
    public sealed partial class LoginPage : Page
    {
        private LoginViewModel _viewModel;

        public LoginPage()
        {
            this.InitializeComponent();
            _viewModel = new LoginViewModel();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            LoginProgress.IsActive = true;
            LoginButton.IsEnabled = false;

            var serverUrl = ServerUrlBox.Text?.Trim();
            var username = UsernameBox.Text?.Trim();
            var password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(serverUrl) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please fill in all fields.");
                return;
            }

            if (!serverUrl.StartsWith("https://") && !serverUrl.StartsWith("http://"))
            {
                serverUrl = "https://" + serverUrl;
            }

            try
            {
                var success = await _viewModel.LoginAsync(serverUrl, username, password);
                if (success)
                {
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

        private void QrButton_Click(object sender, RoutedEventArgs e)
        {
        }
    }
}
