using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 独立运行器：在上传协程不随 HeatmapRenderer 被禁用而中断（如切场景、OnDisable 时保存）。
/// </summary>
public class GoogleDriveUploadRunner : MonoBehaviour
{
    static GoogleDriveUploadRunner _instance;
    public static GoogleDriveUploadRunner Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("GoogleDriveUploadRunner");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<GoogleDriveUploadRunner>();
            }
            return _instance;
        }
    }

    public static void Run(IEnumerator coroutine)
    {
        Instance.StartCoroutine(coroutine);
    }
}

/// <summary>
/// 使用 Google Drive API v3 上传文件（需先通过 Editor 菜单完成 OAuth 连接）。
/// </summary>
public static class GoogleDriveClient
{
    const string TokenPath = "HeatmapGoogleDrive_RefreshToken.txt";
    const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    const string UploadEndpoint = "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart";

    public static string GetRefreshTokenPath()
    {
        return Path.Combine(Application.persistentDataPath, TokenPath);
    }

    /// <summary> 从本地保存的文件读取 refresh_token（由 Editor 连接时写入） </summary>
    public static string ReadStoredRefreshToken()
    {
        string path = GetRefreshTokenPath();
        if (!File.Exists(path)) return null;
        try
        {
            string raw = File.ReadAllText(path).Trim();
            int i = raw.IndexOf('|');
            return i >= 0 ? raw.Substring(0, i).Trim() : raw;
        }
        catch { return null; }
    }

    /// <summary> 使用 refresh_token 换取 access_token（协程内执行，避免主线程阻塞导致请求无法完成） </summary>
    public static IEnumerator GetAccessTokenCoroutine(string clientId, string clientSecret, string refreshToken, Action<string> onResult)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            onResult?.Invoke(null);
            yield break;
        }
        var form = new WWWForm();
        form.AddField("client_id", clientId);
        form.AddField("client_secret", clientSecret);
        form.AddField("refresh_token", refreshToken);
        form.AddField("grant_type", "refresh_token");
        using (var req = UnityWebRequest.Post(TokenEndpoint, form))
        {
            req.timeout = 15;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[GoogleDrive] Token 请求失败: " + req.error + " | " + (req.downloadHandler?.text ?? ""));
                onResult?.Invoke(null);
                yield break;
            }
            onResult?.Invoke(ParseAccessToken(req.downloadHandler.text));
        }
    }

    static string ParseAccessToken(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        int i = json.IndexOf("\"access_token\"");
        if (i < 0) return null;
        i = json.IndexOf('"', i + 15) + 1;
        int j = json.IndexOf('"', i);
        return j > i ? json.Substring(i, j - i) : null;
    }

    /// <summary> 上传单个文件到 Google Drive；parentFolderId 可为空表示根目录。 </summary>
    public static IEnumerator UploadFileCoroutine(string filePath, string fileName, string mimeType, string parentFolderId, string accessToken, Action<bool, string> onDone)
    {
        if (!File.Exists(filePath))
        {
            onDone?.Invoke(false, "File not found: " + filePath);
            yield break;
        }

        byte[] fileBytes = File.ReadAllBytes(filePath);
        string boundary = "heatmap_upload_" + DateTime.UtcNow.Ticks;

        var metadata = new StringBuilder();
        metadata.Append("{ \"name\": \"").Append(EscapeJson(fileName)).Append("\"");
        if (!string.IsNullOrWhiteSpace(parentFolderId))
            metadata.Append(", \"parents\": [ \"").Append(parentFolderId.Trim()).Append("\" ]");
        metadata.Append(" }");

        var body = new MemoryStream();
        byte[] nl = Encoding.UTF8.GetBytes("\r\n");
        byte[] sep = Encoding.UTF8.GetBytes("--" + boundary + "\r\n");
        byte[] end = Encoding.UTF8.GetBytes("\r\n--" + boundary + "--\r\n");

        body.Write(sep, 0, sep.Length);
        body.Write(Encoding.UTF8.GetBytes("Content-Type: application/json; charset=UTF-8\r\n\r\n"), 0, 46);
        body.Write(Encoding.UTF8.GetBytes(metadata.ToString()), 0, Encoding.UTF8.GetByteCount(metadata.ToString()));
        body.Write(nl, 0, nl.Length);
        body.Write(sep, 0, sep.Length);
        body.Write(Encoding.UTF8.GetBytes("Content-Type: " + mimeType + "\r\n\r\n"), 0, Encoding.UTF8.GetByteCount("Content-Type: " + mimeType + "\r\n\r\n"));
        body.Write(fileBytes, 0, fileBytes.Length);
        body.Write(end, 0, end.Length);

        byte[] bodyBytes = body.ToArray();
        using (var req = new UnityWebRequest(UploadEndpoint, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + accessToken);
            req.SetRequestHeader("Content-Type", "multipart/related; boundary=" + boundary);
            req.timeout = 60;

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(true, null);
            }
            else
            {
                string err = req.error + (req.downloadHandler?.text ?? "");
                onDone?.Invoke(false, err);
            }
        }
    }

    static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
    }

    /// <summary> 将 session 的多个文件上传到 Drive；由 HeatmapRenderer 在保存后调用。 </summary>
    public static IEnumerator UploadSessionToDrive(GoogleDriveConfig config, string basePath, bool hadJson, bool hadCsv, bool hadPng, Action<bool, string> onComplete)
    {
        if (config == null || !config.HasCredentials)
        {
            Debug.LogWarning("[HeatmapRenderer] Google Drive 未上传: 未配置或 Client ID/Secret 为空。");
            onComplete?.Invoke(false, "Google Drive config missing or credentials empty.");
            yield break;
        }

        string tokenPath = GetRefreshTokenPath();
        string refreshToken = ReadStoredRefreshToken();
        if (string.IsNullOrEmpty(refreshToken))
        {
            Debug.LogWarning("[HeatmapRenderer] Google Drive 未上传: 未找到已保存的登录信息。请在 Unity 菜单执行 Tools → Heatmap → Connect Google Drive，Token 文件路径: " + tokenPath);
            onComplete?.Invoke(false, "Not connected to Google Drive. Use menu: Tools → Heatmap → Connect Google Drive");
            yield break;
        }

        string accessToken = null;
        yield return GetAccessTokenCoroutine(config.clientId, config.clientSecret, refreshToken, (t) => accessToken = t);
        if (string.IsNullOrEmpty(accessToken))
        {
            Debug.LogWarning("[HeatmapRenderer] Google Drive 未上传: 获取 Access Token 失败，请重新执行 Tools → Heatmap → Connect Google Drive。");
            onComplete?.Invoke(false, "Failed to get access token. Try reconnecting: Tools → Heatmap → Connect Google Drive");
            yield break;
        }

        Debug.Log("[HeatmapRenderer] 开始上传到 Google Drive: " + Path.GetFileName(basePath));

        string folderId = string.IsNullOrWhiteSpace(config.folderId) ? null : config.folderId.Trim();
        string sessionName = Path.GetFileName(basePath);
        int done = 0;
        int total = (hadJson ? 1 : 0) + (hadCsv ? 1 : 0) + (hadPng ? 1 : 0);
        bool anyFail = false;
        string lastError = null;

        if (hadJson)
        {
            string path = basePath + ".json";
            yield return UploadFileCoroutine(path, sessionName + ".json", "application/json", folderId, accessToken, (ok, err) =>
            {
                done++;
                if (!ok) { anyFail = true; lastError = err; }
            });
        }

        if (hadCsv)
        {
            string path = basePath + "_samples.csv";
            yield return UploadFileCoroutine(path, sessionName + "_samples.csv", "text/csv", folderId, accessToken, (ok, err) =>
            {
                done++;
                if (!ok) { anyFail = true; lastError = err; }
            });
        }

        if (hadPng)
        {
            string path = basePath + ".png";
            yield return UploadFileCoroutine(path, sessionName + ".png", "image/png", folderId, accessToken, (ok, err) =>
            {
                done++;
                if (!ok) { anyFail = true; lastError = err; }
            });
        }

        onComplete?.Invoke(!anyFail, anyFail ? lastError : null);
    }

    /// <summary> 同步上传（用于 OnApplicationQuit，避免协程被退出流程打断）。 </summary>
    public static void UploadSessionToDriveSync(GoogleDriveConfig config, string basePath, bool hadJson, bool hadCsv, bool hadPng, Action<bool, string> onComplete)
    {
        if (config == null || !config.HasCredentials)
        {
            Debug.LogWarning("[HeatmapRenderer] Google Drive 未上传: 未配置或 Client ID/Secret 为空。");
            onComplete?.Invoke(false, "Google Drive config missing or credentials empty.");
            return;
        }

        string refreshToken = ReadStoredRefreshToken();
        if (string.IsNullOrEmpty(refreshToken))
        {
            Debug.LogWarning("[HeatmapRenderer] Google Drive 未上传: 未找到已保存的登录信息。请执行 Tools → Heatmap → Connect Google Drive");
            onComplete?.Invoke(false, "Not connected to Google Drive.");
            return;
        }

        string accessToken = GetAccessTokenSync(config.clientId, config.clientSecret, refreshToken);
        if (string.IsNullOrEmpty(accessToken))
        {
            Debug.LogWarning("[HeatmapRenderer] Google Drive 未上传: 获取 Access Token 失败。");
            onComplete?.Invoke(false, "Failed to get access token.");
            return;
        }

        Debug.Log("[HeatmapRenderer] 开始同步上传到 Google Drive: " + Path.GetFileName(basePath));
        string folderId = string.IsNullOrWhiteSpace(config.folderId) ? null : config.folderId.Trim();
        string sessionName = Path.GetFileName(basePath);
        bool anyFail = false;
        string lastError = null;

        if (hadJson && !UploadFileSync(basePath + ".json", sessionName + ".json", "application/json", folderId, accessToken, out lastError))
            anyFail = true;
        if (hadCsv && !UploadFileSync(basePath + "_samples.csv", sessionName + "_samples.csv", "text/csv", folderId, accessToken, out lastError))
            anyFail = true;
        if (hadPng && !UploadFileSync(basePath + ".png", sessionName + ".png", "image/png", folderId, accessToken, out lastError))
            anyFail = true;

        onComplete?.Invoke(!anyFail, anyFail ? lastError : null);
    }

    static string GetAccessTokenSync(string clientId, string clientSecret, string refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken)) return null;
        string body = "client_id=" + Uri.EscapeDataString(clientId) + "&client_secret=" + Uri.EscapeDataString(clientSecret)
            + "&refresh_token=" + Uri.EscapeDataString(refreshToken) + "&grant_type=refresh_token";
        try
        {
            var req = (HttpWebRequest)WebRequest.Create(TokenEndpoint);
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            req.ContentLength = Encoding.UTF8.GetByteCount(body);
            req.Timeout = 15000;
            req.ReadWriteTimeout = 15000;
            using (var stream = req.GetRequestStream())
                stream.Write(Encoding.UTF8.GetBytes(body), 0, Encoding.UTF8.GetByteCount(body));
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var reader = new StreamReader(resp.GetResponseStream()))
                return ParseAccessToken(reader.ReadToEnd());
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[GoogleDrive] Token 同步请求异常: " + ex.Message);
            return null;
        }
    }

    static bool UploadFileSync(string filePath, string fileName, string mimeType, string parentFolderId, string accessToken, out string error)
    {
        error = null;
        if (!File.Exists(filePath)) { error = "File not found: " + filePath; return false; }
        byte[] fileBytes = File.ReadAllBytes(filePath);
        const string boundary = "heatmap_drive_boundary";
        var metadata = new StringBuilder();
        metadata.Append("{\"name\":\"").Append(EscapeJson(fileName)).Append("\"");
        if (!string.IsNullOrWhiteSpace(parentFolderId))
            metadata.Append(",\"parents\":[\"").Append(parentFolderId.Trim()).Append("\"]");
        metadata.Append("}");
        byte[] metadataBytes = Encoding.UTF8.GetBytes(metadata.ToString());

        var body = new MemoryStream();
        byte[] crlf = Encoding.UTF8.GetBytes("\r\n");
        void WriteString(string s) { byte[] b = Encoding.UTF8.GetBytes(s); body.Write(b, 0, b.Length); }
        WriteString("--"); WriteString(boundary); body.Write(crlf, 0, 2);
        WriteString("Content-Type: application/json; charset=UTF-8"); body.Write(crlf, 0, 2); body.Write(crlf, 0, 2);
        body.Write(metadataBytes, 0, metadataBytes.Length);
        body.Write(crlf, 0, 2);
        WriteString("--"); WriteString(boundary); body.Write(crlf, 0, 2);
        WriteString("Content-Type: "); WriteString(mimeType); body.Write(crlf, 0, 2); body.Write(crlf, 0, 2);
        body.Write(fileBytes, 0, fileBytes.Length);
        body.Write(crlf, 0, 2);
        WriteString("--"); WriteString(boundary); WriteString("--"); body.Write(crlf, 0, 2);
        byte[] bodyBytes = body.ToArray();

        try
        {
            var req = (HttpWebRequest)WebRequest.Create(UploadEndpoint);
            req.Method = "POST";
            req.ContentType = "multipart/related; boundary=" + boundary;
            req.ContentLength = bodyBytes.Length;
            req.Headers.Add("Authorization", "Bearer " + accessToken);
            req.Timeout = 60000;
            req.ReadWriteTimeout = 60000;
            req.SendChunked = false;
            if (req.ServicePoint != null)
                req.ServicePoint.Expect100Continue = false;
            using (var stream = req.GetRequestStream())
                stream.Write(bodyBytes, 0, bodyBytes.Length);
            using (var resp = (HttpWebResponse)req.GetResponse())
            {
                if (resp.StatusCode == HttpStatusCode.OK) return true;
                using (var reader = new StreamReader(resp.GetResponseStream()))
                    error = "HTTP " + (int)resp.StatusCode + ": " + reader.ReadToEnd();
                return false;
            }
        }
        catch (WebException we)
        {
            try
            {
                if (we.Response != null)
                using (var r = new StreamReader(we.Response.GetResponseStream()))
                    error = we.Message + " | " + r.ReadToEnd();
                else
                    error = we.Message;
            }
            catch { error = we.Message; }
            return false;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }
}
