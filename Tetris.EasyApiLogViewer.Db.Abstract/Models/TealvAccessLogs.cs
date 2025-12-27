using System;

namespace Tetris.EasyApiLogViewer.Db.Abstract.Models
{
    /// <summary>
    /// API访问日志条目
    /// </summary>
    public class TealvAccessLogs
    {
        /// <summary>
        /// 日志ID
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 请求ID（用于追踪）
        /// </summary>
        public string RequestId { get; set; }

        /// <summary>
        /// HTTP方法
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// 请求路径
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 查询字符串
        /// </summary>
        public string QueryString { get; set; }

        /// <summary>
        /// 请求头（JSON格式）
        /// </summary>
        public string RequestHeaders { get; set; }

        /// <summary>
        /// 请求体
        /// </summary>
        public string RequestBody { get; set; }

        /// <summary>
        /// HTTP状态码
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// 响应体
        /// </summary>
        public string ResponseBody { get; set; }

        /// <summary>
        /// 处理时间（毫秒）
        /// </summary>
        public long? Duration { get; set; }

        /// <summary>
        /// 日志级别
        /// </summary>
        public string Level { get; set; }

        /// <summary>
        /// 日志记录器名称
        /// </summary>
        public string Logger { get; set; }

        /// <summary>
        /// 客户端IP地址
        /// </summary>
        public string ClientIpAddress { get; set; }

        /// <summary>
        /// 用户代理
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// 用户ID（如果已认证）
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// 异常信息
        /// </summary>
        public string Exception { get; set; }
    }
}
