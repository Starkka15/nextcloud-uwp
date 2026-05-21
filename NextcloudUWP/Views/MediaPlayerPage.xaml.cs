using System;
using Windows.Media;
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

            TitleText.Text       = file.Name;
            LoadingRing.IsActive = true;
            ErrorText.Visibility = Visibility.Collapsed;

            try
            {
                var tempFolder = ApplicationData.Current.TemporaryFolder;
                var tempFile   = await tempFolder.CreateFileAsync(
                    file.Name, CreationCollisionOption.ReplaceExisting);

                await _viewModel.DownloadToDeviceAsync(file, tempFile);

                // Use app-level singleton player so audio continues when page is left
                var player = App.AppMediaPlayer;
                player.AudioCategory = MediaPlayerAudioCategory.Media;

                var source = MediaSource.CreateFromStorageFile(tempFile);
                player.Source = source;
                Player.SetMediaPlayer(player);

                // Wire system transport controls (lock screen play/pause)
                var smtc = player.SystemMediaTransportControls;
                smtc.IsEnabled        = true;
                smtc.IsPlayEnabled    = true;
                smtc.IsPauseEnabled   = true;
                smtc.IsStopEnabled    = true;
                smtc.DisplayUpdater.Type  = MediaPlaybackType.Music;
                smtc.DisplayUpdater.MusicProperties.Title = file.Name;
                smtc.DisplayUpdater.Update();
                smtc.ButtonPressed += Smtc_ButtonPressed;
            }
            catch (Exception ex)
            {
                ErrorText.Text       = $"Could not load media: {ex.Message}";
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
            // Detach UI from player but keep audio running
            Player.SetMediaPlayer(null);
            var smtc = App.AppMediaPlayer.SystemMediaTransportControls;
            smtc.ButtonPressed -= Smtc_ButtonPressed;
        }

        private void Smtc_ButtonPressed(SystemMediaTransportControls sender,
            SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            var player = App.AppMediaPlayer;
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    _ = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher
                        .RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                        () => player.Play());
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    _ = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher
                        .RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                        () => player.Pause());
                    break;
                case SystemMediaTransportControlsButton.Stop:
                    _ = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher
                        .RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                        () => { player.Source = null; });
                    break;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
