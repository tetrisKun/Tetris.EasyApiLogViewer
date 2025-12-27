using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tetris.EasyApiLogViewer.Db.Abstract.Configuration;
using Tetris.EasyApiLogViewer.Db.Abstract.Models;
using Tetris.EasyApiLogViewer.Db.Abstract.Repository;

namespace Tetris.EasyApiLogViewer.AspNetCore.Middleware;

/// <summary>
/// HTTP请求/响应日志记录中间件
/// </summary>
public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TealvOptions _options;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    /// <summary>
    /// 初始化请求/响应日志中间件
    /// </summary>
    /// <param name="next">下一个请求委托</param>
    /// <param name="options">配置选项</param>
    /// <param name="logger">日志记录器</param>
    public RequestResponseLoggingMiddleware(
        RequestDelegate next,
        IOptions<TealvOptions> options,
        ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 处理HTTP请求并记录日志
    /// </summary>
    /// <param name="context">HTTP上下文</param>
    public async Task InvokeAsync(HttpContext context)
    {
        // 检查是否应该跳过日志记录
        if (!_options.EnableRequestLogging || !ShouldIncludeLogging(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString("N")[..8];

        // 读取请求信息
        var requestBody = await ReadRequestBodyAsync(context.Request);
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;
        var queryString = context.Request.QueryString.Value ?? string.Empty;
        var requestHeaders = GetRequestHeaders(context.Request);
        var clientIp = GetClientIpAddress(context);
        var userAgent = context.Request.Headers.UserAgent.ToString();

        // 替换响应流以捕获响应体
        var originalBodyStream = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // 读取响应体
            var responseBody = await ReadResponseBodyAsync(responseBodyStream);

            // 恢复原始响应流
            responseBodyStream.Seek(0, SeekOrigin.Begin);
            await responseBodyStream.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;

            // 获取用户ID（如果已认证）
            string? userId = null;
            if (context.User.Identity?.IsAuthenticated == true)
            {
                userId = context.User.FindFirst("sub")?.Value ??
                         context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            }

            // 创建日志条目
            var logEntry = new TealvAccessLogs
            {
                Timestamp = DateTime.UtcNow,
                RequestId = requestId,
                Method = method,
                Path = path,
                QueryString = queryString,
                RequestHeaders = requestHeaders,
                RequestBody = TruncateIfNeeded(requestBody, _options.MaxRequestBodySize),
                StatusCode = context.Response.StatusCode,
                ResponseBody = TruncateIfNeeded(responseBody, _options.MaxResponseBodySize),
                Duration = stopwatch.ElapsedMilliseconds,
                Level = context.Response.StatusCode >= 400 ? "Warning" : "Info",
                Logger = "ApiAccessLog",
                ClientIpAddress = clientIp,
                UserAgent = TruncateIfNeeded(userAgent, 500),
                UserId = userId
            };

            // 异步保存日志
            _ = SaveLogEntryAsync(context.RequestServices, logEntry);

            // 控制台日志
            _logger.LogInformation(
                "[{RequestId}] {Method} {Path}{QueryString} -> {StatusCode} ({Duration}ms)",
                requestId, method, path, queryString, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task SaveLogEntryAsync(IServiceProvider serviceProvider, TealvAccessLogs entry)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITealvAccessLogRepository>();
            await repository.InsertAsync(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save access log entry");
        }
    }

    private bool ShouldIncludeLogging(PathString path)
    {
        var pathValue = path.Value?.ToLower() ?? string.Empty;

        return _options.IncludePaths.Any(p =>
                   pathValue.StartsWith(p, StringComparison.OrdinalIgnoreCase))
               && !_options.ExcludedPaths.Any(p =>
                   pathValue.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private bool ShouldSkipLogging(PathString path)
    {
        var pathValue = path.Value?.ToLower() ?? string.Empty;

        foreach (var excludedPath in _options.ExcludedPaths)
        {
            if (pathValue.StartsWith(excludedPath.ToLower()))
            {
                return true;
            }
        }

        return false;
    }

    private string GetRequestHeaders(HttpRequest request)
    {
        if (!_options.LogRequestHeaders)
        {
            return "{}";
        }

        var headers = new Dictionary<string, string>();

        foreach (var header in request.Headers)
        {
            var value = header.Value.ToString();

            // 掩码敏感头部（如果在列表中）
            if (_options.SensitiveHeaders.Any(h =>
                    h.Equals(header.Key, StringComparison.OrdinalIgnoreCase)))
            {
                value = "***MASKED***";
            }

            headers[header.Key] = value;
        }

        return JsonSerializer.Serialize(headers);
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        if (request.ContentLength == null || request.ContentLength == 0)
        {
            return string.Empty;
        }

        request.EnableBuffering();

        using var reader = new StreamReader(
            request.Body,
            encoding: Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: true);

        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        return body;
    }

    private static async Task<string> ReadResponseBodyAsync(MemoryStream responseBodyStream)
    {
        responseBodyStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(responseBodyStream, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        return body;
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        // 尝试从X-Forwarded-For头获取
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        // 尝试从X-Real-IP头获取
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // 使用连接远程IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static string TruncateIfNeeded(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...[TRUNCATED]";
    }
}
