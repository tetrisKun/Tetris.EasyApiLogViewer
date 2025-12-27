using MongoDB.Driver;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tetris.EasyApiLogViewer.Db.Abstract.Configuration;
using Tetris.EasyApiLogViewer.Db.Abstract.Models;
using Tetris.EasyApiLogViewer.Db.Abstract.Repository;

namespace Tetris.EasyApiLogViewer.Db.MongoDB.Repository;

/// <summary>
/// MongoDB管理员账户仓储实现
/// </summary>
public class MongoDbTealvAdminAccountRepository : ITevlaAdminAccountRepository
{
    private readonly IMongoCollection<TealvAdminAccount> _collection;
    private readonly ILogger<MongoDbTealvAdminAccountRepository> _logger;

    /// <summary>
    /// 初始化 MongoDB MongoDbTealvAdminAccountRepository仓储
    /// </summary>
    /// <param name="options">配置选项</param>
    /// <param name="logger">日志记录器</param>
    public MongoDbTealvAdminAccountRepository(
        IOptions<TealvOptions> options,
        ILogger<MongoDbTealvAdminAccountRepository> logger)
    {
        var client = new MongoClient(options.Value.ConnectionString);
        var databaseName = MongoUrl.Create(options.Value.ConnectionString).DatabaseName;
        var database = client.GetDatabase(databaseName);
        _collection = database.GetCollection<TealvAdminAccount>(options.Value.TealvAdminAuth.AdminAccountTableName);
        _logger = logger;
    }

        /// <summary>
    /// 初始化数据库表
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // 创建唯一索引
        var indexKeysDefinition = Builders<TealvAdminAccount>.IndexKeys.Ascending(x => x.Username);
        var indexModel = new CreateIndexModel<TealvAdminAccount>(
            indexKeysDefinition,
            new CreateIndexOptions { Unique = true });

        await _collection.Indexes.CreateOneAsync(indexModel, cancellationToken: cancellationToken);
        _logger.LogInformation("MongoDB {CollectionName} collection initialized", _collection.CollectionNamespace.CollectionName);
    }

        /// <summary>
    /// 根据用户名获取账户
    /// </summary>
    public async Task<TealvAdminAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var filter = Builders<TealvAdminAccount>.Filter.Eq(x => x.Username, username);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// 根据ID获取账户
    /// </summary>
    public async Task<TealvAdminAccount?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<TealvAdminAccount>.Filter.Eq(x => x.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

        /// <summary>
    /// 获取所有账户
    /// </summary>
    public async Task<List<TealvAdminAccount>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(_ => true).Sort(Builders<TealvAdminAccount>.Sort.Ascending(x => x.Id)).ToListAsync(cancellationToken);
    }

        /// <summary>
    /// 创建账户
    /// </summary>
    public async Task<long> CreateAsync(TealvAdminAccount account, CancellationToken cancellationToken = default)
    {
        account.CreatedAt = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;

        await _collection.InsertOneAsync(account, cancellationToken: cancellationToken);
        return account.Id;
    }

        /// <summary>
    /// 更新账户
    /// </summary>
    public async Task<bool> UpdateAsync(TealvAdminAccount account, CancellationToken cancellationToken = default)
    {
        var filter = Builders<TealvAdminAccount>.Filter.Eq(x => x.Id, account.Id);
        account.UpdatedAt = DateTime.UtcNow;

        var updateDefinition = Builders<TealvAdminAccount>.Update
            .Set(x => x.Username, account.Username)
            .Set(x => x.PasswordHash, account.PasswordHash)
            .Set(x => x.Salt, account.Salt)
            .Set(x => x.DisplayName, account.DisplayName)
            .Set(x => x.Role, account.Role)
            .Set(x => x.IsActive, account.IsActive)
            .Set(x => x.UpdatedAt, account.UpdatedAt);

        var result = await _collection.UpdateOneAsync(filter, updateDefinition, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

        /// <summary>
    /// 删除账户
    /// </summary>
    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<TealvAdminAccount>.Filter.Eq(x => x.Id, id);
        var result = await _collection.DeleteOneAsync(filter, cancellationToken);
        return result.DeletedCount > 0;
    }

        /// <summary>
    /// 更新最后登录时间
    /// </summary>
    public async Task UpdateLastLoginAsync(long id, DateTime loginTime, CancellationToken cancellationToken = default)
    {
        var filter = Builders<TealvAdminAccount>.Filter.Eq(x => x.Id, id);
        var updateDefinition = Builders<TealvAdminAccount>.Update.Set(x => x.LastLoginAt, loginTime);

        await _collection.UpdateOneAsync(filter, updateDefinition, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 检查用户名是否存在
    /// </summary>
    public async Task<bool> ExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        var filter = Builders<TealvAdminAccount>.Filter.Eq(x => x.Username, username);
        var count = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        return count > 0;
    }
}
