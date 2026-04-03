using System;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using NextcloudUWP.Models;
using NextcloudUWP.ViewModels;

namespace NextcloudUWP.Views
{
    public sealed partial class TrashbinPage : Page
    {
        private readonly MainViewModel _viewModel = new MainViewModel();

        public TrashbinPage()
        {
            this.InitializeComponent();
            Loaded += async (s, e) => await LoadTrashbin();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }

        private async System.Threading.Tasks.Task LoadTrashbin()
        {
            LoadingRing.IsActive = true;
            TrashListView.ItemsSource = null;
            EmptyText.Visibility = Visibility.Collapsed;

            try
            {
                var items = await _viewModel.ListTrashbinAsync();
                if (items != null && items.Count > 0)
                    TrashListView.ItemsSource = new ObservableCollection<TrashbinFile>(items);
                else
                    EmptyText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                EmptyText.Text = $"Failed to load trash: {ex.Message}";
                EmptyText.Visibility = Visibility.Visible;
            }
            finally
            {
                LoadingRing.IsActive = false;
            }
        }

        private async void Restore_Click(object sender, RoutedEventArgs e)
        {
            var file = TrashListView.SelectedItem as TrashbinFile;
            if (file == null)
            {
                var hint = new ContentDialog
                {
                    Title = "Restore",
                    Content = "Select a file to restore.",
                    PrimaryButtonText = "OK"
                };
                await hint.ShowAsync();
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "Restore",
                Content = $"Restore \"{file.Name}\"?",
                PrimaryButtonText = "Restore",
                SecondaryButtonText = "Cancel"
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            LoadingRing.IsActive = true;
            try
            {
                if (await _viewModel.RestoreTrashbinFileAsync(file))
                    await LoadTrashbin();
                else
                    ShowError("Restore failed.");
            }
            catch (Exception ex) { ShowError(ex.Message); }
            finally { LoadingRing.IsActive = false; }
        }

        private async void DeletePermanently_Click(object sender, RoutedEventArgs e)
        {
            var file = TrashListView.SelectedItem as TrashbinFile;
            if (file == null)
            {
                var hint = new ContentDialog
                {
                    Title = "Delete",
                    Content = "Select a file to delete permanently.",
                    PrimaryButtonText = "OK"
                };
                await hint.ShowAsync();
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "Delete permanently",
                Content = $"Permanently delete \"{file.Name}\"? This cannot be undone.",
                PrimaryButtonText = "Delete",
                SecondaryButtonText = "Cancel"
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            LoadingRing.IsActive = true;
            try
            {
                if (await _viewModel.DeleteTrashbinPermanentlyAsync(file))
                    await LoadTrashbin();
                else
                    ShowError("Delete failed.");
            }
            catch (Exception ex) { ShowError(ex.Message); }
            finally { LoadingRing.IsActive = false; }
        }

        private async void EmptyTrash_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Empty Trash",
                Content = "Permanently delete all items in trash? This cannot be undone.",
                PrimaryButtonText = "Empty",
                SecondaryButtonText = "Cancel"
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            LoadingRing.IsActive = true;
            try
            {
                if (await _viewModel.EmptyTrashbinAsync())
                    await LoadTrashbin();
                else
                    ShowError("Failed to empty trash.");
            }
            catch (Exception ex) { ShowError(ex.Message); }
            finally { LoadingRing.IsActive = false; }
        }

        private async void ShowError(string message)
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
