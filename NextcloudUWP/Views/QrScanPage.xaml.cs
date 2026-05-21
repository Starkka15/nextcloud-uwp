using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using ZXing;
using ZXing.Common;
using NextcloudUWP;
using NextcloudUWP.Services;

namespace NextcloudUWP.Views
{
    public sealed partial class QrScanPage : Page
    {
        private MediaCapture _capture;
        private CancellationTokenSource _cts;
        private bool _decoded;

        public QrScanPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await StartCameraAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _cts?.Cancel();
            StopCamera();
        }

        private async Task StartCameraAsync()
        {
            try
            {
                _capture = new MediaCapture();
                await _capture.InitializeAsync(new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Video
                });

                CameraPreview.Source = _capture;
                await _capture.StartPreviewAsync();

                _cts = new CancellationTokenSource();
                _ = ScanLoopAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Camera error: {ex.Message}";
            }
        }

        private async Task ScanLoopAsync(CancellationToken token)
        {
            var reader = new BarcodeReader
            {
                Options = new DecodingOptions
                {
                    PossibleFormats = new[] { BarcodeFormat.QR_CODE },
                    TryHarder = true
                }
            };

            while (!token.IsCancellationRequested && !_decoded)
            {
                await Task.Delay(400, token).ContinueWith(_ => { });
                if (token.IsCancellationRequested) return;

                try
                {
                    // Capture a JPEG frame for decoding
                    var props   = ImageEncodingProperties.CreateJpeg();
                    props.Width = 640; props.Height = 480;
                    var stream  = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                    await _capture.CapturePhotoToStreamAsync(props, stream);
                    stream.Seek(0);

                    var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
                    var pixels  = await decoder.GetPixelDataAsync(
                        Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                        Windows.Graphics.Imaging.BitmapAlphaMode.Ignore,
                        new Windows.Graphics.Imaging.BitmapTransform(),
                        Windows.Graphics.Imaging.ExifOrientationMode.IgnoreExifOrientation,
                        Windows.Graphics.Imaging.ColorManagementMode.DoNotColorManage);

                    var bytes  = pixels.DetachPixelData();
                    var result = reader.Decode(new RGBLuminanceSource(bytes,
                        (int)decoder.PixelWidth, (int)decoder.PixelHeight,
                        RGBLuminanceSource.BitmapFormat.BGRA32));

                    if (result != null)
                    {
                        _decoded = true;
                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                            () => HandleQrResult(result.Text));
                    }
                }
                catch { }
            }
        }

        private void HandleQrResult(string text)
        {
            StopCamera();

            // nc://login?server={url}&user={user}&password={appPassword}
            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
                !uri.Scheme.Equals("nc", StringComparison.OrdinalIgnoreCase))
            {
                StatusText.Text = "Not a Nextcloud QR code. Try again.";
                _decoded = false;
                _ = StartCameraAsync();
                return;
            }

            string Get(string key)
            {
                try
                {
                    var q = new Windows.Foundation.WwwFormUrlDecoder(uri.Query.TrimStart('?'));
                    return q.GetFirstValueByName(key);
                }
                catch { return null; }
            }

            var server   = Get("server");
            var user     = Get("user");
            var password = Get("password");

            if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
            {
                StatusText.Text = "QR code missing credentials. Try browser login instead.";
                return;
            }

            var settings = new SettingsService();
            settings.AddAccount(server, user, password);
            App.IsUnlocked = true;
            Frame.Navigate(typeof(MainPage));
            Frame.BackStack.Clear();
        }

        private void StopCamera()
        {
            try
            {
                CameraPreview.Source = null;
                _capture?.Dispose();
                _capture = null;
            }
            catch { }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
