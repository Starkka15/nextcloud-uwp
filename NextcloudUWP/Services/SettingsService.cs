using System;
using Windows.Storage;

namespace NextcloudUWP.Services
{
    public class SettingsService
    {
        private static readonly string KEY_SERVER_URL = "ServerUrl";
        private static readonly string KEY_USERNAME = "Username";
        private static readonly string KEY_PASSWORD = "Password";
        private static readonly string KEY_ACCESS_TOKEN = "AccessToken";

        private ApplicationDataContainer _localSettings;

        public SettingsService()
        {
            _localSettings = ApplicationData.Current.LocalSettings;
        }

        public bool HasCredentials
        {
            get
            {
                return !string.IsNullOrEmpty(ServerUrl)
                    && !string.IsNullOrEmpty(Username)
                    && !string.IsNullOrEmpty(Password);
            }
        }

        public string ServerUrl
        {
            get { return GetSetting<string>(KEY_SERVER_URL); }
            set { SetSetting(KEY_SERVER_URL, value); }
        }

        public string Username
        {
            get { return GetSetting<string>(KEY_USERNAME); }
            set { SetSetting(KEY_USERNAME, value); }
        }

        public string Password
        {
            get { return GetSetting<string>(KEY_PASSWORD); }
            set { SetSetting(KEY_PASSWORD, value); }
        }

        public string AccessToken
        {
            get { return GetSetting<string>(KEY_ACCESS_TOKEN); }
            set { SetSetting(KEY_ACCESS_TOKEN, value); }
        }

        public void SaveCredentials(string serverUrl, string username, string password)
        {
            ServerUrl = serverUrl;
            Username = username;
            Password = password;
        }

        public void ClearCredentials()
        {
            _localSettings.Values.Remove(KEY_SERVER_URL);
            _localSettings.Values.Remove(KEY_USERNAME);
            _localSettings.Values.Remove(KEY_PASSWORD);
            _localSettings.Values.Remove(KEY_ACCESS_TOKEN);
        }

        private T GetSetting<T>(string key)
        {
            if (_localSettings.Values.ContainsKey(key))
            {
                return (T)_localSettings.Values[key];
            }
            return default(T);
        }

        private void SetSetting(string key, object value)
        {
            if (value == null)
            {
                _localSettings.Values.Remove(key);
            }
            else
            {
                _localSettings.Values[key] = value;
            }
        }
    }
}
