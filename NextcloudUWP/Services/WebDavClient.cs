using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using NextcloudUWP.Models;

namespace NextcloudUWP.Services
{
    public class WebDavClient
    {
        private static readonly XNamespace DavNs = "DAV:";
        private readonly HttpClient _httpClient;
        private string _serverUrl;
        private string _username;

        public WebDavClient()
        {
            _httpClient = new HttpClient();
        }

        public void Configure(string serverUrl, string username, string password)
        {
            _serverUrl = serverUrl.TrimEnd('/');
            _username = username;

            var authBytes = Encoding.UTF8.GetBytes($"{username}:{password}");
            var authHeader = Convert.ToBase64String(authBytes);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
        }

        public async Task<List<CloudFile>> ListFilesAsync(string path)
        {
            var result = new List<CloudFile>();

            var propfindBody = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<d:propfind xmlns:d=""DAV:"" xmlns:oc=""http://owncloud.org/ns"" xmlns:nc=""http://nextcloud.org/ns"">
    <d:prop>
        <d:getlastmodified />
        <d:getcontenttype />
        <d:getcontentlength />
        <d:resourcetype />
        <d:etag />
        <oc:id />
        <oc:size />
        <oc:permissions />
        <oc:favorite />
        <nc:has-preview />
    </d:prop>
</d:propfind>";

            var request = new HttpRequestMessage(new HttpMethod("PROPFIND"),
                $"{_serverUrl}/remote.php/dav/files/{_username}{NormalizePath(path)}");
            request.Content = new StringContent(propfindBody, Encoding.UTF8, "application/xml");
            request.Headers.Add("Depth", "1");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(content);

            foreach (var responseElem in doc.Root.Elements(DavNs + "response"))
            {
                var href = responseElem.Element(DavNs + "href")?.Value;
                if (href == null) continue;

                var decodedHref = Uri.UnescapeDataString(href);
                var name = decodedHref.TrimEnd('/').Split('/');
                var fileName = name.Length > 0 ? name[name.Length - 1] : "";

                if (string.IsNullOrEmpty(fileName)) continue;

                var basePath = $"/remote.php/dav/files/{_username}";
                var relativePath = decodedHref;
                if (relativePath.StartsWith(basePath))
                    relativePath = relativePath.Substring(basePath.Length);
                if (string.IsNullOrEmpty(relativePath))
                    relativePath = "/";

                var propStat = responseElem.Element(DavNs + "propstat");
                var prop = propStat?.Element(DavNs + "prop");
                if (prop == null) continue;

                var isFolder = prop.Element(DavNs + "resourcetype")?.Element(DavNs + "collection") != null;
                var sizeStr = prop.Element(DavNs + "getcontentlength")?.Value
                    ?? prop.Element(XNamespace.Get("http://owncloud.org/ns") + "size")?.Value;
                long size = 0;
                long.TryParse(sizeStr, out size);

                var modifiedStr = prop.Element(DavNs + "getlastmodified")?.Value;
                DateTime modified = DateTime.MinValue;
                DateTime.TryParse(modifiedStr, out modified);

                var mimeType = prop.Element(DavNs + "getcontenttype")?.Value;
                var etag = prop.Element(DavNs + "etag")?.Value?.Trim('"');
                var remoteId = prop.Element(XNamespace.Get("http://owncloud.org/ns") + "id")?.Value;
                var permissions = prop.Element(XNamespace.Get("http://owncloud.org/ns") + "permissions")?.Value;
                var favoriteStr = prop.Element(XNamespace.Get("http://owncloud.org/ns") + "favorite")?.Value;
                bool isFavorite = favoriteStr == "1";

                result.Add(new CloudFile
                {
                    Name = fileName,
                    Path = relativePath,
                    RemoteId = remoteId,
                    Size = size,
                    MimeType = mimeType,
                    IsFolder = isFolder,
                    ModifiedDate = modified,
                    ETag = etag,
                    Permissions = permissions,
                    IsFavorite = isFavorite
                });
            }

            return result;
        }

        public async Task<bool> UploadFileAsync(string remotePath, Stream fileStream, string contentType)
        {
            var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? "application/octet-stream");

            var response = await _httpClient.PutAsync(
                $"{_serverUrl}/remote.php/dav/files/{_username}{NormalizePath(remotePath)}",
                content);
            return response.IsSuccessStatusCode;
        }

        public async Task<Stream> DownloadFileAsync(string remotePath)
        {
            var response = await _httpClient.GetAsync(
                $"{_serverUrl}/remote.php/dav/files/{_username}{NormalizePath(remotePath)}",
                HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync();
        }

        public async Task<bool> DeleteFileAsync(string remotePath)
        {
            var response = await _httpClient.DeleteAsync(
                $"{_serverUrl}/remote.php/dav/files/{_username}{NormalizePath(remotePath)}");
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> MoveFileAsync(string sourcePath, string destPath, bool overwrite = false)
        {
            var request = new HttpRequestMessage(new HttpMethod("MOVE"),
                $"{_serverUrl}/remote.php/dav/files/{_username}{NormalizePath(sourcePath)}");
            request.Headers.Add("Destination",
                $"{_serverUrl}/remote.php/dav/files/{_username}{NormalizePath(destPath)}");
            if (overwrite)
                request.Headers.Add("Overwrite", "T");
            else
                request.Headers.Add("Overwrite", "F");

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> CopyFileAsync(string sourcePath, string destPath, bool overwrite = false)
        {
            var request = new HttpRequestMessage(new HttpMethod("COPY"),
                $"{_serverUrl}/remote.php/dav/files/{_username}{NormalizePath(sourcePath)}");
            request.Headers.Add("Destination",
                $"{_serverUrl}/remote.php/dav/files/{_username}{NormalizePath(destPath)}");
            if (overwrite)
                request.Headers.Add("Overwrite", "T");
            else
                request.Headers.Add("Overwrite", "F");

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> CreateFolderAsync(string folderPath)
        {
            var request = new HttpRequestMessage(new HttpMethod("MKCOL"),
                $"{_serverUrl}/remote.php/dav/files/{_username}{NormalizePath(folderPath)}");
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "/";
            if (!path.StartsWith("/")) path = "/" + path;
            return path;
        }
    }
}
