using UnityEngine;

/// <summary>
/// Google Drive API 配置：OAuth 2.0 客户端 ID / Secret。
/// 在 Google Cloud Console 创建 OAuth 2.0 凭据后，创建本资产并填入。
/// 建议将本资产或所在目录加入 .gitignore，避免泄露密钥。
/// </summary>
[CreateAssetMenu(fileName = "GoogleDriveConfig", menuName = "Heatmap/Google Drive Config")]
public class GoogleDriveConfig : ScriptableObject
{
    [Tooltip("OAuth 2.0 Client ID（从 Google Cloud Console 获取）")]
    public string clientId = "";

    [Tooltip("OAuth 2.0 Client Secret")]
    public string clientSecret = "";

    [Tooltip("可选：上传到的云端文件夹 ID。留空则上传到「我的云端硬盘」根目录。在网盘打开该文件夹，URL 中 drive 后的即为 ID")]
    public string folderId = "";

    public bool HasCredentials => !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
}
