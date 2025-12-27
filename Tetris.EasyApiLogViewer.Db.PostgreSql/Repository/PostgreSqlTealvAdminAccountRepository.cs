using System.Data;
using Npgsql;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tetris.EasyApiLogViewer.Db.Abstract.Configuration;
using Tetris.EasyApiLogViewer.Db.Abstract.Models;
using Tetris.EasyApiLogViewer.Db.Abstract.Repository;

namespace Tetris.EasyApiLogViewer.Db.PostgreSql.Repository;

/// <summary>
/// PostgreSQL管理员账户仓储实现
/// </summary>
public class PostgreSqlTealvAdminAccountRepository : ITevlaAdminAccountRepository
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly ILogger<PostgreSqlTealvAdminAccountRepository> _logger;

    /// <summary>
    /// 初始化 PostgreSQL PostgreSqlTealvAdminAccountRepository仓储
    /// </summary>
    /// <param name="options">配置选项</param>
    /// <param name="logger">日志记录器</param>
    public PostgreSqlTealvAdminAccountRepository(
        IOptions<TealvOptions> options,
        ILogger<PostgreSqlTealvAdminAccountRepository> logger)
    {
        _connectionString = options.Value.ConnectionString;
        _tableName = options.Value.TealvAdminAuth.AdminAccountTableName;
        _logger = logger;
    }

    private async Task<NpgsqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

        /// <summary>
    /// 初始化数据库表
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var fullTableName = $"\"{_tableName}\"";

        await using var connection = await GetConnectionAsync(cancellationToken);
        var createTableSql = $@"
            CREATE TABLE IF NOT EXISTS {fullTableName}
            (
                ""Id""            BIGSERIAL       NOT NULL,
                ""Username""      VARCHAR(100)    NOT NULL,
                ""PasswordHash""  VARCHAR(255)    NOT NULL,
                ""Salt""          VARCHAR(100)    NOT NULL,
                ""DisplayName""   VARCHAR(200)    NULL,
                ""Role""          VARCHAR(50)     NOT NULL DEFAULT 'viewer',
                ""IsActive""      BOOLEAN         NOT NULL DEFAULT true,
                ""LastLoginAt""   TIMESTAMP(3)    NULL,
                ""CreatedAt""     TIMESTAMP(3)    NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
                ""UpdatedAt""     TIMESTAMP(3)    NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
                PRIMARY KEY (""Id""),
                CONSTRAINT ""UQ_{_tableName}_Username"" UNIQUE (""Username"")
            );";

        await using var command = connection.CreateCommand();
        command.CommandText = createTableSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("PostgreSQL {TableName} table initialized", _tableName);
    }

        /// <summary>
    /// 根据用户名获取账户
    /// </summary>
    public async Task<TealvAdminAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var fullTableName = $"\"{_tableName}\"";
        command.CommandText = $@"
            SELECT ""Id"", ""Username"", ""PasswordHash"", ""Salt"", ""DisplayName"", ""Role"", ""IsActive"", ""LastLoginAt"", ""CreatedAt"", ""UpdatedAt""
            FROM {fullTableName}
            WHERE ""Username"" = @Username";
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

        var fullTableName = $"\"{_tableName}\"";
        command.CommandText = $@"
            SELECT ""Id"", ""Username"", ""PasswordHash"", ""Salt"", ""DisplayName"", ""Role"", ""IsActive"", ""LastLoginAt"", ""CreatedAt"", ""UpdatedAt""
            FROM {fullTableName}
            WHERE ""Id"" = @Id";
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

        var fullTableName = $"\"{_tableName}\"";
        command.CommandText = $@"
            SELECT ""Id"", ""Username"", ""PasswordHash"", ""Salt"", ""DisplayName"", ""Role"", ""IsActive"", ""LastLoginAt"", ""CreatedAt"", ""UpdatedAt""
            FROM {fullTableName}
            ORDER BY ""Id""";

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

        var fullTableName = $"\"{_tableName}\"";
        command.CommandText = $@"
            INSERT INTO {fullTableName} (""Username"", ""PasswordHash"", ""Salt"", ""DisplayName"", ""Role"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
            VALUES (@Username, @PasswordHash, @Salt, @DisplayName, @Role, @IsActive, @CreatedAt, @UpdatedAt)
            RETURNING ""Id""";

        command.Parameters.AddWithValue("@Username", account.Username);
        command.Parameters.AddWithValue("@PasswordHash", account.PasswordHash);
        command.Parameters.AddWithValue("@Salt", account.Salt);
        command.Parameters.AddWithValue("@DisplayName", account.DisplayName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Role", account.Role);
        command.Parameters.AddWithValue("@IsActive", account.IsActive);
        command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
        command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);

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

        var fullTableName = $"\"{_tableName}\"";
        command.CommandText = $@"
            UPDATE {fullTableName}
            SET ""Username"" = @Username,
                ""PasswordHash"" = @PasswordHash,
                ""Salt"" = @Salt,
                ""DisplayName"" = @DisplayName,
                ""Role"" = @Role,
                ""IsActive"" = @IsActive,
                ""UpdatedAt"" = @UpdatedAt
            WHERE ""Id"" = @Id";

        command.Parameters.AddWithValue("@Id", account.Id);
        command.Parameters.AddWithValue("@Username", account.Username);
        command.Parameters.AddWithValue("@PasswordHash", account.PasswordHash);
        command.Parameters.AddWithValue("@Salt", account.Salt);
        command.Parameters.AddWithValue("@DisplayName", account.DisplayName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Role", account.Role);
        command.Parameters.AddWithValue("@IsActive", account.IsActive);
        command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);

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

        var fullTableName = $"\"{_tableName}\"";
        command.CommandText = $"DELETE FROM {fullTableName} WHERE \"Id\" = @Id";
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

        var fullTableName = $"\"{_tableName}\"";
        command.CommandText = $"UPDATE {fullTableName} SET \"LastLoginAt\" = @LastLoginAt WHERE \"Id\" = @Id";
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@LastLoginAt", loginTime);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 检查用户名是否存在
    /// </summary>
    public async Task<bool> ExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var fullTableName = $"\"{_tableName}\"";
        command.CommandText = $"SELECT COUNT(*) FROM {fullTableName} WHERE \"Username\" = @Username";
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
            IsActive = reader.GetBoolean(6),
            LastLoginAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
            CreatedAt = reader.GetDateTime(8),
            UpdatedAt = reader.GetDateTime(9)
        };
    }
}
