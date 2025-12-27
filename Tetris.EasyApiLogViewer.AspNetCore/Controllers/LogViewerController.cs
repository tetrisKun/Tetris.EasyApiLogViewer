using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Tetris.EasyApiLogViewer.AspNetCore.Services;
using Tetris.EasyApiLogViewer.Db.Abstract.Models;
using Tetris.EasyApiLogViewer.Db.Abstract.Repository;

namespace Tetris.EasyApiLogViewer.AspNetCore.Controllers;

/// <summary>
/// 日志查看器控制器
/// </summary>
[ApiController]
[Route("api/logs")]
public class LogViewerController : ControllerBase
{
    private readonly ITealvAccessLogRepository _logRepository;
    private readonly IAdminAuthService _authService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LogViewerController> _logger;

    /// <summary>
    /// 初始化日志查看器控制器
    /// </summary>
    /// <param name="logRepository">访问日志仓储</param>
    /// <param name="authService">管理员认证服务</param>
    /// <param name="httpClientFactory">HTTP 客户端工厂</param>
    /// <param name="logger">日志记录器</param>
    public LogViewerController(
        ITealvAccessLogRepository logRepository,
        IAdminAuthService authService,
        IHttpClientFactory httpClientFactory,
        ILogger<LogViewerController> logger)
    {
        _logRepository = logRepository;
        _authService = authService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// 日志查看器页面
    /// </summary>
    [HttpGet("/log-viewer")]
    [Produces("text/html")]
    public IActionResult GetLogViewerPage()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Tetris.EasyApiLogViewer.AspNetCore.Resources.log-viewer.html";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            _logger.LogError("Embedded resource not found: {ResourceName}", resourceName);
            return NotFound("Log viewer page not found");
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var html = reader.ReadToEnd();

        return Content(html, "text/html", Encoding.UTF8);
    }

    /// <summary>
    /// 获取日志列表
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? method = null,
        [FromQuery] string? path = null,
        [FromQuery] int? statusCode = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        if (!ValidateToken())
        {
            return UnauthorizedResponse();
        }

        var parameters = new LogQueryParameters
        {
            Page = page,
            PageSize = pageSize,
            Method = method,
            Path = path,
            StatusCode = statusCode,
            StartDate = startDate,
            EndDate = endDate
        };

        var result = await _logRepository.QueryAsync(parameters);

        return Ok(new
        {
            success = true,
            data = new
            {
                logs = result.Logs.Select(log => new
                {
                    id = log.Id,
                    timestamp = log.Timestamp,
                    requestId = log.RequestId,
                    method = log.Method,
                    path = log.Path,
                    queryString = log.QueryString,
                    statusCode = log.StatusCode,
                    duration = log.Duration,
                    clientIpAddress = log.ClientIpAddress,
                    userId = log.UserId
                }),
                totalCount = result.TotalCount,
                page = result.Page,
                pageSize = result.PageSize,
                totalPages = result.TotalPages
            }
        });
    }

    /// <summary>
    /// 获取日志详情
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetLogById(long id)
    {
        if (!ValidateToken())
        {
            return UnauthorizedResponse();
        }

        var log = await _logRepository.GetByIdAsync(id);
        if (log == null)
        {
            return NotFound(new { success = false, message = "日志不存在" });
        }

        return Ok(new
        {
            success = true,
            data = log
        });
    }

    /// <summary>
    /// 获取日志统计
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        if (!ValidateToken())
        {
            return UnauthorizedResponse();
        }

        var stats = await _logRepository.GetStatisticsAsync(startDate, endDate);

        return Ok(new
        {
            success = true,
            data = stats
        });
    }

    /// <summary>
    /// 重放请求（后端代理）
    /// </summary>
    [HttpPost("{id:long}/replay")]
    public async Task<IActionResult> ReplayRequest(long id, [FromBody] ReplayRequestOptions? options = null)
    {
        if (!ValidateToken())
        {
            return UnauthorizedResponse();
        }

        var log = await _logRepository.GetByIdAsync(id);
        if (log == null)
        {
            return NotFound(new { success = false, message = "日志不存在" });
        }

        try
        {
            // 构建请求URL
            var baseUrl = options?.BaseUrl ?? $"{Request.Scheme}://{Request.Host}";
            var url = $"{baseUrl.TrimEnd('/')}{log.Path}{log.QueryString}";

            _logger.LogInformation("Replaying request: {Method} {Url}", log.Method, url);

            // 创建HTTP请求
            var httpClient = _httpClientFactory.CreateClient("ReplayClient");
            var requestMessage = new HttpRequestMessage(new HttpMethod(log.Method ?? "GET"), url);

            // 添加请求头（排除一些不应该重放的头）
            if (!string.IsNullOrEmpty(log.RequestHeaders))
            {
                try
                {
                    var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(log.RequestHeaders);
                    if (headers != null)
                    {
                        foreach (var header in headers)
                        {
                            // 跳过一些特殊头
                            if (ShouldSkipHeader(header.Key))
                            {
                                continue;
                            }

                            // 覆盖Authorization头（如果options中提供了）
                            if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) &&
                                !string.IsNullOrEmpty(options?.AuthorizationHeader))
                            {
                                requestMessage.Headers.TryAddWithoutValidation("Authorization", options.AuthorizationHeader);
                                continue;
                            }

                            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }
                }
                catch (JsonException)
                {
                    // 忽略头解析错误
                }
            }

            // 添加自定义Authorization头
            if (!string.IsNullOrEmpty(options?.AuthorizationHeader) &&
                !requestMessage.Headers.Contains("Authorization"))
            {
                requestMessage.Headers.TryAddWithoutValidation("Authorization", options.AuthorizationHeader);
            }

            // 设置重放标志的 User-Agent
            var replayUserAgent = $"ApiAccessLog-Replay/1.0 (LogId:{id}; ReplayedAt:{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ})";
            requestMessage.Headers.TryAddWithoutValidation("User-Agent", replayUserAgent);
            requestMessage.Headers.TryAddWithoutValidation("X-Replay-Request", "true");
            requestMessage.Headers.TryAddWithoutValidation("X-Replay-LogId", id.ToString());

            // 添加请求体
            if (!string.IsNullOrEmpty(log.RequestBody) &&
                (log.Method == "POST" || log.Method == "PUT" || log.Method == "PATCH"))
            {
                var contentType = "application/json";

                // 尝试从原始请求头获取Content-Type
                if (!string.IsNullOrEmpty(log.RequestHeaders))
                {
                    try
                    {
                        var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(log.RequestHeaders);
                        if (headers?.TryGetValue("Content-Type", out var ct) == true)
                        {
                            contentType = ct.Split(';')[0].Trim();
                        }
                    }
                    catch
                    {
                        // 忽略
                    }
                }

                requestMessage.Content = new StringContent(log.RequestBody, Encoding.UTF8, contentType);
            }

            // 发送请求
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = await httpClient.SendAsync(requestMessage);
            stopwatch.Stop();

            // 读取响应
            var responseBody = await response.Content.ReadAsStringAsync();
            var responseHeaders = response.Headers
                .Concat(response.Content.Headers)
                .ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

            _logger.LogInformation(
                "Replay completed: {StatusCode} in {Duration}ms",
                (int)response.StatusCode, stopwatch.ElapsedMilliseconds);

            return Ok(new
            {
                success = true,
                data = new
                {
                    originalLogId = id,
                    replayedAt = DateTime.UtcNow,
                    request = new
                    {
                        method = log.Method,
                        url,
                        body = log.RequestBody
                    },
                    response = new
                    {
                        statusCode = (int)response.StatusCode,
                        headers = responseHeaders,
                        body = responseBody,
                        duration = stopwatch.ElapsedMilliseconds
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to replay request {Id}", id);

            return StatusCode(500, new
            {
                success = false,
                message = $"重放请求失败: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// 清理过期日志
    /// </summary>
    [HttpPost("purge")]
    public async Task<IActionResult> PurgeLogs([FromQuery] int retentionDays = 90)
    {
        if (!ValidateToken())
        {
            return UnauthorizedResponse();
        }

        // 检查是否为admin角色
        var token = GetTokenFromRequest();
        var validation = _authService.ValidateToken(token);
        if (validation.Role != "admin")
        {
            return StatusCode(403, new { success = false, message = "需要管理员权限" });
        }

        var deletedCount = await _logRepository.PurgeOldLogsAsync(retentionDays);

        return Ok(new
        {
            success = true,
            data = new
            {
                deletedCount,
                retentionDays
            }
        });
    }

    private bool ValidateToken()
    {
        var token = GetTokenFromRequest();
        var result = _authService.ValidateToken(token);
        return result.IsValid;
    }

    private string? GetTokenFromRequest()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader["Bearer ".Length..].Trim();
        }

        return Request.Query["token"].FirstOrDefault();
    }

    private IActionResult UnauthorizedResponse()
    {
        return Unauthorized(new
        {
            success = false,
            error = new
            {
                code = "UNAUTHORIZED",
                message = "请先登录"
            }
        });
    }

    private static bool ShouldSkipHeader(string headerName)
    {
        var skipHeaders = new[]
        {
            "Host", "Content-Length", "Transfer-Encoding",
            "Connection", "Keep-Alive", "Upgrade",
            "Proxy-Connection", "Proxy-Authenticate", "Proxy-Authorization",
            "User-Agent" // 重放时使用自定义 User-Agent
        };

        return skipHeaders.Any(h => h.Equals(headerName, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// 重放请求选项
/// </summary>
public class ReplayRequestOptions
{
    /// <summary>
    /// 基础URL（默认使用当前服务器）
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// 覆盖Authorization头
    /// </summary>
    public string? AuthorizationHeader { get; set; }
}
