using System;

namespace Tetris.EasyApiLogViewer.Db.Abstract.Models
{
    /// <summary>
    /// 日志查询参数
    /// </summary>
    public class LogQueryParameters
    {
        /// <summary>
        /// 页码（从1开始）
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// 每页条数
        /// </summary>
        public int PageSize { get; set; } = 20;

        /// <summary>
        /// HTTP方法过滤
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// 路径过滤（部分匹配）
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 状态码过滤
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime? EndDate { get; set; }
    }
}