using System;
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

            var isValid = await _client.ValidateCredentialsAsync();
            if (isValid)
            {
                _settings.SaveCredentials(serverUrl, username, password);
                return true;
            }

            return false;
        }
    }
}
