using System.Collections.Generic;

namespace Tetris.EasyApiLogViewer.Db.Abstract.Configuration
{
    /// <summary>
    /// API访问日志模块配置选项
    /// </summary>
    public class TealvOptions
    {
        /// <summary>
        /// 配置节名称
        /// </summary>
        public const string SectionName = "ApiAccessLog";

        /// <summary>
        /// 数据库提供程序类型
        /// </summary>
        public string DatabaseProvider { get; set; } = "sqlite";

        /// <summary>
        /// 数据库连接字符串
        /// </summary>
        public string ConnectionString { get; set; } = "Data Source=logs/access.db";

        /// <summary>
        /// 日志保留天数（默认90天）
        /// </summary>
        public int RetentionDays { get; set; } = 90;

        /// <summary>
        /// 是否启用请求/响应日志记录
        /// </summary>
        public bool EnableRequestLogging { get; set; } = true;

        /// <summary>
        /// 是否启用日志查看器
        /// </summary>
        public bool EnableLogViewer { get; set; } = true;

        /// <summary>
        /// 日志查看器路由前缀
        /// </summary>
        public string LogViewerRoutePrefix { get; set; } = "api/logs";

        /// <summary>
        /// 路径排除（不记录日志）
        /// </summary>
        public List<string> ExcludedPaths { get; set; } = new List<string>
        {
            "/health",
            "/api/logs",
            "/swagger",
            "/log-viewer"
        };

        /// <summary>
        /// 路径前缀匹配（记录日志,但会被ExcludedPaths覆盖）
        /// </summary>
        public List<string> IncludePaths { get; set; } = new List<string>()
        {
            "/api/"
        };
    
        /// <summary>
        /// 最大请求体大小（字节，超过则截断）
        /// </summary>
        public int MaxRequestBodySize { get; set; } = 102400; // 100KB

        /// <summary>
        /// 最大响应体大小（字节，超过则截断）
        /// </summary>
        public int MaxResponseBodySize { get; set; } = 102400; // 100KB

        /// <summary>
        /// 是否记录请求头
        /// </summary>
        public bool LogRequestHeaders { get; set; } = true;

        /// <summary>
        /// 敏感头部名称（将被掩码，空列表则不掩码）
        /// </summary>
        public List<string> SensitiveHeaders { get; set; } = new List<string>();

        /// <summary>
        /// 管理员认证配置
        /// </summary>
        public TealvAdminAuthOptions TealvAdminAuth { get; set; } = new TealvAdminAuthOptions();

        /// <summary>
        /// API访问日志表名
        /// </summary>
        public string AccessLogTableName { get; set; } = "tealv_access_logs";
    }
}