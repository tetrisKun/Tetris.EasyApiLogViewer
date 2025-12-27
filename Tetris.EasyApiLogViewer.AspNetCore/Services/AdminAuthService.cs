using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Tetris.EasyApiLogViewer.AspNetCore.Models;
using Tetris.EasyApiLogViewer.Db.Abstract.Configuration;
using Tetris.EasyApiLogViewer.Db.Abstract.Models;
using Tetris.EasyApiLogViewer.Db.Abstract.Repository;
using TokenValidationResult = Tetris.EasyApiLogViewer.AspNetCore.Models.TokenValidationResult;

namespace Tetris.EasyApiLogViewer.AspNetCore.Services;

/// <summary>
/// 管理员认证服务实现
/// </summary>
public class AdminAuthService : IAdminAuthService
{
    private readonly ITevlaAdminAccountRepository _accountRepository;
    private readonly TealvOptions _options;
    private readonly ILogger<AdminAuthService> _logger;

    /// <summary>
    /// 初始化管理员认证服务
    /// </summary>
    /// <param name="accountRepository">管理员账户仓储</param>
    /// <param name="options">配置选项</param>
    /// <param name="logger">日志记录器</param>
    public AdminAuthService(
        ITevlaAdminAccountRepository accountRepository,
        IOptions<TealvOptions> options,
        ILogger<AdminAuthService> logger)
    {
        _accountRepository = accountRepository;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 管理员登录
    /// </summary>
    /// <param name="request">登录请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var account = await _accountRepository.GetByUsernameAsync(request.Username, cancellationToken);

            if (account == null)
            {
                _logger.LogWarning("Login failed: user not found - {Username}", request.Username);
                return new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "用户名或密码错误"
                };
            }

            if (!account.IsActive)
            {
                _logger.LogWarning("Login failed: account disabled - {Username}", request.Username);
                return new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "账户已被禁用"
                };
            }

            if (!VerifyPassword(request.Password, account.PasswordHash, account.Salt))
            {
                _logger.LogWarning("Login failed: invalid password - {Username}", request.Username);
                return new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "用户名或密码错误"
                };
            }

            // 更新最后登录时间
            await _accountRepository.UpdateLastLoginAsync(account.Id, DateTime.UtcNow, cancellationToken);

            // 生成JWT令牌
            var token = GenerateJwtToken(account);
            var expiresAt = DateTime.UtcNow.AddMinutes(_options.TealvAdminAuth.JwtExpirationMinutes);

            _logger.LogInformation("Login successful - {Username}", request.Username);

            return new LoginResponse
            {
                Success = true,
                Token = token,
                ExpiresAt = expiresAt,
                Admin = new AdminInfo
                {
                    Id = account.Id,
                    Username = account.Username,
                    DisplayName = account.DisplayName,
                    Role = account.Role
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error - {Username}", request.Username);
            return new LoginResponse
            {
                Success = false,
                ErrorMessage = "登录时发生错误"
            };
        }
    }

    /// <summary>
    /// 验证 JWT 令牌
    /// </summary>
    /// <param name="token">JWT 令牌</param>
    public TokenValidationResult ValidateToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return new TokenValidationResult { IsValid = false };
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_options.TealvAdminAuth.JwtSecretKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _options.TealvAdminAuth.JwtIssuer,
                ValidateAudience = true,
                ValidAudience = _options.TealvAdminAuth.JwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return new TokenValidationResult { IsValid = false };
            }

            var adminIdClaim = principal.FindFirst("admin_id")?.Value;
            var usernameClaim = principal.FindFirst(ClaimTypes.Name)?.Value;
            var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value;

            return new TokenValidationResult
            {
                IsValid = true,
                AdminId = long.TryParse(adminIdClaim, out var id) ? id : null,
                Username = usernameClaim,
                Role = roleClaim
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Token validation failed");
            return new TokenValidationResult { IsValid = false };
        }
    }

    /// <summary>
    /// 修改管理员密码
    /// </summary>
    /// <param name="adminId">管理员ID</param>
    /// <param name="request">修改密码请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<bool> ChangePasswordAsync(long adminId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(adminId, cancellationToken);
        if (account == null)
        {
            return false;
        }

        if (!VerifyPassword(request.CurrentPassword, account.PasswordHash, account.Salt))
        {
            return false;
        }

        var (hash, salt) = CreatePasswordHash(request.NewPassword);
        account.PasswordHash = hash;
        account.Salt = salt;

        return await _accountRepository.UpdateAsync(account, cancellationToken);
    }

    /// <summary>
    /// 获取管理员信息
    /// </summary>
    /// <param name="adminId">管理员ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<AdminInfo?> GetAdminInfoAsync(long adminId, CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(adminId, cancellationToken);
        if (account == null)
        {
            return null;
        }

        return new AdminInfo
        {
            Id = account.Id,
            Username = account.Username,
            DisplayName = account.DisplayName,
            Role = account.Role
        };
    }

    /// <summary>
    /// 创建密码哈希
    /// </summary>
    /// <param name="password">明文密码</param>
    public (string hash, string salt) CreatePasswordHash(string password)
    {
        var salt = GenerateSalt();
        var hash = HashPassword(password, salt);
        return (hash, salt);
    }

    /// <summary>
    /// 验证密码
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <param name="hash">密码哈希</param>
    /// <param name="salt">盐值</param>
    public bool VerifyPassword(string password, string hash, string salt)
    {
        var computedHash = HashPassword(password, salt);
        return computedHash == hash;
    }

    private string GenerateJwtToken(TealvAdminAccount account)
    {
        var key = Encoding.UTF8.GetBytes(_options.TealvAdminAuth.JwtSecretKey);
        var securityKey = new SymmetricSecurityKey(key);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("admin_id", account.Id.ToString()),
            new Claim(ClaimTypes.Name, account.Username),
            new Claim(ClaimTypes.Role, account.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _options.TealvAdminAuth.JwtIssuer,
            audience: _options.TealvAdminAuth.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.TealvAdminAuth.JwtExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateSalt()
    {
        var saltBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(saltBytes);
        return Convert.ToBase64String(saltBytes);
    }

    private string HashPassword(string password, string salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            Encoding.UTF8.GetBytes(salt),
            _options.TealvAdminAuth.PasswordHashIterations,
            HashAlgorithmName.SHA256);

        var hashBytes = pbkdf2.GetBytes(32);
        return Convert.ToBase64String(hashBytes);
    }
}
