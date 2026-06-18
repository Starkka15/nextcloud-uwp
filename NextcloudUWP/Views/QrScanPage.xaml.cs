using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Graphics.Display;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Graphics.Imaging;
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
        // MFSampleExtension_Rotation — stamps the preview stream with a rotation so
        // CaptureElement renders upright. The phone camera sensor is mounted in a fixed
        // landscape orientation, so we must rotate to follow the display.
        private static readonly Guid RotationKey =
            new Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1");

        private MediaCapture _capture;
        private CancellationTokenSource _cts;
        private bool _decoded;

        private DisplayInformation _displayInfo;
        private int _sensorOrientation;   // camera mount angle, clockwise degrees
        private bool _mirroringPreview;    // true for front-facing cameras
        private bool _externalCamera;      // USB/webcam — no fixed orientation

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
            if (_displayInfo != null)
                _displayInfo.OrientationChanged -= OnOrientationChanged;
            _cts?.Cancel();
            StopCamera();
        }

        private async Task StartCameraAsync()
        {
            try
            {
                // Prefer the rear camera — that's what a QR scanner points outward.
                var cameras = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                if (cameras.Count == 0)
                {
                    StatusText.Text = "No camera found on this device.";
                    return;
                }
                var device = cameras.FirstOrDefault(c =>
                                 c.EnclosureLocation?.Panel == Windows.Devices.Enumeration.Panel.Back)
                             ?? cameras[0];

                _externalCamera   = device.EnclosureLocation == null;
                _mirroringPreview = !_externalCamera &&
                                    device.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front;
                _sensorOrientation = _externalCamera
                    ? 0
                    : (int)device.EnclosureLocation.RotationAngleInDegreesClockwise;

                _capture = new MediaCapture();
                await _capture.InitializeAsync(new MediaCaptureInitializationSettings
                {
                    VideoDeviceId = device.Id,
                    StreamingCaptureMode = StreamingCaptureMode.Video
                });

                CameraPreview.Source = _capture;
                await _capture.StartPreviewAsync();

                await EnableContinuousAutoFocusAsync();

                _displayInfo = DisplayInformation.GetForCurrentView();
                _displayInfo.OrientationChanged += OnOrientationChanged;
                await SetPreviewRotationAsync();

                _cts = new CancellationTokenSource();
                _ = ScanLoopAsync(_cts.Token).ContinueWith(t =>
                {
                    if (t.Exception != null) DebugLogger.LogException(nameof(QrScanPage), t.Exception);
                });
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Camera error: {ex.Message}";
            }
        }

        private async Task EnableContinuousAutoFocusAsync()
        {
            try
            {
                var focus = _capture.VideoDeviceController.FocusControl;
                if (!focus.Supported) return;

                await focus.UnlockAsync();
                var range = focus.SupportedFocusRanges.Contains(AutoFocusRange.FullRange)
                    ? AutoFocusRange.FullRange
                    : AutoFocusRange.Normal;
                var mode = focus.SupportedFocusModes.Contains(FocusMode.Continuous)
                    ? FocusMode.Continuous
                    : FocusMode.Auto;
                focus.Configure(new FocusSettings { Mode = mode, AutoFocusRange = range });
                await focus.FocusAsync();
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(QrScanPage), ex); }
        }

        private async void OnOrientationChanged(DisplayInformation sender, object args)
        {
            await SetPreviewRotationAsync();
        }

        private async Task SetPreviewRotationAsync()
        {
            if (_capture == null || _externalCamera) return;
            try
            {
                int displayDegrees = DisplayOrientationToDegrees(_displayInfo.CurrentOrientation);
                if (_mirroringPreview) displayDegrees = (360 - displayDegrees) % 360;
                int total = (_sensorOrientation + displayDegrees) % 360;

                var props = _capture.VideoDeviceController
                    .GetMediaStreamProperties(MediaStreamType.VideoPreview);
                props.Properties[RotationKey] = total;
                await _capture.SetEncodingPropertiesAsync(
                    MediaStreamType.VideoPreview, props, null);
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(QrScanPage), ex); }
        }

        private static int DisplayOrientationToDegrees(DisplayOrientations orientation)
        {
            switch (orientation)
            {
                case DisplayOrientations.Portrait:           return 90;
                case DisplayOrientations.LandscapeFlipped:   return 180;
                case DisplayOrientations.PortraitFlipped:    return 270;
                default:                                     return 0; // Landscape
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

            // Preview resolution drives the frame buffer we decode against.
            var previewProps = _capture.VideoDeviceController
                .GetMediaStreamProperties(MediaStreamType.VideoPreview) as VideoEncodingProperties;
            int width  = (int)(previewProps?.Width  ?? 640);
            int height = (int)(previewProps?.Height ?? 480);

            // Reused across iterations so the scan loop allocates nothing per frame.
            var pixelBuffer = new byte[width * height * 4];

            while (!token.IsCancellationRequested && !_decoded)
            {
                await Task.Delay(300, token).ContinueWith(_ => { });
                if (token.IsCancellationRequested) return;

                try
                {
                    // Sample the live preview buffer — NOT a still photo. CapturePhotoToStreamAsync
                    // drives the full shutter/refocus/full-res pipeline and on W10M repeated calls
                    // exhaust memory and crash the app. GetPreviewFrameAsync just copies the
                    // frame that is already on screen.
                    using (var frame = new VideoFrame(BitmapPixelFormat.Bgra8, width, height))
                    {
                        await _capture.GetPreviewFrameAsync(frame);
                        // SoftwareBitmap is owned by the VideoFrame; the outer using disposes it.
                        frame.SoftwareBitmap.CopyToBuffer(pixelBuffer.AsBuffer());
                    }

                    var result = reader.Decode(new RGBLuminanceSource(
                        pixelBuffer, width, height,
                        RGBLuminanceSource.BitmapFormat.BGRA32));

                    if (result != null)
                    {
                        _decoded = true;
                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                            () => HandleQrResult(result.Text));
                    }
                }
                catch (Exception ex) { DebugLogger.LogException(nameof(QrScanPage), ex); }
            }
        }

        private void HandleQrResult(string text)
        {
            StopCamera();

            // Nextcloud login deep link:
            //   nc://login/server:<url>&user:<user>&password:<appPassword>
            // Note: params are ':'-separated (not '='), '&'-delimited, live in the
            // PATH (no '?'), and the server value itself contains "https://" — so a
            // plain query-string parse fails. We split on '&', then on the FIRST ':'.
            if (string.IsNullOrEmpty(text) ||
                !text.StartsWith("nc://login", StringComparison.OrdinalIgnoreCase))
            {
                StatusText.Text = "Not a Nextcloud QR code. Try again.";
                _decoded = false;
                _ = StartCameraAsync().ContinueWith(t =>
                {
                    if (t.Exception != null) DebugLogger.LogException(nameof(QrScanPage), t.Exception);
                });
                return;
            }

            // Strip the "nc://login" prefix and any leading "/" or "?".
            var payload = text.Substring("nc://login".Length).TrimStart('/', '?');

            string server = null, user = null, password = null;
            foreach (var part in payload.Split('&'))
            {
                // Split on the FIRST ':' only — the server value contains "https://".
                int sep = part.IndexOf(':');
                if (sep < 0) continue;

                var key = part.Substring(0, sep).Trim();
                var val = Uri.UnescapeDataString(part.Substring(sep + 1));

                if (key.Equals("server", StringComparison.OrdinalIgnoreCase)) server = val;
                else if (key.Equals("user", StringComparison.OrdinalIgnoreCase)) user = val;
                else if (key.Equals("password", StringComparison.OrdinalIgnoreCase)) password = val;
            }

            if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
            {
                // Diagnostic: show which fields parsed + a snippet of the raw payload,
                // so a failing code can be inspected on-device instead of guessed at.
                var got = $"srv={(server != null)} usr={(user != null)} pwd={(password != null)}";
                var snippet = text.Length > 60 ? text.Substring(0, 60) + "…" : text;
                StatusText.Text = "Missing credentials [" + got + "]\n" + snippet;
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
            catch (Exception ex) { DebugLogger.LogException(nameof(QrScanPage), ex); }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
