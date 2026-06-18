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
        private readonly MainViewModel _viewModel = MainViewModel.Instance;

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
                PreviewImage.Width  = bmp.PixelWidth;
                PreviewImage.Height = bmp.PixelHeight;
                PreviewImage.Source = bmp;

                // Fit image to the viewport on first load
                PreviewScrollViewer.UpdateLayout();
                if (bmp.PixelWidth > 0 && bmp.PixelHeight > 0)
                {
                    var vw   = PreviewScrollViewer.ViewportWidth;
                    var vh   = PreviewScrollViewer.ViewportHeight;
                    var fit  = (float)Math.Min(vw / bmp.PixelWidth, vh / bmp.PixelHeight);
                    fit = Math.Max(PreviewScrollViewer.MinZoomFactor,
                          Math.Min(fit, PreviewScrollViewer.MaxZoomFactor));
                    PreviewScrollViewer.ChangeView(null, null, fit, true);
                }
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
