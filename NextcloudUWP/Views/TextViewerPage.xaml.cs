using System;
using System.IO;
using System.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using NextcloudUWP.Models;
using NextcloudUWP.ViewModels;

namespace NextcloudUWP.Views
{
    public sealed partial class TextViewerPage : Page
    {
        private readonly MainViewModel _viewModel = new MainViewModel();

        public TextViewerPage()
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
                using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                {
                    var text = await reader.ReadToEndAsync();
                    // Cap at 500KB of display to avoid UI freeze
                    if (text.Length > 512000)
                        text = text.Substring(0, 512000) + "\n\n[File truncated for display]";
                    ContentText.Text = text;
                }
            }
            catch (Exception ex)
            {
                ErrorText.Text = $"Could not load file: {ex.Message}";
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
