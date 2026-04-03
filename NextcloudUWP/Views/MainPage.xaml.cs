using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using NextcloudUWP.Models;
using NextcloudUWP.Services;
using NextcloudUWP.ViewModels;

namespace NextcloudUWP.Views
{
    public sealed partial class MainPage : Page
    {
        private MainViewModel _viewModel;
        private Stack<string> _navigationStack = new Stack<string>();

        public MainPage()
        {
            this.InitializeComponent();
            _viewModel = new MainViewModel();
            Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadFiles("/");
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
                    var sorted = files.OrderByDescending(f => f.IsFolder).ThenBy(f => f.Name).ToList();
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
                OpenFile(file);
            }
        }

        private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var file = FileListView.SelectedItem as CloudFile;
            BottomBar.PrimaryCommands.Clear();
            BottomBar.PrimaryCommands.Add(new AppBarButton { Icon = new SymbolIcon(Symbol.Add), Label = "New Folder" });

            if (file != null)
            {
                if (file.IsFolder) return;
                BottomBar.PrimaryCommands.Add(new AppBarButton { Icon = new SymbolIcon(Symbol.Download), Label = "Download" });
                BottomBar.PrimaryCommands.Add(new AppBarButton { Icon = new SymbolIcon(Symbol.Share), Label = "Share" });
            }
        }

        private async void OpenFile(CloudFile file)
        {
            try
            {
                await _viewModel.OpenFileAsync(file);
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Could not open file: {ex.Message}",
                    PrimaryButtonText = "OK"
                };
                await dialog.ShowAsync();
            }
        }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Could not open file: {ex.Message}",
                    PrimaryButtonText = "OK"
                };
                await dialog.ShowAsync();
            }
            finally
            {
                LoadingRing.IsActive = false;
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
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    LoadingRing.IsActive = true;
                    try
                    {
                        await _viewModel.UploadFileAsync(file, currentPath);
                        await LoadFiles(currentPath);
                    }
                    catch (Exception ex)
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = "Upload Failed",
                            Content = ex.Message,
                            PrimaryButtonText = "OK"
                        };
                        await errorDialog.ShowAsync();
                    }
                    finally
                    {
                        LoadingRing.IsActive = false;
                    }
                }
            }
        }

        private async void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            var inputDialog = new ContentDialog
            {
                Title = "New Folder",
                Content = new TextBox { PlaceholderText = "Folder name" },
                PrimaryButtonText = "Create",
                SecondaryButtonText = "Cancel"
            };
            var result = await inputDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var name = (inputDialog.Content as TextBox)?.Text?.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    var currentPath = _navigationStack.Count > 0 ? _navigationStack.Peek() : "/";
                    try
                    {
                        await _viewModel.CreateFolderAsync(name, currentPath);
                        await LoadFiles(currentPath);
                    }
                    catch (Exception ex)
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = "Error",
                            Content = ex.Message,
                            PrimaryButtonText = "OK"
                        };
                        await errorDialog.ShowAsync();
                    }
                }
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LoginPage));
        }

        private void Accounts_Click(object sender, RoutedEventArgs e)
        {
        }

        private void OfflineFiles_Click(object sender, RoutedEventArgs e)
        {
        }
    }
}
