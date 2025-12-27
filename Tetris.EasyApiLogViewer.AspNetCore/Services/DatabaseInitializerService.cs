using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tetris.EasyApiLogViewer.Db.Abstract.Configuration;
using Tetris.EasyApiLogViewer.Db.Abstract.Models;
using Tetris.EasyApiLogViewer.Db.Abstract.Repository;

namespace Tetris.EasyApiLogViewer.AspNetCore.Services;

/// <summary>
/// 数据库初始化后台服务
/// </summary>
public class DatabaseInitializerService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TealvOptions _options;
    private readonly ILogger<DatabaseInitializerService> _logger;

    /// <summary>
    /// 初始化数据库初始化服务
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="options">配置选项</param>
    /// <param name="logger">日志记录器</param>
    public DatabaseInitializerService(
        IServiceProvider serviceProvider,
        IOptions<TealvOptions> options,
        ILogger<DatabaseInitializerService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 启动服务并初始化数据库
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing API Access Log database...");

        try
        {
            using var scope = _serviceProvider.CreateScope();

            // 初始化日志表
            var logRepository = scope.ServiceProvider.GetRequiredService<ITealvAccessLogRepository>();
            await logRepository.InitializeAsync(cancellationToken);

            // 初始化管理员账户表
            var accountRepository = scope.ServiceProvider.GetRequiredService<ITevlaAdminAccountRepository>();
            await accountRepository.InitializeAsync(cancellationToken);

            // 创建默认管理员账户
            if (_options.TealvAdminAuth.EnableDefaultAdmin)
            {
                await CreateDefaultAdminAsync(scope.ServiceProvider, cancellationToken);
            }

            _logger.LogInformation("API Access Log database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize API Access Log database");
            throw;
        }
    }

    /// <summary>
    /// 停止服务
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task CreateDefaultAdminAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var accountRepository = serviceProvider.GetRequiredService<ITevlaAdminAccountRepository>();
        var authService = serviceProvider.GetRequiredService<IAdminAuthService>();

        var defaultUsername = _options.TealvAdminAuth.DefaultAdminUsername;

        // 检查是否已存在
        if (await accountRepository.ExistsAsync(defaultUsername, cancellationToken))
        {
            _logger.LogDebug("Default admin account already exists: {Username}", defaultUsername);
            return;
        }

        // 创建默认管理员
        var (hash, salt) = authService.CreatePasswordHash(_options.TealvAdminAuth.DefaultAdminPassword);

        var admin = new TealvAdminAccount
        {
            Username = defaultUsername,
            PasswordHash = hash,
            Salt = salt,
            DisplayName = "Administrator",
            Role = "admin",
            IsActive = true
        };

        await accountRepository.CreateAsync(admin, cancellationToken);

        _logger.LogInformation("Default admin account created: {Username}", defaultUsername);
        _logger.LogWarning("Please change the default admin password after first login!");
    }
}
