using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Tetris.EasyApiLogViewer.AspNetCore.Repository;
using Tetris.EasyApiLogViewer.AspNetCore.Services;
using Tetris.EasyApiLogViewer.Db.Abstract.Configuration;
using Tetris.EasyApiLogViewer.Db.Abstract.Repository;

namespace Tetris.EasyApiLogViewer.AspNetCore.Extensions;

/// <summary>
/// API 访问日志模块服务注册扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加 API 访问日志模块
    /// 会自动扫描已加载的程序集，根据配置中的 DatabaseProvider 动态注册对应的数据库实现
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddApiAccessLog(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 绑定配置
        services.Configure<TealvOptions>(
            configuration.GetSection(TealvOptions.SectionName));

        var options = new TealvOptions();
        configuration.GetSection(TealvOptions.SectionName).Bind(options);

        return AddApiAccessLogCore(services, options);
    }

    /// <summary>
    /// 添加 API 访问日志模块
    /// 会自动扫描已加载的程序集，根据配置中的 DatabaseProvider 动态注册对应的数据库实现
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置Action</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddApiAccessLog(
        this IServiceCollection services,
        Action<TealvOptions> configureOptions)
    {
        var options = new TealvOptions();
        configureOptions(options);

        services.Configure(configureOptions);

        return AddApiAccessLogCore(services, options);
    }

    private static IServiceCollection AddApiAccessLogCore(
        IServiceCollection services,
        TealvOptions options)
    {
        // 扫描已加载的程序集，查找并注册数据库仓储实现
        RegisterRepositories(services, options);

        // 注册认证服务
        services.AddScoped<IAdminAuthService, AdminAuthService>();

        // 注册数据库初始化后台服务
        services.AddHostedService<DatabaseInitializerService>();

        // 注册HttpClient用于请求重放
        services.AddHttpClient("ReplayClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            // 允许自签名证书（开发环境）
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        });

        return services;
    }

    private static void RegisterRepositories(
        IServiceCollection services,
        TealvOptions options)
    {
        // 如果是 Sqlite，直接注册内置的 Sqlite 实现
        if (options.DatabaseProvider.ToLower() == "Sqlite")
        {
            services.AddScoped<ITealvAccessLogRepository, SqliteTealvAccessLogRepository>();
            services.AddScoped<ITevlaAdminAccountRepository, SqliteTevlaAdminAccountRepository>();
            return;
        }

        // 获取当前应用程序域中已加载的所有程序集
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        
        // 获取应用程序目录中的所有程序集文件
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var assemblyFiles = Directory.GetFiles(appDirectory, "*.dll");
        
        // 加载需要搜索的程序集
        var searchAssemblies = new List<Assembly>(loadedAssemblies);
        foreach (var assemblyFile in assemblyFiles)
        {
            try
            {
                var assemblyName = AssemblyLoadContext.GetAssemblyName(assemblyFile);
                if (!loadedAssemblies.Any(a => a.FullName == assemblyName.FullName))
                {
                    var assembly = Assembly.Load(assemblyName);
                    // 检查程序集是否包含所需的接口实现
                    var hasRequiredImplementations = assembly.GetTypes()
                        .Any(t => t is { IsClass: true, IsAbstract: false } &&
                                 (typeof(ITealvAccessLogRepository).IsAssignableFrom(t) ||
                                  typeof(ITevlaAdminAccountRepository).IsAssignableFrom(t)));
                    
                    if (hasRequiredImplementations)
                    {
                        searchAssemblies.Add(assembly);
                    }
                }
            }
            catch
            {
                // 如果无法加载程序集，则跳过
            }
        }
        
        var assemblies = searchAssemblies.ToArray();

        // 根据配置的 DatabaseProvider 确定预期的命名空间和类型名

        // 查找对应的仓储实现类型（使用完整命名空间限定）
        var accessLogRepositoryType = assemblies
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.FullName != null&&!t.FullName.Contains("Sqlite") &&
                             typeof(ITealvAccessLogRepository).IsAssignableFrom(t) &&
                             !t.IsInterface && !t.IsAbstract);

        var adminAccountRepositoryType = assemblies
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.FullName != null&&!t.FullName.Contains("Sqlite") &&
                             typeof(ITevlaAdminAccountRepository).IsAssignableFrom(t) &&
                             !t.IsInterface && !t.IsAbstract);

        // 如果找不到指定的数据库实现，回退到 Sqlite 实现
        if (accessLogRepositoryType == null || adminAccountRepositoryType == null)
        {
            // 回退到 Sqlite 实现
            services.AddScoped<ITealvAccessLogRepository, SqliteTealvAccessLogRepository>();
            services.AddScoped<ITevlaAdminAccountRepository, SqliteTevlaAdminAccountRepository>();
            return;
        }
        Console.WriteLine($"{accessLogRepositoryType.Assembly.GetName()} 已加载");
        // 直接注册仓储实现（Scoped）
        services.AddScoped(typeof(ITealvAccessLogRepository), accessLogRepositoryType);
        services.AddScoped(typeof(ITevlaAdminAccountRepository), adminAccountRepositoryType);
    }
}