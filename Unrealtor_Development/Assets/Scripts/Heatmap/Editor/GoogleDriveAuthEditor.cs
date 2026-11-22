using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor 菜单：通过 OAuth 2.0 连接 Google Drive，将 refresh_token 写入与运行时共用的路径，供 HeatmapRenderer 上传使用。
/// </summary>
public static class GoogleDriveAuthEditor
{
    const int CallbackPort = 8765;
    const string CallbackPath = "/";
    static readonly string RedirectUri = "http://localhost:" + CallbackPort + CallbackPath;
    const string Scope = "https://www.googleapis.com/auth/drive.file";
    const string AuthUrlTemplate = "https://accounts.google.com/o/oauth2/v2/auth?client_id={0}&redirect_uri={1}&response_type=code&scope={2}&access_type=offline&prompt=consent";

    [MenuItem("Tools/Heatmap/Connect Google Drive")]
    public static void ConnectGoogleDrive()
    {
        GoogleDriveConfig config = FindGoogleDriveConfig();
        if (config == null || !config.HasCredentials)
        {
            EditorUtility.DisplayDialog("Google Drive", "请先创建 Google Drive 配置资产并填写 Client ID 与 Client Secret。\n\n在 Project 窗口右键 → Create → Heatmap → Google Drive Config，然后在该资产的 Inspector 中填入从 Google Cloud Console 获取的 OAuth 2.0 凭据。", "确定");
            return;
        }

        string authUrl = string.Format(AuthUrlTemplate,
            Uri.EscapeDataString(config.clientId.Trim()),
            Uri.EscapeDataString(RedirectUri),
            Uri.EscapeDataString(Scope));

        Application.OpenURL(authUrl);

        HttpListener listener = null;
        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:" + CallbackPort + "/");
            listener.Start();

            EditorUtility.DisplayDialog("Google Drive", "浏览器已打开，请使用 Google 账号登录并授权。\n授权完成后会跳转回本机，若未自动关闭请回到 Unity。", "确定");

            var context = listener.GetContext();
            string code = context.Request.QueryString["code"];
            string responseHtml = "<html><body style='font-family:sans-serif;'><p>授权成功，请关闭此页并回到 Unity。</p></body></html>";
            byte[] buf = Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentLength64 = buf.Length;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.OutputStream.Write(buf, 0, buf.Length);
            context.Response.Close();

            if (string.IsNullOrEmpty(code))
            {
                EditorUtility.DisplayDialog("Google Drive", "未收到授权码，请确认在浏览器中完成了授权并同意访问。", "确定");
                return;
            }

            string refreshToken = ExchangeCodeForTokens(config.clientId.Trim(), config.clientSecret.Trim(), code);
            if (string.IsNullOrEmpty(refreshToken))
            {
                EditorUtility.DisplayDialog("Google Drive", "用授权码换取 Token 失败，请检查 Client ID/Secret 与重定向 URI 是否与 Google Cloud Console 中一致（http://localhost:8765/）。", "确定");
                return;
            }

            string tokenPath = GoogleDriveClient.GetRefreshTokenPath();
            string dir = Path.GetDirectoryName(tokenPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(tokenPath, refreshToken);

            EditorUtility.DisplayDialog("Google Drive", "已连接 Google Drive。热力图保存时若勾选「上传到 Google Drive」，将自动上传到云端。", "确定");
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Google Drive", "连接过程出错: " + ex.Message + "\n\n请确认未占用端口 " + CallbackPort + "，且 Google Cloud 中已添加重定向 URI: " + RedirectUri, "确定");
        }
        finally
        {
            try { listener?.Stop(); } catch { }
        }
    }

    static string ExchangeCodeForTokens(string clientId, string clientSecret, string code)
    {
        string body = "code=" + Uri.EscapeDataString(code)
            + "&client_id=" + Uri.EscapeDataString(clientId)
            + "&client_secret=" + Uri.EscapeDataString(clientSecret)
            + "&redirect_uri=" + Uri.EscapeDataString(RedirectUri)
            + "&grant_type=authorization_code";

        var req = (HttpWebRequest)WebRequest.Create("https://oauth2.googleapis.com/token");
        req.Method = "POST";
        req.ContentType = "application/x-www-form-urlencoded";
        req.ContentLength = Encoding.UTF8.GetByteCount(body);
        req.GetRequestStream().Write(Encoding.UTF8.GetBytes(body), 0, Encoding.UTF8.GetByteCount(body));

        using (var resp = (HttpWebResponse)req.GetResponse())
        using (var reader = new StreamReader(resp.GetResponseStream()))
        {
            string json = reader.ReadToEnd();
            return ParseRefreshToken(json);
        }
    }

    static string ParseRefreshToken(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        int i = json.IndexOf("\"refresh_token\"");
        if (i < 0) return null;
        i = json.IndexOf('"', i + 16) + 1;
        int j = json.IndexOf('"', i);
        return j > i ? json.Substring(i, j - i) : null;
    }

    static GoogleDriveConfig FindGoogleDriveConfig()
    {
        string[] guids = AssetDatabase.FindAssets("t:GoogleDriveConfig");
        if (guids == null || guids.Length == 0) return null;
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<GoogleDriveConfig>(path);
    }
}
