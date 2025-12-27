using System.Collections.Generic;

namespace Tetris.EasyApiLogViewer.Db.Abstract.Models
{
    /// <summary>
    /// 日志统计信息
    /// </summary>
    public class LogStatistics
    {
        /// <summary>
        /// 总请求数
        /// </summary>
        public int TotalRequests { get; set; }

        /// <summary>
        /// 平均处理时间（毫秒）
        /// </summary>
        public double AvgDuration { get; set; }

        /// <summary>
        /// 状态码统计
        /// </summary>
        public List<StatusCodeStat> StatusCodeStats { get; set; } = new List<StatusCodeStat>();
    }
}