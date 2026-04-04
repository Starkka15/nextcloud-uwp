using System;
using System.IO;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using NextcloudUWP.Models;
using NextcloudUWP.Services;
using NextcloudUWP.ViewModels;

namespace NextcloudUWP.Views
{
    public sealed partial class ImagePreviewPage : Page
    {
        private readonly MainViewModel _viewModel = new MainViewModel();

        public ImagePreviewPage()
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
                var stream = await _viewModel.GetDownloadStreamAsync(file);
                var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                ms.Position = 0;

                var bmp = new BitmapImage();
                await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                PreviewImage.Source = bmp;
            }
            catch (Exception ex)
            {
                ErrorText.Text = $"Could not load image: {ex.Message}";
                ErrorText.Visibility = Visibility.Visible;
            }
            finally
            {
                LoadingRing.IsActive = false;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
