using System;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using NextcloudUWP.Models;
using NextcloudUWP.ViewModels;

namespace NextcloudUWP.Views
{
    public sealed partial class CommentsPage : Page
    {
        private readonly MainViewModel _viewModel = new MainViewModel();
        private CloudFile _file;

        public CommentsPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _file = e.Parameter as CloudFile;
            if (_file == null) { Frame.GoBack(); return; }
            TitleText.Text = $"Comments: {_file.Name}";
            await LoadCommentsAsync();
        }

        private async System.Threading.Tasks.Task LoadCommentsAsync()
        {
            LoadingRing.IsActive     = true;
            CommentsList.Visibility  = Visibility.Collapsed;
            EmptyText.Visibility     = Visibility.Collapsed;

            try
            {
                var comments = await _viewModel.GetCommentsAsync(_file);
                if (comments.Count > 0)
                {
                    CommentsList.ItemsSource = new ObservableCollection<FileComment>(comments);
                    CommentsList.Visibility  = Visibility.Visible;
                }
                else
                {
                    EmptyText.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                EmptyText.Text       = "Could not load comments.";
                EmptyText.Visibility = Visibility.Visible;
            }
            finally
            {
                LoadingRing.IsActive = false;
            }
        }

        private async void PostComment_Click(object sender, RoutedEventArgs e)
        {
            var msg = CommentBox.Text?.Trim();
            if (string.IsNullOrEmpty(msg)) return;

            CommentBox.IsEnabled = false;
            try
            {
                var ok = await _viewModel.PostCommentAsync(_file, msg);
                if (ok)
                {
                    CommentBox.Text = "";
                    await LoadCommentsAsync();
                }
                else
                {
                    await new ContentDialog
                    {
                        Title             = "Error",
                        Content           = "Failed to post comment.",
                        PrimaryButtonText = "OK"
                    }.ShowAsync();
                }
            }
            finally { CommentBox.IsEnabled = true; }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
