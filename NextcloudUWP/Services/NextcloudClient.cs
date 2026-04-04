using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NextcloudUWP.Models;

namespace NextcloudUWP.Services
{
    public class NextcloudClient
    {
        private readonly HttpClient _httpClient;
        private string _serverUrl;
        private string _username;
        private string _password;

        public NextcloudClient()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("OCS-APIREQUEST", "true");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public void Configure(string serverUrl, string username, string password)
        {
            _serverUrl = serverUrl.TrimEnd('/');
            _username = username;
            _password = password;

            var authBytes = Encoding.UTF8.GetBytes($"{username}:{password}");
            var authHeader = Convert.ToBase64String(authBytes);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_serverUrl}/status.php");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var status = JObject.Parse(content);
                    var installed = status["installed"];
                    return installed != null && installed.ToObject<bool>();
                }
            }
            catch { }
            return false;
        }

        public async Task<UserAccount> GetUserAsync()
        {
            var response = await _httpClient.GetAsync($"{_serverUrl}/ocs/v1.php/cloud/user?format=json");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            var data = json["ocs"]?["data"];

            var quota = data?["quota"];
            long quotaUsed  = quota?["used"]?.ToObject<long>()  ?? 0;
            long quotaTotal = quota?["total"]?.ToObject<long>() ?? 0;

            return new UserAccount
            {
                Id = data?["id"]?.ToString(),
                DisplayName = data?["display-name"]?.ToString(),
                Email = data?["email"]?.ToString(),
                ServerUrl = _serverUrl,
                Username = _username,
                QuotaUsed = quotaUsed,
                QuotaTotal = quotaTotal
            };
        }

        public async Task<bool> ValidateCredentialsAsync()
        {
            try
            {
                var user = await GetUserAsync();
                return !string.IsNullOrEmpty(user?.Id);
            }
            catch
            {
                return false;
            }
        }

        public async Task<JObject> GetCapabilitiesAsync()
        {
            var response = await _httpClient.GetAsync($"{_serverUrl}/ocs/v1.php/cloud/capabilities?format=json");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return JObject.Parse(content);
        }

        public async Task<List<CloudFile>> GetSharesAsync()
        {
            var response = await _httpClient.GetAsync($"{_serverUrl}/ocs/v1.php/apps/files_sharing/api/v1/shares?format=json");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            var shares = json["ocs"]?["data"] as JArray;
            var result = new List<CloudFile>();

            if (shares != null)
            {
                foreach (var share in shares)
                {
                    result.Add(new CloudFile
                    {
                        Name = share["file_target"]?.ToString()?.Trim('/'),
                        Path = share["path"]?.ToString(),
                        IsFolder = share["item_type"]?.ToString() == "folder",
                        MimeType = share["mimetype"]?.ToString()
                    });
                }
            }
            return result;
        }

        public async Task<string> CreateShareLinkAsync(string path)
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "path", path },
                { "shareType", "3" }
            });
            var response = await _httpClient.PostAsync(
                $"{_serverUrl}/ocs/v1.php/apps/files_sharing/api/v1/shares?format=json", content);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(body);
            return json["ocs"]?["data"]?["url"]?.ToString();
        }

        public async Task<bool> DeleteShareAsync(int shareId)
        {
            var response = await _httpClient.DeleteAsync($"{_serverUrl}/ocs/v1.php/apps/files_sharing/api/v1/shares/{shareId}");
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> CreateFolderAsync(string path)
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "path", path }
            });
            var response = await _httpClient.PostAsync($"{_serverUrl}/ocs/v2.php/apps/files/api/v1/directory?format=json", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<List<Models.NextcloudNotification>> GetNotificationsAsync()
        {
            var result = new List<Models.NextcloudNotification>();
            try
            {
                var response = await _httpClient.GetAsync(
                    $"{_serverUrl}/ocs/v2.php/apps/notifications/api/v2/notifications?format=json");
                if (!response.IsSuccessStatusCode) return result;
                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);
                var data = json["ocs"]?["data"] as JArray;
                if (data == null) return result;
                foreach (var n in data)
                {
                    var dt = DateTime.MinValue;
                    DateTime.TryParse(n["datetime"]?.ToString(), out dt);
                    result.Add(new Models.NextcloudNotification
                    {
                        NotificationId = n["notification_id"]?.ToObject<int>() ?? 0,
                        App      = n["app"]?.ToString(),
                        Subject  = n["subject"]?.ToString(),
                        Message  = n["message"]?.ToString(),
                        Link     = n["link"]?.ToString(),
                        Datetime = dt
                    });
                }
            }
            catch { }
            return result;
        }

        public async Task<List<Models.NextcloudActivity>> GetActivitiesAsync()
        {
            var result = new List<Models.NextcloudActivity>();
            try
            {
                var response = await _httpClient.GetAsync(
                    $"{_serverUrl}/ocs/v2.php/apps/activity/api/v2/activity?format=json&limit=50");
                if (!response.IsSuccessStatusCode) return result;
                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);
                var data = json["ocs"]?["data"] as JArray;
                if (data == null) return result;
                foreach (var a in data)
                {
                    var dt = DateTime.MinValue;
                    DateTime.TryParse(a["datetime"]?.ToString(), out dt);
                    result.Add(new Models.NextcloudActivity
                    {
                        ActivityId  = a["activity_id"]?.ToObject<int>() ?? 0,
                        App         = a["app"]?.ToString(),
                        Type        = a["type"]?.ToString(),
                        Subject     = a["subject"]?.ToString(),
                        Message     = a["message"]?.ToString(),
                        ObjectName  = a["object_name"]?.ToString(),
                        Link        = a["link"]?.ToString(),
                        Datetime    = dt
                    });
                }
            }
            catch { }
            return result;
        }

        public HttpClient GetRawHttpClient()
        {
            return _httpClient;
        }

        public string GetServerUrl()
        {
            return _serverUrl;
        }
    }
}
