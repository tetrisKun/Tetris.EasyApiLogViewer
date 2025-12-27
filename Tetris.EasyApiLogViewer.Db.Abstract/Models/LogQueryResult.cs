using System;
using System.Collections.Generic;

namespace Tetris.EasyApiLogViewer.Db.Abstract.Models
{
    /// <summary>
    /// 日志查询结果
    /// </summary>
    public class LogQueryResult
    {
        /// <summary>
        /// 日志列表
        /// </summary>
        public List<TealvAccessLogs> Logs { get; set; } = new List<TealvAccessLogs>();

        /// <summary>
        /// 总记录数
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// 当前页码
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// 每页条数
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// 总页数
        /// </summary>
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    }
}