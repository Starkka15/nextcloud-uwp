using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using NextcloudUWP.Services;

namespace NextcloudUWP.Views
{
    public sealed partial class AccountsPage : Page
    {
        private readonly SettingsService _settings = new SettingsService();

        public AccountsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Reload();
        }

        private void Reload()
        {
            var accounts = _settings.GetAccounts();
            var items = new List<AccountItem>();
            foreach (var a in accounts)
            {
                var label = string.IsNullOrEmpty(a.DisplayName) ? a.Username : a.DisplayName;
                items.Add(new AccountItem
                {
                    Label = label,
                    Initial = label.Length > 0 ? label.Substring(0, 1).ToUpper() : "?",
                    ServerUrl = a.ServerUrl,
                    AccountKey = $"{a.ServerUrl}|{a.Username}",
                    ActiveVisibility = a.IsActive ? Visibility.Visible : Visibility.Collapsed
                });
            }
            AccountsList.ItemsSource = items;
        }

        private void AccountsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as AccountItem;
            if (item == null) return;

            var parts = item.AccountKey.Split('|');
            if (parts.Length != 2) return;

            _settings.SetActiveAccount(parts[0], parts[1]);
            // Go back to MainPage — OnNavigatedTo there will reconfigure
            if (Frame.CanGoBack)
                Frame.GoBack();
        }

        private async void RemoveAccount_Click(object sender, RoutedEventArgs e)
        {
            var key = (sender as Button)?.Tag as string;
            if (key == null) return;

            var parts = key.Split('|');
            if (parts.Length != 2) return;

            var dialog = new ContentDialog
            {
                Title = "Remove account",
                Content = $"Remove {parts[1]} from {parts[0]}?",
                PrimaryButtonText = "Remove",
                SecondaryButtonText = "Cancel"
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            _settings.RemoveAccount(parts[0], parts[1]);

            // If no accounts left, go to login
            if (!_settings.HasCredentials)
            {
                Frame.Navigate(typeof(LoginPage));
                Frame.BackStack.Clear();
                return;
            }

            Reload();
        }

        private void AddAccount_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LoginPage), "addAccount");
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }

        private class AccountItem
        {
            public string Label { get; set; }
            public string Initial { get; set; }
            public string ServerUrl { get; set; }
            public string AccountKey { get; set; }
            public Visibility ActiveVisibility { get; set; }
        }
    }
}
