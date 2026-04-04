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
                var hasPreviewStr = prop.Element(XNamespace.Get("http://nextcloud.org/ns") + "has-preview")?.Value;
                bool hasPreview = hasPreviewStr == "true";

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
                    IsFavorite = isFavorite,
                    HasPreview = hasPreview
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

        public async Task<List<CloudFile>> SearchAsync(string query)
        {
            var result = new List<CloudFile>();
            var body = $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<d:searchrequest xmlns:d=""DAV:"" xmlns:oc=""http://owncloud.org/ns"" xmlns:nc=""http://nextcloud.org/ns"">
  <d:basicsearch>
    <d:select>
      <d:prop>
        <d:displayname/><d:getcontentlength/><d:getcontenttype/>
        <d:resourcetype/><d:getlastmodified/><d:etag/>
        <oc:id/><oc:size/><oc:permissions/><oc:favorite/>
      </d:prop>
    </d:select>
    <d:from>
      <d:scope>
        <d:href>/remote.php/dav/files/{_username}/</d:href>
        <d:depth>infinity</d:depth>
      </d:scope>
    </d:from>
    <d:where>
      <d:like>
        <d:prop><d:displayname/></d:prop>
        <d:literal>%{query}%</d:literal>
      </d:like>
    </d:where>
    <d:orderby/>
  </d:basicsearch>
</d:searchrequest>";

            var request = new HttpRequestMessage(new HttpMethod("SEARCH"),
                $"{_serverUrl}/remote.php/dav/files/{_username}/");
            request.Content = new StringContent(body, Encoding.UTF8, "text/xml");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(content);
            var basePath = $"/remote.php/dav/files/{_username}";

            foreach (var responseElem in doc.Root.Elements(DavNs + "response"))
            {
                var href = responseElem.Element(DavNs + "href")?.Value;
                if (href == null) continue;

                var decoded = Uri.UnescapeDataString(href);
                var parts = decoded.TrimEnd('/').Split('/');
                var fileName = parts.Length > 0 ? parts[parts.Length - 1] : "";
                if (string.IsNullOrEmpty(fileName)) continue;

                var relativePath = decoded.StartsWith(basePath)
                    ? decoded.Substring(basePath.Length)
                    : decoded;
                if (string.IsNullOrEmpty(relativePath)) relativePath = "/";

                var propStat = responseElem.Element(DavNs + "propstat");
                var prop = propStat?.Element(DavNs + "prop");
                if (prop == null) continue;

                var ocNs = XNamespace.Get("http://owncloud.org/ns");
                var isFolder = prop.Element(DavNs + "resourcetype")?.Element(DavNs + "collection") != null;
                var sizeStr = prop.Element(DavNs + "getcontentlength")?.Value ?? prop.Element(ocNs + "size")?.Value;
                long size = 0; long.TryParse(sizeStr, out size);
                DateTime modified = DateTime.MinValue;
                DateTime.TryParse(prop.Element(DavNs + "getlastmodified")?.Value, out modified);
                bool isFavorite = prop.Element(ocNs + "favorite")?.Value == "1";

                result.Add(new CloudFile
                {
                    Name = fileName,
                    Path = relativePath,
                    RemoteId = prop.Element(ocNs + "id")?.Value,
                    Size = size,
                    MimeType = prop.Element(DavNs + "getcontenttype")?.Value,
                    IsFolder = isFolder,
                    ModifiedDate = modified,
                    ETag = prop.Element(DavNs + "etag")?.Value?.Trim('"'),
                    Permissions = prop.Element(ocNs + "permissions")?.Value,
                    IsFavorite = isFavorite
                });
            }
            return result;
        }

        public async Task<List<TrashbinFile>> ListTrashbinAsync()
        {
            var result = new List<TrashbinFile>();
            var ncNs = XNamespace.Get("http://nextcloud.org/ns");

            var body = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<d:propfind xmlns:d=""DAV:"" xmlns:nc=""http://nextcloud.org/ns"" xmlns:oc=""http://owncloud.org/ns"">
  <d:prop>
    <d:resourcetype/>
    <d:getcontentlength/>
    <oc:size/>
    <nc:trashbin-filename/>
    <nc:trashbin-original-location/>
    <nc:trashbin-deletion-time/>
  </d:prop>
</d:propfind>";

            var request = new HttpRequestMessage(new HttpMethod("PROPFIND"),
                $"{_serverUrl}/remote.php/dav/trashbin/{_username}/trash/");
            request.Content = new StringContent(body, Encoding.UTF8, "application/xml");
            request.Headers.Add("Depth", "1");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return result;

            var content = await response.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(content);
            var ocNs = XNamespace.Get("http://owncloud.org/ns");

            foreach (var responseElem in doc.Root.Elements(DavNs + "response"))
            {
                var href = responseElem.Element(DavNs + "href")?.Value;
                if (href == null) continue;

                var decoded = Uri.UnescapeDataString(href);
                var trashPrefix = $"/remote.php/dav/trashbin/{_username}/trash/";
                if (decoded == trashPrefix || decoded == trashPrefix.TrimEnd('/')) continue;

                var propStat = responseElem.Element(DavNs + "propstat");
                var prop = propStat?.Element(DavNs + "prop");
                if (prop == null) continue;

                var isFolder = prop.Element(DavNs + "resourcetype")?.Element(DavNs + "collection") != null;
                var sizeStr = prop.Element(DavNs + "getcontentlength")?.Value ?? prop.Element(ocNs + "size")?.Value;
                long size = 0; long.TryParse(sizeStr, out size);

                var origName = prop.Element(ncNs + "trashbin-filename")?.Value;
                var origLoc = prop.Element(ncNs + "trashbin-original-location")?.Value;
                var delTimeStr = prop.Element(ncNs + "trashbin-deletion-time")?.Value;
                DateTime delTime = DateTime.MinValue;
                if (long.TryParse(delTimeStr, out long unixTime))
                    delTime = DateTimeOffset.FromUnixTimeSeconds(unixTime).LocalDateTime;

                // Extract just the item name from the href (the last segment)
                var segments = decoded.TrimEnd('/').Split('/');
                var itemName = segments.Length > 0 ? segments[segments.Length - 1] : decoded;

                result.Add(new TrashbinFile
                {
                    Name = origName ?? itemName,
                    OriginalFilename = origName ?? itemName,
                    OriginalLocation = origLoc,
                    TrashbinPath = decoded,
                    Size = size,
                    IsFolder = isFolder,
                    DeletionTime = delTime
                });
            }
            return result;
        }

        public async Task<bool> RestoreTrashbinFileAsync(string trashbinHref, string originalFilename)
        {
            var request = new HttpRequestMessage(new HttpMethod("MOVE"),
                $"{_serverUrl}{trashbinHref}");
            request.Headers.Add("Destination",
                $"{_serverUrl}/remote.php/dav/trashbin/{_username}/restore/{Uri.EscapeDataString(originalFilename)}");
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteTrashbinPermanentlyAsync(string trashbinHref)
        {
            var response = await _httpClient.DeleteAsync($"{_serverUrl}{trashbinHref}");
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> EmptyTrashbinAsync()
        {
            var response = await _httpClient.DeleteAsync(
                $"{_serverUrl}/remote.php/dav/trashbin/{_username}/trash/");
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
