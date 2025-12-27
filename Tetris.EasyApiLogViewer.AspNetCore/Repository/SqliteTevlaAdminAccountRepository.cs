using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tetris.EasyApiLogViewer.Db.Abstract.Configuration;
using Tetris.EasyApiLogViewer.Db.Abstract.Models;
using Tetris.EasyApiLogViewer.Db.Abstract.Repository;

namespace Tetris.EasyApiLogViewer.AspNetCore.Repository;

/// <summary>
/// SQLite管理员账户仓储实现
/// </summary>
public class SqliteTevlaAdminAccountRepository : ITevlaAdminAccountRepository
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly ILogger<SqliteTevlaAdminAccountRepository> _logger;

    /// <summary>
    /// 初始化 SQLite 管理员账户仓储
    /// </summary>
    /// <param name="options">配置选项</param>
    /// <param name="logger">日志记录器</param>
    public SqliteTevlaAdminAccountRepository(
        IOptions<TealvOptions> options,
        ILogger<SqliteTevlaAdminAccountRepository> logger)
    {
        var opts = options.Value;
        _connectionString = opts.ConnectionString.Contains("Data Source")
            ? opts.ConnectionString
            : $"Data Source={Path.Combine(AppContext.BaseDirectory, opts.ConnectionString)}";
        _tableName = opts.TealvAdminAuth.AdminAccountTableName;
        _logger = logger;
    }

    private async Task<SqliteConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>
    /// 初始化数据库表
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(cancellationToken);
        var createTableSql = $@"
            CREATE TABLE IF NOT EXISTS {_tableName} (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                username        TEXT NOT NULL UNIQUE,
                password_hash   TEXT NOT NULL,
                salt            TEXT NOT NULL,
                display_name    TEXT,
                role            TEXT NOT NULL DEFAULT 'viewer',
                is_active       INTEGER NOT NULL DEFAULT 1,
                last_login_at   TEXT,
                created_at      TEXT NOT NULL DEFAULT (datetime('now')),
                updated_at      TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE INDEX IF NOT EXISTS idx_{_tableName}_username ON {_tableName}(username);
        ";

        await using var command = connection.CreateCommand();
        command.CommandText = createTableSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("SQLite {TableName} table initialized", _tableName);
    }

    /// <summary>
    /// 根据用户名获取账户
    /// </summary>
    public async Task<TealvAdminAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = $@"
            SELECT id, username, password_hash, salt, display_name, role, is_active, last_login_at, created_at, updated_at
            FROM {_tableName}
            WHERE username = @Username";
        command.Parameters.AddWithValue("@Username", username);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapToAdminAccount(reader);
        }

        return null;
    }

    /// <summary>
    /// 根据ID获取账户
    /// </summary>
    public async Task<TealvAdminAccount?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = $@"
            SELECT id, username, password_hash, salt, display_name, role, is_active, last_login_at, created_at, updated_at
            FROM {_tableName}
            WHERE id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapToAdminAccount(reader);
        }

        return null;
    }

    /// <summary>
    /// 获取所有账户
    /// </summary>
    public async Task<List<TealvAdminAccount>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = $@"
            SELECT id, username, password_hash, salt, display_name, role, is_active, last_login_at, created_at, updated_at
            FROM {_tableName}
            ORDER BY id";

        var accounts = new List<TealvAdminAccount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(MapToAdminAccount(reader));
        }

        return accounts;
    }

    /// <summary>
    /// 创建账户
    /// </summary>
    public async Task<long> CreateAsync(TealvAdminAccount account, CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = $@"
            INSERT INTO {_tableName} (username, password_hash, salt, display_name, role, is_active, created_at, updated_at)
            VALUES (@Username, @PasswordHash, @Salt, @DisplayName, @Role, @IsActive, @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("@Username", account.Username);
        command.Parameters.AddWithValue("@PasswordHash", account.PasswordHash);
        command.Parameters.AddWithValue("@Salt", account.Salt);
        command.Parameters.AddWithValue("@DisplayName", account.DisplayName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Role", account.Role);
        command.Parameters.AddWithValue("@IsActive", account.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    /// <summary>
    /// 更新账户
    /// </summary>
    public async Task<bool> UpdateAsync(TealvAdminAccount account, CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = $@"
            UPDATE {_tableName}
            SET username = @Username,
                password_hash = @PasswordHash,
                salt = @Salt,
                display_name = @DisplayName,
                role = @Role,
                is_active = @IsActive,
                updated_at = @UpdatedAt
            WHERE id = @Id";

        command.Parameters.AddWithValue("@Id", account.Id);
        command.Parameters.AddWithValue("@Username", account.Username);
        command.Parameters.AddWithValue("@PasswordHash", account.PasswordHash);
        command.Parameters.AddWithValue("@Salt", account.Salt);
        command.Parameters.AddWithValue("@DisplayName", account.DisplayName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Role", account.Role);
        command.Parameters.AddWithValue("@IsActive", account.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected > 0;
    }

    /// <summary>
    /// 删除账户
    /// </summary>
    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = $"DELETE FROM {_tableName} WHERE id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected > 0;
    }

    /// <summary>
    /// 更新最后登录时间
    /// </summary>
    public async Task UpdateLastLoginAsync(long id, DateTime loginTime, CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = $"UPDATE {_tableName} SET last_login_at = @LastLoginAt WHERE id = @Id";
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@LastLoginAt", loginTime.ToString("yyyy-MM-dd HH:mm:ss"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 检查用户名是否存在
    /// </summary>
    public async Task<bool> ExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = $"SELECT COUNT(*) FROM {_tableName} WHERE username = @Username";
        command.Parameters.AddWithValue("@Username", username);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    private static TealvAdminAccount MapToAdminAccount(IDataReader reader)
    {
        return new TealvAdminAccount
        {
            Id = reader.GetInt64(0),
            Username = reader.GetString(1),
            PasswordHash = reader.GetString(2),
            Salt = reader.GetString(3),
            DisplayName = reader.IsDBNull(4) ? null : reader.GetString(4),
            Role = reader.GetString(5),
            IsActive = reader.GetInt32(6) == 1,
            LastLoginAt = reader.IsDBNull(7) ? null : DateTime.TryParse(reader.GetString(7), out var dt) ? dt : null,
            CreatedAt = DateTime.TryParse(reader.GetString(8), out var ca) ? ca : DateTime.MinValue,
            UpdatedAt = DateTime.TryParse(reader.GetString(9), out var ua) ? ua : DateTime.MinValue
        };
    }
}