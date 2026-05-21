using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using NextcloudUWP.Models;
using NextcloudUWP.ViewModels;

namespace NextcloudUWP.Views
{
    public sealed partial class WebViewPage : Page
    {
        private readonly MainViewModel _viewModel = new MainViewModel();

        public WebViewPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            LoadingRing.IsActive = true;
            ErrorText.Visibility = Visibility.Collapsed;

            // URL-only mode (Collabora/Office, external web pages)
            if (e.Parameter is WebViewParams wp)
            {
                TitleText.Text = wp.Title ?? wp.Url;
                ContentWebView.Navigate(new Uri(wp.Url));
                LoadingRing.IsActive = false;
                return;
            }

            var file = e.Parameter as CloudFile;
            if (file == null) { Frame.GoBack(); return; }

            TitleText.Text = file.Name;

            try
            {
                var ext  = Path.GetExtension(file.Name).ToLowerInvariant();
                var mime = file.MimeType ?? "";

                if (mime == "text/markdown" || ext == ".md" || ext == ".markdown")
                    await RenderMarkdownAsync(file);
                else
                    await RenderFileAsync(file);
            }
            catch (Exception ex)
            {
                ErrorText.Text       = $"Could not open file: {ex.Message}";
                ErrorText.Visibility = Visibility.Visible;
            }
            finally
            {
                LoadingRing.IsActive = false;
            }
        }

        private async Task RenderFileAsync(CloudFile file)
        {
            // Use a fixed safe name — avoids URI encoding issues with spaces/special chars
            var ext      = Path.GetExtension(file.Name).ToLowerInvariant();
            var safeName = "preview" + ext;

            var tempFolder = ApplicationData.Current.TemporaryFolder;
            var tempFile   = await tempFolder.CreateFileAsync(
                safeName, CreationCollisionOption.ReplaceExisting);

            using (var dl  = await _viewModel.GetDownloadStreamAsync(file))
            using (var ras = await tempFile.OpenAsync(FileAccessMode.ReadWrite))
            using (var fs  = ras.AsStreamForWrite())
            {
                await dl.CopyToAsync(fs);
                await fs.FlushAsync();
            }

            // WebView can't render PDFs — hand off to system viewer
            if (ext == ".pdf")
            {
                await Windows.System.Launcher.LaunchFileAsync(tempFile);
                if (Frame.CanGoBack) Frame.GoBack();
                return;
            }

            // ms-appdata:///temp/ maps to the app's TemporaryFolder
            ContentWebView.Navigate(new Uri($"ms-appdata:///temp/{safeName}"));
        }

        private async Task RenderMarkdownAsync(CloudFile file)
        {
            using (var dl  = await _viewModel.GetDownloadStreamAsync(file))
            using (var reader = new StreamReader(dl, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                var markdown = await reader.ReadToEndAsync();
                if (markdown.Length > 256000)
                    markdown = markdown.Substring(0, 256000) + "\n\n*[truncated]*";

                var html = BuildMarkdownHtml(markdown);
                ContentWebView.NavigateToString(html);
            }
        }

        private static string BuildMarkdownHtml(string markdown)
        {
            // Inline minimal Markdown→HTML converter (headers, bold, italic, code, lists, links, hr)
            var body = System.Text.RegularExpressions.Regex.Replace(
                System.Net.WebUtility.HtmlEncode(markdown), @"&amp;", "&");

            // Code blocks (``` ... ```)
            body = System.Text.RegularExpressions.Regex.Replace(
                body, @"```([^`]*?)```",
                m => $"<pre><code>{m.Groups[1].Value}</code></pre>",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            // Headers
            body = System.Text.RegularExpressions.Regex.Replace(body, @"^######\s+(.+)$", "<h6>$1</h6>", System.Text.RegularExpressions.RegexOptions.Multiline);
            body = System.Text.RegularExpressions.Regex.Replace(body, @"^#####\s+(.+)$",  "<h5>$1</h5>", System.Text.RegularExpressions.RegexOptions.Multiline);
            body = System.Text.RegularExpressions.Regex.Replace(body, @"^####\s+(.+)$",   "<h4>$1</h4>", System.Text.RegularExpressions.RegexOptions.Multiline);
            body = System.Text.RegularExpressions.Regex.Replace(body, @"^###\s+(.+)$",    "<h3>$1</h3>", System.Text.RegularExpressions.RegexOptions.Multiline);
            body = System.Text.RegularExpressions.Regex.Replace(body, @"^##\s+(.+)$",     "<h2>$1</h2>", System.Text.RegularExpressions.RegexOptions.Multiline);
            body = System.Text.RegularExpressions.Regex.Replace(body, @"^#\s+(.+)$",      "<h1>$1</h1>", System.Text.RegularExpressions.RegexOptions.Multiline);

            // HR
            body = System.Text.RegularExpressions.Regex.Replace(body, @"^---+$", "<hr/>", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Bold / italic / inline code
            body = System.Text.RegularExpressions.Regex.Replace(body, @"\*\*\*(.+?)\*\*\*", "<strong><em>$1</em></strong>");
            body = System.Text.RegularExpressions.Regex.Replace(body, @"\*\*(.+?)\*\*",     "<strong>$1</strong>");
            body = System.Text.RegularExpressions.Regex.Replace(body, @"\*(.+?)\*",          "<em>$1</em>");
            body = System.Text.RegularExpressions.Regex.Replace(body, @"`(.+?)`",            "<code>$1</code>");

            // Links  [text](url)
            body = System.Text.RegularExpressions.Regex.Replace(body, @"\[([^\]]+)\]\(([^)]+)\)",
                "<a href=\"$2\">$1</a>");

            // Unordered lists
            body = System.Text.RegularExpressions.Regex.Replace(body, @"^[\*\-]\s+(.+)$",
                "<li>$1</li>", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Newlines → <br> (for non-block lines)
            body = System.Text.RegularExpressions.Regex.Replace(body, @"\n(?!<(h\d|ul|ol|li|pre|hr))", "<br/>\n");

            return $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<meta name='viewport' content='width=device-width,initial-scale=1'/>
<style>
  body{{font-family:Segoe UI,sans-serif;font-size:14px;padding:16px;
        color:#1a1a1a;background:#fff;line-height:1.6;max-width:700px;margin:0 auto}}
  h1,h2,h3,h4,h5,h6{{color:#0082c9;margin:1em 0 .4em}}
  code,pre{{background:#f5f5f5;border-radius:3px;padding:2px 4px;font-family:Consolas,monospace;font-size:13px}}
  pre{{padding:12px;overflow-x:auto}}
  a{{color:#0082c9}} hr{{border:none;border-top:1px solid #ddd;margin:1em 0}}
  li{{margin:.2em 0}}
</style></head><body>{body}</body></html>";
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }

    public class WebViewParams
    {
        public string Url   { get; set; }
        public string Title { get; set; }
    }
}
