using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tetris.EasyApiLogViewer.Db.Abstract.Configuration;
using Tetris.EasyApiLogViewer.Db.Abstract.Models;
using Tetris.EasyApiLogViewer.Db.Abstract.Repository;

namespace Tetris.EasyApiLogViewer.Db.SqlServer.Repository;

/// <summary>
/// SQL Server访问日志仓储实现
/// </summary>
public class SqlServerTealvAccessLogRepository : ITealvAccessLogRepository
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly ILogger<SqlServerTealvAccessLogRepository> _logger;

    /// <summary>
    /// 初始化 SQL Server 访问日志仓储
    /// </summary>
    /// <param name="options">配置选项</param>
    /// <param name="logger">日志记录器</param>
    public SqlServerTealvAccessLogRepository(
        IOptions<TealvOptions> options,
        ILogger<SqlServerTealvAccessLogRepository> logger)
    {
        _connectionString = options.Value.ConnectionString;
        _tableName = options.Value.AccessLogTableName;
        _logger = logger;
    }

    private async Task<SqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>
    /// 初始化数据库表
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var fullTableName = $"[dbo].[{_tableName}]";
        var pkName = $"[PK_{_tableName}]";
        var idxTimestampName = $"[IX_{_tableName}_Timestamp]";
        var idxStatusCodeName = $"[IX_{_tableName}_StatusCode]";

        await using var connection = await GetConnectionAsync(cancellationToken);
        var createTableSql = $@"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='{_tableName}' AND xtype='U')
            BEGIN
                CREATE TABLE {fullTableName}
                (
                    [Id]              BIGINT           IDENTITY(1,1) NOT NULL,
                    [Timestamp]       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
                    [RequestId]       NVARCHAR(50)     NULL,
                    [Method]          NVARCHAR(10)     NULL,
                    [Path]            NVARCHAR(500)    NULL,
                    [QueryString]     NVARCHAR(2000)   NULL,
                    [RequestHeaders]  NVARCHAR(MAX)    NULL,
                    [RequestBody]     NVARCHAR(MAX)    NULL,
                    [StatusCode]      INT              NULL,
                    [ResponseBody]    NVARCHAR(MAX)    NULL,
                    [Duration]        BIGINT           NULL,
                    [Level]           NVARCHAR(20)     NULL,
                    [Logger]          NVARCHAR(255)    NULL,
                    [ClientIpAddress] NVARCHAR(45)     NULL,
                    [UserAgent]       NVARCHAR(500)    NULL,
                    [UserId]          NVARCHAR(100)    NULL,
                    CONSTRAINT {pkName} PRIMARY KEY CLUSTERED ([Id] ASC)
                );

                CREATE NONCLUSTERED INDEX {idxTimestampName}
                ON {fullTableName} ([Timestamp] DESC)
                INCLUDE ([Method], [Path], [StatusCode], [Duration]);

                CREATE NONCLUSTERED INDEX {idxStatusCodeName}
                ON {fullTableName} ([StatusCode])
                WHERE [StatusCode] >= 400;
            END";

        await using var command = connection.CreateCommand();
        command.CommandText = createTableSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("SQL Server {TableName} table initialized", _tableName);
    }

    /// <summary>
    /// 插入日志条目
    /// </summary>
    public async Task InsertAsync(TealvAccessLogs entry, CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var sql = $@"
            INSERT INTO [dbo].[{_tableName}]
            ([Timestamp], [RequestId], [Method], [Path], [QueryString], [RequestHeaders], [RequestBody],
             [StatusCode], [ResponseBody], [Duration], [Level], [Logger], [ClientIpAddress], [UserAgent], [UserId])
            VALUES
            (@Timestamp, @RequestId, @Method, @Path, @QueryString, @RequestHeaders, @RequestBody,
             @StatusCode, @ResponseBody, @Duration, @Level, @Logger, @ClientIpAddress, @UserAgent, @UserId)";

        command.CommandText = sql;
        command.Parameters.AddWithValue("@Timestamp", entry.Timestamp);
        command.Parameters.AddWithValue("@RequestId", entry.RequestId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Method", entry.Method ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Path", entry.Path ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@QueryString", entry.QueryString ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@RequestHeaders", entry.RequestHeaders ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@RequestBody", entry.RequestBody ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@StatusCode", entry.StatusCode ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ResponseBody", entry.ResponseBody ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Duration", entry.Duration ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Level", entry.Level ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Logger", entry.Logger ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ClientIpAddress", entry.ClientIpAddress ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@UserAgent", entry.UserAgent ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@UserId", entry.UserId ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 查询日志列表
    /// </summary>
    public async Task<LogQueryResult> QueryAsync(LogQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string>();
        var sqlParams = new List<SqlParameter>();

        if (!string.IsNullOrEmpty(parameters.Method))
        {
            conditions.Add("[Method] = @Method");
            sqlParams.Add(new SqlParameter("@Method", parameters.Method.ToUpper()));
        }

        if (!string.IsNullOrEmpty(parameters.Path))
        {
            conditions.Add("[Path] LIKE @Path");
            sqlParams.Add(new SqlParameter("@Path", $"%{parameters.Path}%"));
        }

        if (parameters.StatusCode.HasValue)
        {
            conditions.Add("[StatusCode] = @StatusCode");
            sqlParams.Add(new SqlParameter("@StatusCode", parameters.StatusCode.Value));
        }

        if (parameters.StartDate.HasValue)
        {
            conditions.Add("[Timestamp] >= @StartDate");
            sqlParams.Add(new SqlParameter("@StartDate", parameters.StartDate.Value));
        }

        if (parameters.EndDate.HasValue)
        {
            conditions.Add("[Timestamp] <= @EndDate");
            sqlParams.Add(new SqlParameter("@EndDate", parameters.EndDate.Value));
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";
        var fullTableName = $"[dbo].[{_tableName}]";

        await using var connection = await GetConnectionAsync(cancellationToken);

        // 获取总数
        var countSql = $"SELECT COUNT(*) FROM {fullTableName} {whereClause}";
        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = countSql;
        foreach (var param in sqlParams)
        {
            countCommand.Parameters.Add(new SqlParameter(param.ParameterName, param.Value));
        }

        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

        // 获取数据
        var offset = (parameters.Page - 1) * parameters.PageSize;
        var dataSql = $@"
            SELECT [Id], [Timestamp], [RequestId], [Method], [Path], [QueryString], [RequestHeaders], [RequestBody],
                   [StatusCode], [ResponseBody], [Duration], [Level], [Logger], [ClientIpAddress], [UserAgent], [UserId]
            FROM {fullTableName}
            {whereClause}
            ORDER BY [Id] DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        await using var dataCommand = connection.CreateCommand();
        dataCommand.CommandText = dataSql;
        foreach (var param in sqlParams)
        {
            dataCommand.Parameters.Add(new SqlParameter(param.ParameterName, param.Value));
        }
        dataCommand.Parameters.AddWithValue("@Offset", offset);
        dataCommand.Parameters.AddWithValue("@PageSize", parameters.PageSize);

        var logs = new List<TealvAccessLogs>();
        await using var reader = await dataCommand.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            logs.Add(MapToAccessLog(reader));
        }

        return new LogQueryResult
        {
            Logs = logs,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }

    /// <summary>
    /// 根据ID获取日志详情
    /// </summary>
    public async Task<TealvAccessLogs?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var fullTableName = $"[dbo].[{_tableName}]";
        command.CommandText = $@"
            SELECT [Id], [Timestamp], [RequestId], [Method], [Path], [QueryString],
                   [RequestHeaders], [RequestBody], [StatusCode], [ResponseBody], [Duration],
                   [Level], [Logger], [ClientIpAddress], [UserAgent], [UserId]
            FROM {fullTableName}
            WHERE [Id] = @Id";
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapToAccessLog(reader);
        }

        return null;
    }

    /// <summary>
    /// 获取统计信息
    /// </summary>
    public async Task<LogStatistics> GetStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string>();
        var sqlParams = new List<SqlParameter>();

        if (startDate.HasValue)
        {
            conditions.Add("[Timestamp] >= @StartDate");
            sqlParams.Add(new SqlParameter("@StartDate", startDate.Value));
        }

        if (endDate.HasValue)
        {
            conditions.Add("[Timestamp] <= @EndDate");
            sqlParams.Add(new SqlParameter("@EndDate", endDate.Value));
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";
        var fullTableName = $"[dbo].[{_tableName}]";

        await using var connection = await GetConnectionAsync(cancellationToken);

        // 总数和平均时间
        var statsSql = $"SELECT COUNT(*), AVG(CAST([Duration] AS FLOAT)) FROM {fullTableName} {whereClause}";

        await using var statsCommand = connection.CreateCommand();
        statsCommand.CommandText = statsSql;
        foreach (var param in sqlParams)
        {
            statsCommand.Parameters.Add(new SqlParameter(param.ParameterName, param.Value));
        }

        int totalRequests = 0;
        double avgDuration = 0;

        await using var statsReader = await statsCommand.ExecuteReaderAsync(cancellationToken);
        if (await statsReader.ReadAsync(cancellationToken))
        {
            totalRequests = statsReader.GetInt32(0);
            avgDuration = statsReader.IsDBNull(1) ? 0 : statsReader.GetDouble(1);
        }

        await statsReader.CloseAsync();

        // 状态码统计
        var statusSql = $@"
            SELECT [StatusCode], COUNT(*) as [Count]
            FROM {fullTableName}
            {whereClause}
            GROUP BY [StatusCode]
            ORDER BY [Count] DESC";

        await using var statusCommand = connection.CreateCommand();
        statusCommand.CommandText = statusSql;
        foreach (var param in sqlParams)
        {
            statusCommand.Parameters.Add(new SqlParameter(param.ParameterName, param.Value));
        }

        var statusCodeStats = new List<StatusCodeStat>();
        await using var statusReader = await statusCommand.ExecuteReaderAsync(cancellationToken);

        while (await statusReader.ReadAsync(cancellationToken))
        {
            statusCodeStats.Add(new StatusCodeStat
            {
                StatusCode = statusReader.IsDBNull(0) ? null : statusReader.GetInt32(0),
                Count = statusReader.GetInt32(1)
            });
        }

        return new LogStatistics
        {
            TotalRequests = totalRequests,
            AvgDuration = Math.Round(avgDuration, 2),
            StatusCodeStats = statusCodeStats
        };
    }

    /// <summary>
    /// 清理过期日志
    /// </summary>
    public async Task<int> PurgeOldLogsAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        await using var connection = await GetConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var fullTableName = $"[dbo].[{_tableName}]";
        command.CommandText = $"DELETE FROM {fullTableName} WHERE [Timestamp] < @CutoffDate";
        command.Parameters.AddWithValue("@CutoffDate", cutoffDate);

        var deletedCount = await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Purged {Count} old access logs from SQL Server", deletedCount);

        return deletedCount;
    }

    private static TealvAccessLogs MapToAccessLog(IDataReader reader)
    {
        return new TealvAccessLogs
        {
            Id = reader.GetInt64(0),
            Timestamp = reader.GetDateTime(1),
            RequestId = reader.IsDBNull(2) ? null : reader.GetString(2),
            Method = reader.IsDBNull(3) ? null : reader.GetString(3),
            Path = reader.IsDBNull(4) ? null : reader.GetString(4),
            QueryString = reader.IsDBNull(5) ? null : reader.GetString(5),
            RequestHeaders = reader.IsDBNull(6) ? null : reader.GetString(6),
            RequestBody = reader.IsDBNull(7) ? null : reader.GetString(7),
            StatusCode = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            ResponseBody = reader.IsDBNull(9) ? null : reader.GetString(9),
            Duration = reader.IsDBNull(10) ? null : reader.GetInt64(10),
            Level = reader.IsDBNull(11) ? null : reader.GetString(11),
            Logger = reader.IsDBNull(12) ? null : reader.GetString(12),
            ClientIpAddress = reader.IsDBNull(13) ? null : reader.GetString(13),
            UserAgent = reader.IsDBNull(14) ? null : reader.GetString(14),
            UserId = reader.IsDBNull(15) ? null : reader.GetString(15)
        };
    }
}
