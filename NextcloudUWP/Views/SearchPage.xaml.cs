using System;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using NextcloudUWP.Models;
using NextcloudUWP.ViewModels;

namespace NextcloudUWP.Views
{
    public sealed partial class SearchPage : Page
    {
        private readonly MainViewModel _viewModel = new MainViewModel();

        public SearchPage()
        {
            this.InitializeComponent();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }

        private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
                DoSearch();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            DoSearch();
        }

        private async void DoSearch()
        {
            var query = SearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(query)) return;

            LoadingRing.IsActive = true;
            ResultsListView.ItemsSource = null;
            EmptyText.Visibility = Visibility.Collapsed;

            try
            {
                var results = await _viewModel.SearchAsync(query);
                if (results != null && results.Count > 0)
                {
                    ResultsListView.ItemsSource = new ObservableCollection<CloudFile>(results);
                }
                else
                {
                    EmptyText.Text = "No results found";
                    EmptyText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                EmptyText.Text = $"Search failed: {ex.Message}";
                EmptyText.Visibility = Visibility.Visible;
            }
            finally
            {
                LoadingRing.IsActive = false;
            }
        }

        private async void ResultsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var file = e.ClickedItem as CloudFile;
            if (file == null) return;

            if (file.IsFolder)
            {
                Frame.Navigate(typeof(MainPage), file.Path);
                return;
            }

            try
            {
                await _viewModel.OpenFileAsync(file);
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = ex.Message,
                    PrimaryButtonText = "OK"
                };
                await dialog.ShowAsync();
            }
        }
    }
}
