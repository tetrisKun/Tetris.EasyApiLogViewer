using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tetris.EasyApiLogViewer.Db.Abstract.Configuration;
using Tetris.EasyApiLogViewer.Db.Abstract.Models;
using Tetris.EasyApiLogViewer.Db.Abstract.Repository;

namespace Tetris.EasyApiLogViewer.Db.MongoDB.Repository;

/// <summary>
/// MongoDB访问日志仓储实现
/// </summary>
public class MongoDbTealvAccessLogRepository : ITealvAccessLogRepository
{
    private readonly IMongoCollection<TealvAccessLogs> _collection;
    private readonly ILogger<MongoDbTealvAccessLogRepository> _logger;

    /// <summary>
    /// 初始化 MongoDB MongoDbTealvAccessLogRepository仓储
    /// </summary>
    /// <param name="options">配置选项</param>
    /// <param name="logger">日志记录器</param>
    public MongoDbTealvAccessLogRepository(
        IOptions<TealvOptions> options,
        ILogger<MongoDbTealvAccessLogRepository> logger)
    {
        var client = new MongoClient(options.Value.ConnectionString);
        var databaseName = MongoUrl.Create(options.Value.ConnectionString).DatabaseName;
        var database = client.GetDatabase(databaseName);
        _collection = database.GetCollection<TealvAccessLogs>(options.Value.AccessLogTableName);
        _logger = logger;
    }

        /// <summary>
    /// 初始化数据库表
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // 创建时间戳索引
        var timestampIndexKeys = Builders<TealvAccessLogs>.IndexKeys.Descending(x => x.Timestamp);
        var timestampIndexModel = new CreateIndexModel<TealvAccessLogs>(timestampIndexKeys);
        await _collection.Indexes.CreateOneAsync(timestampIndexModel, cancellationToken: cancellationToken);

        // 状态码索引
        var statusCodeIndexKeys = Builders<TealvAccessLogs>.IndexKeys.Ascending(x => x.StatusCode);
        var statusCodeIndexModel = new CreateIndexModel<TealvAccessLogs>(statusCodeIndexKeys);
        await _collection.Indexes.CreateOneAsync(statusCodeIndexModel, cancellationToken: cancellationToken);

        _logger.LogInformation("MongoDB {CollectionName} collection initialized", _collection.CollectionNamespace.CollectionName);
    }

        /// <summary>
    /// 插入日志条目
    /// </summary>
    public async Task InsertAsync(TealvAccessLogs entry, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entry, cancellationToken: cancellationToken);
    }

        /// <summary>
    /// 查询日志列表
    /// </summary>
    public async Task<LogQueryResult> QueryAsync(LogQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(parameters);

        var totalCount = Convert.ToInt32(await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken));

        var sort = Builders<TealvAccessLogs>.Sort.Descending(x => x.Id);
        var findResult = await _collection
            .Find(filter)
            .Sort(sort)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Limit(parameters.PageSize)
            .ToListAsync(cancellationToken);

        return new LogQueryResult
        {
            Logs = findResult,
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
        var filter = Builders<TealvAccessLogs>.Filter.Eq(x => x.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

        /// <summary>
    /// 获取统计信息
    /// </summary>
    public async Task<LogStatistics> GetStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        var filter = BuildDateFilter(startDate, endDate);

        var totalCount = Convert.ToInt32(await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken));

        // 使用聚合计算平均持续时间
        double avgDuration = 0;
        var avgPipeline = PipelineDefinition<TealvAccessLogs, BsonDocument>.Create(
            new BsonDocument("$match", filter.ToBsonDocument()),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", BsonNull.Value },
                { "avgDuration", new BsonDocument("$avg", "$Duration") }
            })
        );

        using (var cursor = await _collection.AggregateAsync(avgPipeline, cancellationToken: cancellationToken))
        {
            var avgResult = await cursor.FirstOrDefaultAsync(cancellationToken);
            if (avgResult != null && avgResult.Contains("avgDuration"))
            {
                avgDuration = avgResult["avgDuration"].ToDouble();
            }
        }

        // 使用聚合计算状态码统计
        var statusPipeline = PipelineDefinition<TealvAccessLogs, BsonDocument>.Create(
            new BsonDocument("$match", filter.ToBsonDocument()),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$StatusCode" },
                { "Count", new BsonDocument("$sum", 1) }
            }),
            new BsonDocument("$sort", new BsonDocument("Count", -1))
        );

        var statusCodeStats = new List<StatusCodeStat>();
        using (var cursor = await _collection.AggregateAsync(statusPipeline, cancellationToken: cancellationToken))
        {
            await cursor.ForEachAsync(doc =>
            {
                statusCodeStats.Add(new StatusCodeStat
                {
                    StatusCode = doc["_id"].IsBsonNull ? null : doc["_id"].ToInt32(),
                    Count = doc["Count"].ToInt32()
                });
            }, cancellationToken);
        }

        return new LogStatistics
        {
            TotalRequests = totalCount,
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
        var filter = Builders<TealvAccessLogs>.Filter.Lt(x => x.Timestamp, cutoffDate);

        var result = await _collection.DeleteManyAsync(filter, cancellationToken);
        _logger.LogInformation("Purged {Count} old access logs from MongoDB", result.DeletedCount);

        return Convert.ToInt32(result.DeletedCount);
    }

    private FilterDefinition<TealvAccessLogs> BuildFilter(LogQueryParameters parameters)
    {
        var builder = Builders<TealvAccessLogs>.Filter;
        var filters = new List<FilterDefinition<TealvAccessLogs>>();

        if (!string.IsNullOrEmpty(parameters.Method))
        {
            filters.Add(builder.Eq(x => x.Method, parameters.Method.ToUpper()));
        }

        if (!string.IsNullOrEmpty(parameters.Path))
        {
            filters.Add(builder.Regex(x => x.Path, new BsonRegularExpression(parameters.Path, "i")));
        }

        if (parameters.StatusCode.HasValue)
        {
            filters.Add(builder.Eq(x => x.StatusCode, parameters.StatusCode.Value));
        }

        if (parameters.StartDate.HasValue)
        {
            filters.Add(builder.Gte(x => x.Timestamp, parameters.StartDate.Value));
        }

        if (parameters.EndDate.HasValue)
        {
            filters.Add(builder.Lte(x => x.Timestamp, parameters.EndDate.Value));
        }

        return filters.Count > 0 ? builder.And(filters) : builder.Empty;
    }

    private FilterDefinition<TealvAccessLogs> BuildDateFilter(DateTime? startDate, DateTime? endDate)
    {
        var builder = Builders<TealvAccessLogs>.Filter;
        var filters = new List<FilterDefinition<TealvAccessLogs>>();

        if (startDate.HasValue)
        {
            filters.Add(builder.Gte(x => x.Timestamp, startDate.Value));
        }

        if (endDate.HasValue)
        {
            filters.Add(builder.Lte(x => x.Timestamp, endDate.Value));
        }

        return filters.Count > 0 ? builder.And(filters) : builder.Empty;
    }
}
