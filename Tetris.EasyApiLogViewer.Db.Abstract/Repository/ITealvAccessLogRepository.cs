using System;
using System.Threading;
using System.Threading.Tasks;
using Tetris.EasyApiLogViewer.Db.Abstract.Models;

namespace Tetris.EasyApiLogViewer.Db.Abstract.Repository
{
    /// <summary>
    /// 访问日志仓储接口
    /// </summary>
    public interface ITealvAccessLogRepository
    {
        /// <summary>
        /// 插入日志条目
        /// </summary>
        Task InsertAsync(TealvAccessLogs entry, CancellationToken cancellationToken = default);

        /// <summary>
        /// 查询日志列表
        /// </summary>
        Task<LogQueryResult> QueryAsync(LogQueryParameters parameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// 根据ID获取日志详情
        /// </summary>
        Task<TealvAccessLogs> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取统计信息
        /// </summary>
        Task<LogStatistics> GetStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 清理过期日志
        /// </summary>
        Task<int> PurgeOldLogsAsync(int retentionDays, CancellationToken cancellationToken = default);

        /// <summary>
        /// 初始化数据库表
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);
    }
}
