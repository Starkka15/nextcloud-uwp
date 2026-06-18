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
        private HttpClient _httpClient;
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

            _httpClient = new HttpClient(new CertPinningHandler(_serverUrl));
            _httpClient.DefaultRequestHeaders.Add("OCS-APIREQUEST", "true");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
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
            catch (Exception ex) { DebugLogger.LogException(nameof(NextcloudClient), ex); }
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
            catch (Exception ex)
            {
                DebugLogger.LogException(nameof(NextcloudClient), ex);
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
            catch (Exception ex) { DebugLogger.LogException(nameof(NextcloudClient), ex); }
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
            catch (Exception ex) { DebugLogger.LogException(nameof(NextcloudClient), ex); }
            return result;
        }

        // ── Shares ────────────────────────────────────────────────────────────

        public async Task<List<Models.ShareInfo>> GetSharesForFileAsync(string path)
        {
            var result = new List<Models.ShareInfo>();
            try
            {
                var url = $"{_serverUrl}/ocs/v1.php/apps/files_sharing/api/v1/shares?format=json&path={Uri.EscapeDataString(path)}";
                var resp = await _httpClient.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return result;
                var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
                var data = json["ocs"]?["data"] as JArray;
                if (data == null) return result;
                foreach (var s in data)
                    result.Add(ParseShare(s));
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(NextcloudClient), ex); }
            return result;
        }

        public async Task<Models.ShareInfo> CreateUserShareAsync(string path, string userId, int permissions = 17)
        {
            return await CreateShareAsync(path, shareType: 0, shareWith: userId, permissions: permissions);
        }

        public async Task<Models.ShareInfo> CreateGroupShareAsync(string path, string groupId, int permissions = 17)
        {
            return await CreateShareAsync(path, shareType: 1, shareWith: groupId, permissions: permissions);
        }

        private async Task<Models.ShareInfo> CreateShareAsync(string path, int shareType,
            string shareWith = null, int permissions = 17)
        {
            try
            {
                var fields = new Dictionary<string, string>
                {
                    { "path",        path },
                    { "shareType",   shareType.ToString() },
                    { "permissions", permissions.ToString() }
                };
                if (!string.IsNullOrEmpty(shareWith))
                    fields["shareWith"] = shareWith;

                var resp = await _httpClient.PostAsync(
                    $"{_serverUrl}/ocs/v1.php/apps/files_sharing/api/v1/shares?format=json",
                    new FormUrlEncodedContent(fields));
                if (!resp.IsSuccessStatusCode) return null;
                var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
                var data = json["ocs"]?["data"];
                return data != null ? ParseShare(data) : null;
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(NextcloudClient), ex); return null; }
        }

        public async Task<bool> UpdateSharePermissionsAsync(int shareId, int permissions)
        {
            try
            {
                var resp = await _httpClient.SendAsync(new HttpRequestMessage(
                    new HttpMethod("PUT"),
                    $"{_serverUrl}/ocs/v1.php/apps/files_sharing/api/v1/shares/{shareId}?format=json")
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                        { { "permissions", permissions.ToString() } })
                });
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(NextcloudClient), ex); return false; }
        }

        private static Models.ShareInfo ParseShare(JToken s) => new Models.ShareInfo
        {
            Id                   = s.Value<int>("id"),
            Path                 = s.Value<string>("path"),
            ShareType            = s.Value<int>("share_type"),
            ShareWith            = s.Value<string>("share_with"),
            ShareWithDisplayName = s.Value<string>("share_with_displayname"),
            Token                = s.Value<string>("token"),
            Url                  = s.Value<string>("url"),
            Permissions          = s.Value<int>("permissions"),
            Expiration           = s.Value<string>("expiration")
        };

        // ── File comments ─────────────────────────────────────────────────────

        public async Task<List<Models.FileComment>> GetCommentsAsync(string fileId)
        {
            var result = new List<Models.FileComment>();
            try
            {
                var resp = await _httpClient.GetAsync(
                    $"{_serverUrl}/remote.php/dav/comments/files/{fileId}/?format=json");
                if (!resp.IsSuccessStatusCode) return result;
                // Comments API returns WebDAV XML, parse it
                var content = await resp.Content.ReadAsStringAsync();
                var doc = System.Xml.Linq.XDocument.Parse(content);
                var ncNs = System.Xml.Linq.XNamespace.Get("http://owncloud.org/ns");
                var davNs = System.Xml.Linq.XNamespace.Get("DAV:");
                foreach (var elem in doc.Root.Elements(davNs + "response"))
                {
                    var prop = elem.Element(davNs + "propstat")?.Element(davNs + "prop");
                    if (prop == null) continue;
                    DateTime dt = DateTime.MinValue;
                    DateTime.TryParse(prop.Element(ncNs + "creationDateTime")?.Value, out dt);
                    result.Add(new Models.FileComment
                    {
                        Id               = prop.Element(ncNs + "id")?.Value != null
                                            ? int.Parse(prop.Element(ncNs + "id").Value) : 0,
                        Message          = prop.Element(ncNs + "message")?.Value ?? "",
                        ActorDisplayName = prop.Element(ncNs + "actorDisplayName")?.Value ?? "",
                        ActorId          = prop.Element(ncNs + "actorId")?.Value ?? "",
                        CreationDateTime = dt
                    });
                }
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(NextcloudClient), ex); }
            return result;
        }

        public async Task<bool> PostCommentAsync(string fileId, string message)
        {
            try
            {
                var body = $"{{\"actorType\":\"users\",\"verb\":\"comment\",\"message\":{JsonConvert.SerializeObject(message)}}}";
                var resp = await _httpClient.PostAsync(
                    $"{_serverUrl}/remote.php/dav/comments/files/{fileId}/",
                    new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(NextcloudClient), ex); return false; }
        }

        // ── User / group search ───────────────────────────────────────────────

        public async Task<List<(string id, string displayName)>> SearchUsersAsync(string query)
        {
            var result = new List<(string, string)>();
            try
            {
                var resp = await _httpClient.GetAsync(
                    $"{_serverUrl}/ocs/v1.php/cloud/users?search={Uri.EscapeDataString(query)}&format=json");
                if (!resp.IsSuccessStatusCode) return result;
                var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
                var users = json["ocs"]?["data"]?["users"] as JArray;
                if (users != null)
                    foreach (var u in users)
                        result.Add((u.ToString(), u.ToString()));
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(NextcloudClient), ex); }
            return result;
        }

        public async Task<List<string>> SearchGroupsAsync(string query)
        {
            var result = new List<string>();
            try
            {
                var resp = await _httpClient.GetAsync(
                    $"{_serverUrl}/ocs/v1.php/cloud/groups?search={Uri.EscapeDataString(query)}&format=json");
                if (!resp.IsSuccessStatusCode) return result;
                var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
                var groups = json["ocs"]?["data"]?["groups"] as JArray;
                if (groups != null)
                    foreach (var g in groups)
                        result.Add(g.ToString());
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(NextcloudClient), ex); }
            return result;
        }

        // ── Login Flow v2 ─────────────────────────────────────────────────────

        public async Task<LoginFlowInitResult> InitLoginFlowAsync(string serverUrl)
        {
            try
            {
                var url  = serverUrl.TrimEnd('/') + "/index.php/login/v2";
                var resp = await new HttpClient().PostAsync(url, new StringContent(""));
                if (!resp.IsSuccessStatusCode) return null;
                var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
                return new LoginFlowInitResult
                {
                    LoginUrl     = json["login"]?.ToString(),
                    PollEndpoint = json["poll"]?["endpoint"]?.ToString(),
                    PollToken    = json["poll"]?["token"]?.ToString()
                };
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(NextcloudClient), ex); return null; }
        }

        public async Task<LoginFlowCredentials> PollLoginFlowAsync(string endpoint, string token)
        {
            try
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                    { { "token", token } });
                var resp = await new HttpClient().PostAsync(endpoint, content);
                if (!resp.IsSuccessStatusCode) return null;
                var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
                var server   = json["server"]?.ToString();
                var login    = json["loginName"]?.ToString();
                var password = json["appPassword"]?.ToString();
                if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(login)) return null;
                return new LoginFlowCredentials { Server = server, LoginName = login, AppPassword = password };
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(NextcloudClient), ex); return null; }
        }

        public HttpClient GetRawHttpClient() => _httpClient;
        public string GetServerUrl()         => _serverUrl;
    }

    public class LoginFlowInitResult
    {
        public string LoginUrl     { get; set; }
        public string PollEndpoint { get; set; }
        public string PollToken    { get; set; }
    }

    public class LoginFlowCredentials
    {
        public string Server      { get; set; }
        public string LoginName   { get; set; }
        public string AppPassword { get; set; }
    }
}
