namespace Tetris.EasyApiLogViewer.AspNetCore.Models;

/// <summary>
/// 管理员信息（不含敏感字段）
/// </summary>
public class AdminInfo
{
    /// <summary>
    /// 账户ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 角色
    /// </summary>
    public string Role { get; set; } = string.Empty;
}