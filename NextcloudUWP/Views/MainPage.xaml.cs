using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using NextcloudUWP.Models;
using NextcloudUWP.Services;
using NextcloudUWP.ViewModels;

namespace NextcloudUWP.Views
{
    public sealed partial class MainPage : Page
    {
        private MainViewModel _viewModel;
        private Stack<string> _navigationStack = new Stack<string>();
        private CloudFile _contextFile;

        private enum SortMode { NameAsc, NameDesc, DateNew, DateOld, SizeLarge, SizeSmall }
        private SortMode _sortMode = SortMode.NameAsc;

        public MainPage()
        {
            this.InitializeComponent();
            _viewModel = new MainViewModel();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _viewModel.Reconfigure();

            if (e.NavigationMode == NavigationMode.Back)
            {
                // Returning from a sub-page — reload root
                _navigationStack.Clear();
                LoadFiles("/").ConfigureAwait(false);
            }
            else if (e.Parameter is string path && !string.IsNullOrEmpty(path))
            {
                // Navigated here with a specific folder (e.g. from search)
                _navigationStack.Push(path);
                LoadFiles(path).ConfigureAwait(false);
            }
            else
            {
                // First launch
                LoadFiles("/").ConfigureAwait(false);
            }
        }

        private async System.Threading.Tasks.Task LoadFiles(string path)
        {
            LoadingRing.IsActive = true;
            FileListView.ItemsSource = null;
            EmptyText.Visibility = Visibility.Collapsed;

            try
            {
                var files = await _viewModel.GetFilesAsync(path);
                if (files != null && files.Count > 0)
                {
                    var sorted = ApplySort(files);
                    FileListView.ItemsSource = new ObservableCollection<CloudFile>(sorted);
                }
                else
                {
                    EmptyText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Failed to load files: {ex.Message}",
                    PrimaryButtonText = "OK"
                };
                await dialog.ShowAsync();
            }
            finally
            {
                LoadingRing.IsActive = false;
            }

            UpdateBackButton();
        }

        private List<CloudFile> ApplySort(List<CloudFile> files)
        {
            IEnumerable<CloudFile> ordered;
            switch (_sortMode)
            {
                case SortMode.NameDesc:
                    ordered = files.OrderByDescending(f => f.IsFolder).ThenByDescending(f => f.Name); break;
                case SortMode.DateNew:
                    ordered = files.OrderByDescending(f => f.IsFolder).ThenByDescending(f => f.ModifiedDate); break;
                case SortMode.DateOld:
                    ordered = files.OrderByDescending(f => f.IsFolder).ThenBy(f => f.ModifiedDate); break;
                case SortMode.SizeLarge:
                    ordered = files.OrderByDescending(f => f.IsFolder).ThenByDescending(f => f.Size); break;
                case SortMode.SizeSmall:
                    ordered = files.OrderByDescending(f => f.IsFolder).ThenBy(f => f.Size); break;
                default: // NameAsc
                    ordered = files.OrderByDescending(f => f.IsFolder).ThenBy(f => f.Name); break;
            }
            return ordered.ToList();
        }

        private void FileListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var file = e.ClickedItem as CloudFile;
            if (file == null) return;

            if (file.IsFolder)
            {
                _navigationStack.Push(file.Path);
                LoadFiles(file.Path).ConfigureAwait(false);
            }
            else
            {
                OpenFileSmart(file);
            }
        }

        private void OpenFileSmart(CloudFile file)
        {
            if (file.IsImage)
                Frame.Navigate(typeof(ImagePreviewPage), file);
            else if (file.IsVideo || file.IsAudio)
                Frame.Navigate(typeof(MediaPlayerPage), file);
            else if (file.IsText)
                Frame.Navigate(typeof(TextViewerPage), file);
            else
                OpenFile(file); // download to temp + launch external
        }

        // --- Context menu (right-tap / hold) ---

        private void File_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            ShowContextMenu(sender as FrameworkElement);
            e.Handled = true;
        }

        private void File_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                ShowContextMenu(sender as FrameworkElement);
                e.Handled = true;
            }
        }

        private void ShowContextMenu(FrameworkElement element)
        {
            if (element == null) return;
            _contextFile = element.DataContext as CloudFile;
            if (_contextFile == null) return;

            var menu = new MenuFlyout();

            if (!_contextFile.IsFolder)
            {
                var downloadItem = new MenuFlyoutItem { Text = "Download" };
                downloadItem.Click += ContextDownload_Click;
                menu.Items.Add(downloadItem);
            }

            var renameItem = new MenuFlyoutItem { Text = "Rename" };
            renameItem.Click += ContextRename_Click;
            menu.Items.Add(renameItem);

            var favText = _contextFile.IsFavorite ? "Remove from Favorites" : "Add to Favorites";
            var favItem = new MenuFlyoutItem { Text = favText };
            favItem.Click += ContextFavorite_Click;
            menu.Items.Add(favItem);

            if (!_contextFile.IsFolder)
            {
                var shareItem = new MenuFlyoutItem { Text = "Share" };
                shareItem.Click += ContextShare_Click;
                menu.Items.Add(shareItem);
            }

            var copyItem = new MenuFlyoutItem { Text = "Copy to..." };
            copyItem.Click += ContextCopy_Click;
            menu.Items.Add(copyItem);

            var moveItem = new MenuFlyoutItem { Text = "Move to..." };
            moveItem.Click += ContextMove_Click;
            menu.Items.Add(moveItem);

            var infoItem = new MenuFlyoutItem { Text = "Info" };
            infoItem.Click += ContextInfo_Click;
            menu.Items.Add(infoItem);

            menu.Items.Add(new MenuFlyoutSeparator());

            var deleteItem = new MenuFlyoutItem { Text = "Delete", Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Red) };
            deleteItem.Click += ContextDelete_Click;
            menu.Items.Add(deleteItem);

            menu.ShowAt(element);
        }

        private async void ContextDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_contextFile == null) return;
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            picker.SuggestedFileName = _contextFile.Name;
            var ext = System.IO.Path.GetExtension(_contextFile.Name);
            if (!string.IsNullOrEmpty(ext))
                picker.FileTypeChoices.Add("File", new List<string> { ext });
            picker.FileTypeChoices.Add("All Files", new List<string> { "." });

            var destFile = await picker.PickSaveFileAsync();
            if (destFile == null) return;

            LoadingRing.IsActive = true;
            try
            {
                await _viewModel.DownloadToDeviceAsync(_contextFile, destFile);
                var ok = new ContentDialog { Title = "Downloaded", Content = $"Saved to {destFile.Name}.", PrimaryButtonText = "OK" };
                await ok.ShowAsync();
            }
            catch (Exception ex) { await ShowErrorAsync(ex.Message); }
            finally { LoadingRing.IsActive = false; }
        }

        private async void ContextRename_Click(object sender, RoutedEventArgs e)
        {
            if (_contextFile == null) return;
            var tb = new TextBox { Text = _contextFile.Name };
            var dialog = new ContentDialog
            {
                Title = "Rename",
                Content = tb,
                PrimaryButtonText = "Rename",
                SecondaryButtonText = "Cancel"
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            var newName = tb.Text?.Trim();
            if (string.IsNullOrEmpty(newName) || newName == _contextFile.Name) return;

            LoadingRing.IsActive = true;
            try
            {
                await _viewModel.RenameAsync(_contextFile, newName);
                var currentPath = _navigationStack.Count > 0 ? _navigationStack.Peek() : "/";
                await LoadFiles(currentPath);
            }
            catch (Exception ex) { await ShowErrorAsync(ex.Message); }
            finally { LoadingRing.IsActive = false; }
        }

        private async void ContextFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (_contextFile == null) return;
            LoadingRing.IsActive = true;
            try
            {
                var newFav = !_contextFile.IsFavorite;
                await _viewModel.SetFavoriteAsync(_contextFile, newFav);
                _contextFile.IsFavorite = newFav;
                // Refresh to update star display
                var currentPath = _navigationStack.Count > 0 ? _navigationStack.Peek() : "/";
                await LoadFiles(currentPath);
            }
            catch (Exception ex) { await ShowErrorAsync(ex.Message); }
            finally { LoadingRing.IsActive = false; }
        }

        private async void ContextShare_Click(object sender, RoutedEventArgs e)
        {
            if (_contextFile == null) return;
            LoadingRing.IsActive = true;
            string url = null;
            try
            {
                url = await _viewModel.CreateShareLinkAsync(_contextFile);
            }
            catch { }
            finally { LoadingRing.IsActive = false; }

            if (string.IsNullOrEmpty(url))
            {
                await ShowErrorAsync("Failed to create share link.");
                return;
            }

            var tb = new TextBox
            {
                Text = url,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap
            };
            var shareDialog = new ContentDialog
            {
                Title = "Share link",
                Content = tb,
                PrimaryButtonText = "Copy",
                SecondaryButtonText = "Close"
            };
            var result = await shareDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dp.SetText(url);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            }
        }

        private async void ContextDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_contextFile == null) return;
            var dialog = new ContentDialog
            {
                Title = "Delete",
                Content = $"Delete \"{_contextFile.Name}\"?",
                PrimaryButtonText = "Delete",
                SecondaryButtonText = "Cancel"
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            LoadingRing.IsActive = true;
            try
            {
                if (await _viewModel.DeleteFileAsync(_contextFile))
                {
                    var currentPath = _navigationStack.Count > 0 ? _navigationStack.Peek() : "/";
                    await LoadFiles(currentPath);
                }
                else
                {
                    await ShowErrorAsync("Delete failed.");
                }
            }
            catch (Exception ex) { await ShowErrorAsync(ex.Message); }
            finally { LoadingRing.IsActive = false; }
        }

        // --- Navigation ---

        private async void OpenFile(CloudFile file)
        {
            try
            {
                await _viewModel.OpenFileAsync(file);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Could not open file: {ex.Message}");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_navigationStack.Count > 0)
            {
                _navigationStack.Pop();
                var path = _navigationStack.Count > 0 ? _navigationStack.Peek() : "/";
                LoadFiles(path).ConfigureAwait(false);
            }
        }

        private void UpdateBackButton()
        {
            BackButton.Visibility = _navigationStack.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            TitleText.Text = _navigationStack.Count > 0
                ? System.IO.Path.GetFileName(_navigationStack.Peek()?.TrimEnd('/') ?? "Nextcloud")
                : "Nextcloud";
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            var currentPath = _navigationStack.Count > 0 ? _navigationStack.Peek() : "/";
            LoadFiles(currentPath).ConfigureAwait(false);
        }

        private async void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add("*");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var currentPath = _navigationStack.Count > 0 ? _navigationStack.Peek() : "/";
                var dialog = new ContentDialog
                {
                    Title = "Upload",
                    Content = $"Upload {file.Name}?",
                    PrimaryButtonText = "Upload",
                    SecondaryButtonText = "Cancel"
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    LoadingRing.IsActive = true;
                    try
                    {
                        await _viewModel.UploadFileAsync(file, currentPath);
                        await LoadFiles(currentPath);
                    }
                    catch (Exception ex) { await ShowErrorAsync(ex.Message); }
                    finally { LoadingRing.IsActive = false; }
                }
            }
        }

        private async void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            var tb = new TextBox { PlaceholderText = "Folder name" };
            var dialog = new ContentDialog
            {
                Title = "New Folder",
                Content = tb,
                PrimaryButtonText = "Create",
                SecondaryButtonText = "Cancel"
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var name = tb.Text?.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    var currentPath = _navigationStack.Count > 0 ? _navigationStack.Peek() : "/";
                    try
                    {
                        await _viewModel.CreateFolderAsync(name, currentPath);
                        await LoadFiles(currentPath);
                    }
                    catch (Exception ex) { await ShowErrorAsync(ex.Message); }
                }
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }

        private void Accounts_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(AccountsPage));
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SearchPage));
        }

        private void Trashbin_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(TrashbinPage));
        }

        private void Notifications_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(NotificationsPage));
        }

        private void Activities_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ActivitiesPage));
        }

        private void Sort_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            void Add(string label, SortMode mode)
            {
                var item = new MenuFlyoutItem { Text = (_sortMode == mode ? "✓ " : "  ") + label };
                item.Click += (s, a) =>
                {
                    _sortMode = mode;
                    var currentPath = _navigationStack.Count > 0 ? _navigationStack.Peek() : "/";
                    LoadFiles(currentPath).ConfigureAwait(false);
                };
                flyout.Items.Add(item);
            }
            Add("Name (A–Z)",     SortMode.NameAsc);
            Add("Name (Z–A)",     SortMode.NameDesc);
            Add("Date (newest)",  SortMode.DateNew);
            Add("Date (oldest)",  SortMode.DateOld);
            Add("Size (largest)", SortMode.SizeLarge);
            Add("Size (smallest)",SortMode.SizeSmall);
            flyout.ShowAt(SortButton);
        }

        private async void ContextCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_contextFile == null) return;
            var tb = new TextBox
            {
                PlaceholderText = "Destination path (e.g. /Archive)",
                Text = System.IO.Path.GetDirectoryName(_contextFile.Path.TrimEnd('/'))?.Replace('\\', '/') ?? "/"
            };
            var dialog = new ContentDialog
            {
                Title = $"Copy \"{_contextFile.Name}\" to",
                Content = tb,
                PrimaryButtonText = "Copy",
                SecondaryButtonText = "Cancel"
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            var dest = $"{tb.Text.TrimEnd('/')}/{_contextFile.Name}";
            LoadingRing.IsActive = true;
            try
            {
                if (!await _viewModel.CopyAsync(_contextFile, dest))
                    await ShowErrorAsync("Copy failed.");
                else
                {
                    var currentPath = _navigationStack.Count > 0 ? _navigationStack.Peek() : "/";
                    await LoadFiles(currentPath);
                }
            }
            catch (Exception ex) { await ShowErrorAsync(ex.Message); }
            finally { LoadingRing.IsActive = false; }
        }

        private async void ContextMove_Click(object sender, RoutedEventArgs e)
        {
            if (_contextFile == null) return;
            var tb = new TextBox
            {
                PlaceholderText = "Destination folder (e.g. /Archive)",
                Text = System.IO.Path.GetDirectoryName(_contextFile.Path.TrimEnd('/'))?.Replace('\\', '/') ?? "/"
            };
            var dialog = new ContentDialog
            {
                Title = $"Move \"{_contextFile.Name}\" to folder",
                Content = tb,
                PrimaryButtonText = "Move",
                SecondaryButtonText = "Cancel"
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            LoadingRing.IsActive = true;
            try
            {
                if (!await _viewModel.MoveToFolderAsync(_contextFile, tb.Text))
                    await ShowErrorAsync("Move failed.");
                else
                {
                    var currentPath = _navigationStack.Count > 0 ? _navigationStack.Peek() : "/";
                    await LoadFiles(currentPath);
                }
            }
            catch (Exception ex) { await ShowErrorAsync(ex.Message); }
            finally { LoadingRing.IsActive = false; }
        }

        private async void ContextInfo_Click(object sender, RoutedEventArgs e)
        {
            if (_contextFile == null) return;
            var info =
                $"Name:     {_contextFile.Name}\n" +
                $"Path:     {_contextFile.Path}\n" +
                $"Type:     {(_contextFile.IsFolder ? "Folder" : _contextFile.MimeType ?? "Unknown")}\n" +
                $"Size:     {_contextFile.SizeText}\n" +
                $"Modified: {_contextFile.ModifiedDate:MMM dd, yyyy HH:mm}\n" +
                $"ETag:     {_contextFile.ETag ?? "—"}";
            await new ContentDialog
            {
                Title = _contextFile.Name,
                Content = new TextBlock { Text = info, FontFamily = new Windows.UI.Xaml.Media.FontFamily("Consolas"), FontSize = 12, TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = "OK"
            }.ShowAsync();
        }

        private async System.Threading.Tasks.Task ShowErrorAsync(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = message,
                PrimaryButtonText = "OK"
            };
            await dialog.ShowAsync();
        }
    }
}
