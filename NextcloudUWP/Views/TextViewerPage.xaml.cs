using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using NextcloudUWP.Models;
using NextcloudUWP.Services;
using NextcloudUWP.ViewModels;

namespace NextcloudUWP.Views
{
    public sealed partial class TextViewerPage : Page
    {
        private readonly MainViewModel _viewModel = MainViewModel.Instance;
        private CloudFile _currentFile;
        private bool      _isEditMode;

        public TextViewerPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _currentFile = e.Parameter as CloudFile;
            if (_currentFile == null) { Frame.GoBack(); return; }

            TitleText.Text       = _currentFile.Name;
            LoadingRing.IsActive = true;
            ErrorText.Visibility = Visibility.Collapsed;

            try
            {
                var stream = await _viewModel.GetDownloadStreamAsync(_currentFile);
                using (var reader = new StreamReader(stream, Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true))
                {
                    var text = await reader.ReadToEndAsync();
                    if (text.Length > 512000)
                        text = text.Substring(0, 512000) + "\n\n[File truncated — edit mode unavailable for large files]";

                    ContentText.Text = text;
                    EditBox.Text     = text;
                }

                // Show edit button only for writable files (non-truncated and if permissions allow)
                EditButton.Visibility = _currentFile.Name.Length > 0
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ErrorText.Text       = $"Could not load file: {ex.Message}";
                ErrorText.Visibility = Visibility.Visible;
            }
            finally
            {
                LoadingRing.IsActive = false;
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            _isEditMode = true;
            EditBox.Text = ContentText.Text;
            ViewScroller.Visibility  = Visibility.Collapsed;
            EditBox.Visibility       = Visibility.Visible;
            EditToolbar.Visibility   = Visibility.Visible;
            EditButton.Visibility    = Visibility.Collapsed;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveButton.IsEnabled = false;
            SaveRing.IsActive    = true;

            try
            {
                var text    = EditBox.Text ?? "";
                var bytes   = Encoding.UTF8.GetBytes(text);
                var stream  = new MemoryStream(bytes);
                var settings = new SettingsService();

                var dav = new WebDavClient();
                if (settings.HasCredentials)
                    dav.Configure(settings.ServerUrl, settings.Username, settings.Password);

                var ok = await dav.UploadFileAsync(_currentFile.Path, stream, "text/plain");
                if (ok)
                {
                    ContentText.Text = text;
                    ExitEditMode();
                }
                else
                {
                    await new ContentDialog
                    {
                        Title             = "Error",
                        Content           = "Failed to save file.",
                        PrimaryButtonText = "OK"
                    }.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                await new ContentDialog
                {
                    Title             = "Error",
                    Content           = ex.Message,
                    PrimaryButtonText = "OK"
                }.ShowAsync();
            }
            finally
            {
                SaveButton.IsEnabled = true;
                SaveRing.IsActive    = false;
            }
        }

        private void CancelEditButton_Click(object sender, RoutedEventArgs e) => ExitEditMode();

        private void ExitEditMode()
        {
            _isEditMode = false;
            ViewScroller.Visibility = Visibility.Visible;
            EditBox.Visibility      = Visibility.Collapsed;
            EditToolbar.Visibility  = Visibility.Collapsed;
            EditButton.Visibility   = Visibility.Visible;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isEditMode) ExitEditMode();
            else if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
