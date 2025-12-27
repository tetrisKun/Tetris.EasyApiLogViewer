using Tetris.EasyApiLogViewer.AspNetCore.Models;
using Tetris.EasyApiLogViewer.Db.Abstract.Models;

namespace Tetris.EasyApiLogViewer.AspNetCore.Services;

/// <summary>
/// 管理员认证服务接口
/// </summary>
public interface IAdminAuthService
{
    /// <summary>
    /// 登录
    /// </summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证令牌
    /// </summary>
    TokenValidationResult ValidateToken(string? token);

    /// <summary>
    /// 修改密码
    /// </summary>
    Task<bool> ChangePasswordAsync(long adminId, ChangePasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取管理员信息
    /// </summary>
    Task<AdminInfo?> GetAdminInfoAsync(long adminId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建密码哈希
    /// </summary>
    (string hash, string salt) CreatePasswordHash(string password);

    /// <summary>
    /// 验证密码
    /// </summary>
    bool VerifyPassword(string password, string hash, string salt);
}
