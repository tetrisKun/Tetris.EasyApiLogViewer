using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tetris.EasyApiLogViewer.AspNetCore.Middleware;
using Tetris.EasyApiLogViewer.Db.Abstract.Configuration;

namespace Tetris.EasyApiLogViewer.AspNetCore.Extensions;

/// <summary>
/// API访问日志模块应用构建器扩展
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// 使用API访问日志模块
    /// </summary>
    /// <param name="app">应用构建器</param>
    /// <returns>应用构建器</returns>
    public static IApplicationBuilder UseApiAccessLog(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices
            .GetRequiredService<IOptions<TealvOptions>>().Value;

        // 启用请求日志记录中间件
        if (options.EnableRequestLogging)
        {
            app.UseMiddleware<RequestResponseLoggingMiddleware>();
        }

        return app;
    }
}
