using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using NextcloudUWP.ViewModels;

namespace NextcloudUWP.Views
{
    public sealed partial class NotificationsPage : Page
    {
        private readonly MainViewModel _viewModel = new MainViewModel();

        public NotificationsPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            LoadingRing.IsActive = true;
            EmptyText.Visibility = Visibility.Collapsed;

            var notifications = await _viewModel.GetNotificationsAsync();

            LoadingRing.IsActive = false;

            if (notifications == null || notifications.Count == 0)
            {
                EmptyText.Visibility = Visibility.Visible;
            }
            else
            {
                NotificationsList.ItemsSource = new ObservableCollection<Models.NextcloudNotification>(notifications);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
