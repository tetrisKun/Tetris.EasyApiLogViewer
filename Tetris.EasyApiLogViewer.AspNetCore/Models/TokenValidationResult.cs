namespace Tetris.EasyApiLogViewer.AspNetCore.Models;

/// <summary>
/// 令牌验证结果
/// </summary>
public class TokenValidationResult
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 管理员ID
    /// </summary>
    public long? AdminId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 角色
    /// </summary>
    public string? Role { get; set; }
}