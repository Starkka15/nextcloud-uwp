using System.Threading.Tasks;
using NextcloudUWP.Services;

namespace NextcloudUWP.ViewModels
{
    public class LoginViewModel
    {
        private readonly NextcloudClient _client;
        private readonly SettingsService _settings;

        public LoginViewModel()
        {
            _client = new NextcloudClient();
            _settings = new SettingsService();
        }

        public async Task<bool> LoginAsync(string serverUrl, string username, string password)
        {
            _client.Configure(serverUrl, username, password);
            try
            {
                var user = await _client.GetUserAsync();
                if (string.IsNullOrEmpty(user?.Id)) return false;

                _settings.AddAccount(
                    serverUrl, username, password,
                    displayName: user.DisplayName,
                    email: user.Email,
                    quotaUsed: user.QuotaUsed,
                    quotaTotal: user.QuotaTotal);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
