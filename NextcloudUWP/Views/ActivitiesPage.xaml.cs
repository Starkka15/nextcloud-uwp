using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using NextcloudUWP.ViewModels;

namespace NextcloudUWP.Views
{
    public sealed partial class ActivitiesPage : Page
    {
        private readonly MainViewModel _viewModel = new MainViewModel();

        public ActivitiesPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            LoadingRing.IsActive = true;
            EmptyText.Visibility = Visibility.Collapsed;

            var activities = await _viewModel.GetActivitiesAsync();

            LoadingRing.IsActive = false;

            if (activities == null || activities.Count == 0)
            {
                EmptyText.Visibility = Visibility.Visible;
            }
            else
            {
                ActivitiesList.ItemsSource = new ObservableCollection<Models.NextcloudActivity>(activities);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
