using System;
using System.Collections.Generic;
using Windows.Storage;
using Windows.Security.Credentials;
using Newtonsoft.Json;
using NextcloudUWP.Models;

namespace NextcloudUWP.Services
{
    public class SettingsService
    {
        private const string KEY_ACCOUNTS = "Accounts";
        private const string VAULT_RESOURCE = "NextcloudUWP";
        private const string LEGACY_KEY_SERVER = "ServerUrl";
        private const string LEGACY_KEY_USER = "Username";
        private const string LEGACY_KEY_PASS = "Password";

        private readonly ApplicationDataContainer _local;
        private readonly PasswordVault _vault;

        public SettingsService()
        {
            _local = ApplicationData.Current.LocalSettings;
            _vault = new PasswordVault();
        }

        // ── Multi-account storage ────────────────────────────────────────

        public List<UserAccount> GetAccounts()
        {
            var json = _local.Values[KEY_ACCOUNTS] as string;
            if (string.IsNullOrEmpty(json)) return new List<UserAccount>();
            try { return JsonConvert.DeserializeObject<List<UserAccount>>(json); }
            catch { return new List<UserAccount>(); }
        }

        public void SaveAccount(UserAccount account)
        {
            var accounts = GetAccounts();
            var idx = accounts.FindIndex(a =>
                a.ServerUrl == account.ServerUrl && a.Username == account.Username);

            StoreCredential(account.ServerUrl, account.Username, account.Password);

            var sanitized = SanitizeForStorage(account);
            if (idx >= 0) accounts[idx] = sanitized;
            else accounts.Add(sanitized);
            Persist(accounts);
        }

        public void RemoveAccount(string serverUrl, string username)
        {
            RemoveCredential(serverUrl, username);
            var accounts = GetAccounts();
            accounts.RemoveAll(a => a.ServerUrl == serverUrl && a.Username == username);
            if (accounts.Count > 0 && !accounts.Exists(a => a.IsActive))
                accounts[0].IsActive = true;
            Persist(accounts);
        }

        public UserAccount GetActiveAccount()
        {
            var account = GetAccounts().Find(a => a.IsActive);
            if (account != null) account.Password = RetrieveCredential(account.ServerUrl, account.Username);
            return account;
        }

        public void SetActiveAccount(string serverUrl, string username)
        {
            var accounts = GetAccounts();
            foreach (var a in accounts)
                a.IsActive = (a.ServerUrl == serverUrl && a.Username == username);
            Persist(accounts);
        }

        /// <summary>Adds or updates an account and makes it the active one.</summary>
        public void AddAccount(string serverUrl, string username, string password,
                               string displayName = null, string email = null,
                               long quotaUsed = 0, long quotaTotal = 0)
        {
            var accounts = GetAccounts();
            foreach (var a in accounts) a.IsActive = false;
            Persist(accounts);

            SaveAccount(new UserAccount
            {
                ServerUrl = serverUrl,
                Username = username,
                Password = password,
                DisplayName = displayName,
                Email = email,
                QuotaUsed = quotaUsed,
                QuotaTotal = quotaTotal,
                IsActive = true
            });
        }

        // ── PasswordVault helpers ─────────────────────────────────────────

        private void StoreCredential(string serverUrl, string username, string password)
        {
            if (string.IsNullOrEmpty(password)) return;
            RemoveCredential(serverUrl, username);
            _vault.Add(new PasswordCredential(VAULT_RESOURCE, $"{serverUrl}:{username}", password));
        }

        private string RetrieveCredential(string serverUrl, string username)
        {
            try
            {
                var cred = _vault.Retrieve(VAULT_RESOURCE, $"{serverUrl}:{username}");
                cred.RetrievePassword();
                return cred.Password;
            }
            catch { return null; }
        }

        private void RemoveCredential(string serverUrl, string username)
        {
            try
            {
                var cred = _vault.Retrieve(VAULT_RESOURCE, $"{serverUrl}:{username}");
                _vault.Remove(cred);
            }
            catch { }
        }

        private static UserAccount SanitizeForStorage(UserAccount account)
        {
            return new UserAccount
            {
                Id = account.Id,
                ServerUrl = account.ServerUrl,
                Username = account.Username,
                Password = null,
                AccessToken = null,
                DisplayName = account.DisplayName,
                Email = account.Email,
                QuotaUsed = account.QuotaUsed,
                QuotaTotal = account.QuotaTotal,
                IsActive = account.IsActive
            };
        }

        // ── Auto-upload settings ────────────────────────────────────────

        public string AutoUploadRemotePath
        {
            get => _local.Values["AutoUploadRemotePath"] as string ?? "/Photos/AutoUpload";
            set => _local.Values["AutoUploadRemotePath"] = value;
        }

        public string AutoUploadLastSync
        {
            get => _local.Values["AutoUploadLastSync"] as string;
            set => _local.Values["AutoUploadLastSync"] = value;
        }

        // ── Background task settings ────────────────────────────────────

        public bool NotificationsEnabled
        {
            get => (_local.Values["NotificationsEnabled"] as bool?) ?? false;
            set => _local.Values["NotificationsEnabled"] = value;
        }

        public bool AutoSyncEnabled
        {
            get => (_local.Values["AutoSyncEnabled"] as bool?) ?? false;
            set => _local.Values["AutoSyncEnabled"] = value;
        }

        // FutureAccessList token for the auto-upload source folder.
        public string AutoSyncFolderToken
        {
            get => _local.Values["AutoSyncFolderToken"] as string;
            set => _local.Values["AutoSyncFolderToken"] = value;
        }

        // Highest notification ID already shown as a toast — prevents re-toasting on next poll.
        public int LastSeenNotificationId
        {
            get => (_local.Values["LastSeenNotificationId"] as int?) ?? 0;
            set => _local.Values["LastSeenNotificationId"] = value;
        }

        // ── Security settings ───────────────────────────────────────────

        public bool AppLockEnabled
        {
            get => (_local.Values["AppLockEnabled"] as bool?) ?? false;
            set => _local.Values["AppLockEnabled"] = value;
        }

        // When set, the SHA-256 thumbprint of the pinned TLS cert for the active server.
        public string PinnedCertThumbprint
        {
            get => _local.Values["PinnedCertThumbprint"] as string;
            set => _local.Values["PinnedCertThumbprint"] = value;
        }

        // ── Sync settings ───────────────────────────────────────────────

        public long SyncMaxFileSize
        {
            get => (_local.Values["SyncMaxFileSize"] as long?) ?? 0;
            set => _local.Values["SyncMaxFileSize"] = value;
        }

        public List<string> SyncFileExtensions
        {
            get
            {
                var json = _local.Values["SyncFileExtensions"] as string;
                if (string.IsNullOrEmpty(json)) return new List<string>();
                try { return JsonConvert.DeserializeObject<List<string>>(json); }
                catch { return new List<string>(); }
            }
            set => _local.Values["SyncFileExtensions"] = JsonConvert.SerializeObject(value);
        }

        // ── Convenience props (active account) ──────────────────────────

        public bool HasCredentials => GetActiveAccount() != null;
        public string ServerUrl => GetActiveAccount()?.ServerUrl;
        public string Username => GetActiveAccount()?.Username;
        public string Password => GetActiveAccount()?.Password;

        // ── Legacy single-account migration ─────────────────────────────

        public void MigrateLegacyCredentials()
        {
            if (_local.Values.ContainsKey(KEY_ACCOUNTS)) return;
            if (!_local.Values.ContainsKey(LEGACY_KEY_SERVER)) return;

            var url  = _local.Values[LEGACY_KEY_SERVER] as string;
            var user = _local.Values[LEGACY_KEY_USER]   as string;
            var pass = _local.Values[LEGACY_KEY_PASS]   as string;

            if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(user))
                AddAccount(url, user, pass);

            _local.Values.Remove(LEGACY_KEY_SERVER);
            _local.Values.Remove(LEGACY_KEY_USER);
            _local.Values.Remove(LEGACY_KEY_PASS);
        }

        private void Persist(List<UserAccount> accounts)
        {
            _local.Values[KEY_ACCOUNTS] = JsonConvert.SerializeObject(accounts);
        }
    }
}
