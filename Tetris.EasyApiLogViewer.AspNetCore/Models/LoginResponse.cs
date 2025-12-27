namespace Tetris.EasyApiLogViewer.AspNetCore.Models;

/// <summary>
/// 登录响应
/// </summary>
public class LoginResponse
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// JWT令牌
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// 令牌过期时间
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 用户信息
    /// </summary>
    public AdminInfo? Admin { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}