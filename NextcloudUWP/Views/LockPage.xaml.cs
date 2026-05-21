using System;
using System.Threading.Tasks;
using Windows.Security.Credentials.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using NextcloudUWP;

namespace NextcloudUWP.Views
{
    public sealed partial class LockPage : Page
    {
        public LockPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await VerifyAsync();
        }

        private async Task VerifyAsync()
        {
            LockRing.IsActive        = true;
            RetryButton.Visibility   = Visibility.Collapsed;
            StatusText.Text          = "Verifying identity…";

            try
            {
                var avail = await UserConsentVerifier.CheckAvailabilityAsync();
                if (avail != UserConsentVerifierAvailability.Available)
                {
                    // Device has no biometric/PIN configured — bypass lock.
                    App.IsUnlocked = true;
                    Frame.Navigate(typeof(MainPage));
                    return;
                }

                var result = await UserConsentVerifier.RequestVerificationAsync("Unlock Nextcloud");
                if (result == UserConsentVerificationResult.Verified)
                {
                    App.IsUnlocked = true;
                    Frame.Navigate(typeof(MainPage));
                    Frame.BackStack.Clear();
                }
                else
                {
                    StatusText.Text        = result == UserConsentVerificationResult.Canceled
                        ? "Verification cancelled."
                        : "Verification failed.";
                    LockRing.IsActive      = false;
                    RetryButton.Visibility = Visibility.Visible;
                }
            }
            catch (Exception)
            {
                // UserConsentVerifier not supported — bypass.
                App.IsUnlocked = true;
                Frame.Navigate(typeof(MainPage));
            }
        }

        private async void RetryButton_Click(object sender, RoutedEventArgs e)
            => await VerifyAsync();
    }
}
