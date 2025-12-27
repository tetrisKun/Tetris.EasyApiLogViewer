using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Tetris.EasyApiLogViewer.AspNetCore.Models;
using Tetris.EasyApiLogViewer.AspNetCore.Services;

namespace Tetris.EasyApiLogViewer.AspNetCore.Controllers;

/// <summary>
/// 管理员认证控制器
/// </summary>
[ApiController]
[Route("api/logs/auth")]
public class AdminAuthController : ControllerBase
{
    private readonly IAdminAuthService _authService;
    private readonly ILogger<AdminAuthController> _logger;

    /// <summary>
    /// 初始化管理员认证控制器
    /// </summary>
    /// <param name="authService">管理员认证服务</param>
    /// <param name="logger">日志记录器</param>
    public AdminAuthController(
        IAdminAuthService authService,
        ILogger<AdminAuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// 管理员登录
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new { success = false, message = "用户名和密码不能为空" });
        }

        var result = await _authService.LoginAsync(request);

        if (!result.Success)
        {
            return Unauthorized(new
            {
                success = false,
                message = result.ErrorMessage
            });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                token = result.Token,
                expiresAt = result.ExpiresAt,
                admin = result.Admin
            }
        });
    }

    /// <summary>
    /// 获取当前登录信息
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentAdmin()
    {
        var token = GetTokenFromRequest();
        var validation = _authService.ValidateToken(token);

        if (!validation.IsValid || !validation.AdminId.HasValue)
        {
            return Unauthorized(new { success = false, message = "未授权" });
        }

        var adminInfo = await _authService.GetAdminInfoAsync(validation.AdminId.Value);
        if (adminInfo == null)
        {
            return NotFound(new { success = false, message = "管理员不存在" });
        }

        return Ok(new
        {
            success = true,
            data = adminInfo
        });
    }

    /// <summary>
    /// 修改密码
    /// </summary>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var token = GetTokenFromRequest();
        var validation = _authService.ValidateToken(token);

        if (!validation.IsValid || !validation.AdminId.HasValue)
        {
            return Unauthorized(new { success = false, message = "未授权" });
        }

        if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword))
        {
            return BadRequest(new { success = false, message = "当前密码和新密码不能为空" });
        }

        if (request.NewPassword.Length < 6)
        {
            return BadRequest(new { success = false, message = "新密码至少6个字符" });
        }

        var result = await _authService.ChangePasswordAsync(validation.AdminId.Value, request);

        if (!result)
        {
            return BadRequest(new { success = false, message = "当前密码错误" });
        }

        return Ok(new { success = true, message = "密码修改成功" });
    }

    /// <summary>
    /// 验证令牌
    /// </summary>
    [HttpGet("validate")]
    public IActionResult ValidateToken()
    {
        var token = GetTokenFromRequest();
        var validation = _authService.ValidateToken(token);

        return Ok(new
        {
            success = true,
            data = new
            {
                isValid = validation.IsValid,
                username = validation.Username,
                role = validation.Role
            }
        });
    }

    private string? GetTokenFromRequest()
    {
        // 从Authorization头获取
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader["Bearer ".Length..].Trim();
        }

        // 从查询参数获取
        return Request.Query["token"].FirstOrDefault();
    }
}
