using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using NextcloudUWP.Models;
using NextcloudUWP.ViewModels;

namespace NextcloudUWP.Views
{
    public sealed partial class ShareFilePage : Page
    {
        private readonly MainViewModel _viewModel = new MainViewModel();
        private CloudFile _file;
        private ObservableCollection<ShareInfo> _shares = new ObservableCollection<ShareInfo>();

        public ShareFilePage()
        {
            this.InitializeComponent();
            SharesList.ItemsSource    = _shares;
            UserResultsList.ItemsSource  = new ObservableCollection<string>();
            GroupResultsList.ItemsSource = new ObservableCollection<string>();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _file = e.Parameter as CloudFile;
            if (_file == null) { Frame.GoBack(); return; }
            TitleText.Text = $"Share: {_file.Name}";
            await LoadSharesAsync();
        }

        private async Task LoadSharesAsync()
        {
            LoadingRing.IsActive   = true;
            SharesList.Visibility  = Visibility.Collapsed;
            NoSharesText.Visibility = Visibility.Collapsed;

            try
            {
                _shares.Clear();
                var shares = await _viewModel.GetSharesForFileAsync(_file);
                foreach (var s in shares) _shares.Add(s);
                SharesList.Visibility  = _shares.Count > 0 ? Visibility.Visible   : Visibility.Collapsed;
                NoSharesText.Visibility = _shares.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { ShowStatus("Failed to load shares."); }
            finally { LoadingRing.IsActive = false; }
        }

        private async void CreateLink_Click(object sender, RoutedEventArgs e)
        {
            CreateLinkButton.IsEnabled = false;
            try
            {
                var url = await _viewModel.CreateShareLinkAsync(_file);
                if (!string.IsNullOrEmpty(url))
                {
                    var dp = new DataPackage();
                    dp.SetText(url);
                    Clipboard.SetContent(dp);
                    ShowStatus($"Link copied: {url}", isError: false);
                    await LoadSharesAsync();
                }
                else
                {
                    ShowStatus("Failed to create link.");
                }
            }
            finally { CreateLinkButton.IsEnabled = true; }
        }

        private async void AddUserShare_Click(object sender, RoutedEventArgs e)
        {
            var userId = UserResultsList.SelectedItem as string
                      ?? UserSearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(userId)) return;

            try
            {
                var share = await _viewModel.CreateUserShareAsync(_file, userId);
                if (share != null) await LoadSharesAsync();
                else ShowStatus("Failed to share with user.");
            }
            catch (Exception ex) { ShowStatus(ex.Message); }
        }

        private async void AddGroupShare_Click(object sender, RoutedEventArgs e)
        {
            var groupId = GroupResultsList.SelectedItem as string
                       ?? GroupSearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(groupId)) return;

            try
            {
                var share = await _viewModel.CreateGroupShareAsync(_file, groupId);
                if (share != null) await LoadSharesAsync();
                else ShowStatus("Failed to share with group.");
            }
            catch (Exception ex) { ShowStatus(ex.Message); }
        }

        private async void DeleteShare_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse((sender as FrameworkElement)?.Tag?.ToString(), out int id)) return;
            var dialog = new ContentDialog
            {
                Title             = "Remove share",
                Content           = "Remove this share?",
                PrimaryButtonText = "Remove",
                SecondaryButtonText = "Cancel"
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            await _viewModel.DeleteShareAsync(id);
            await LoadSharesAsync();
        }

        private async void UserSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var q = UserSearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(q) || q.Length < 2) return;
            try
            {
                var results = await _viewModel.SearchUsersAsync(q);
                var list = UserResultsList.ItemsSource as ObservableCollection<string>;
                list.Clear();
                foreach (var (id, _) in results) list.Add(id);
            }
            catch { }
        }

        private async void GroupSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var q = GroupSearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(q) || q.Length < 2) return;
            try
            {
                var results = await _viewModel.SearchGroupsAsync(q);
                var list = GroupResultsList.ItemsSource as ObservableCollection<string>;
                list.Clear();
                foreach (var g in results) list.Add(g);
            }
            catch { }
        }

        private void ShowStatus(string msg, bool isError = true)
        {
            StatusText.Text       = msg;
            StatusText.Foreground = isError
                ? new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Red)
                : new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Green);
            StatusText.Visibility = Visibility.Visible;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
