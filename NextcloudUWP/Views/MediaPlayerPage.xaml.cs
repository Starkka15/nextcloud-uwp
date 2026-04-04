using System;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using NextcloudUWP.Models;
using NextcloudUWP.ViewModels;

namespace NextcloudUWP.Views
{
    public sealed partial class MediaPlayerPage : Page
    {
        private readonly MainViewModel _viewModel = new MainViewModel();

        public MediaPlayerPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var file = e.Parameter as CloudFile;
            if (file == null) { Frame.GoBack(); return; }

            TitleText.Text = file.Name;
            LoadingRing.IsActive = true;
            ErrorText.Visibility = Visibility.Collapsed;

            try
            {
                // Download to temp file then play — MediaSource needs a seekable stream.
                var tempFolder = ApplicationData.Current.TemporaryFolder;
                var tempFile = await tempFolder.CreateFileAsync(
                    file.Name, CreationCollisionOption.ReplaceExisting);

                await _viewModel.DownloadToDeviceAsync(file, tempFile);

                var source = MediaSource.CreateFromStorageFile(tempFile);
                var mediaPlayer = new MediaPlayer();
                mediaPlayer.Source = source;
                Player.SetMediaPlayer(mediaPlayer);
            }
            catch (Exception ex)
            {
                ErrorText.Text = $"Could not load media: {ex.Message}";
                ErrorText.Visibility = Visibility.Visible;
            }
            finally
            {
                LoadingRing.IsActive = false;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            Player.MediaPlayer?.Pause();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
