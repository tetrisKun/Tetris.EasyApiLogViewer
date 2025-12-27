# Tetris.EasyApiLogViewer.AspNetCore

一个简单易用的 .NET WebAPI 请求日志记录和查看工具。

## 功能特性

- **请求/响应日志记录** - 自动记录 API 请求和响应的完整信息
- **内置 Web UI** - 提供美观的日志查看界面，无需额外部署
- **多数据库支持** - 系统默认支持 Sqlite,其它数据库可自行添加 Tetris.EasyApiLogViewer.Db.xx 扩展包,
    也可自行实现(需引用 Tetris.EasyApiLogViewer.Db.Contracts 包, 并自行实现 IAccessLogRepository和 IAdminAccountRepository 接口即可)
- **日志查询** - 支持按时间、HTTP 方法、路径、状态码等条件筛选
- **统计分析** - 提供请求统计和状态码分布分析
- **请求重放** - 支持重放历史请求进行调试
- **管理员认证** - 内置 JWT 认证保护日志查看器
- **敏感信息脱敏** - 自动掩码敏感请求头

## 安装

```bash
dotnet add package Tetris.EasyApiLogViewer.AspNetCore
```

## 快速开始

### 1. 注册服务

在 `Program.cs` 中添加服务注册：

```csharp
using BackendApi.ApiAccessLog.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 添加 API 日志记录服务
builder.Services.AddApiAccessLog(builder.Configuration);

// 或使用 Action 配置
builder.Services.AddApiAccessLog(options =>
{
    options.EnableRequestLogging = true;
    options.DatabaseProvider = DatabaseProvider.Sqlite;
    options.ConnectionString = "api_logs.db";
});

var app = builder.Build();

// 启用 API 日志记录中间件
app.UseApiAccessLog();

app.Run();
```

### 2. 配置选项

在 `appsettings.json` 中添加配置：

> 🌟 默认只记录/api/开头的请求,如需其它请自行添加配置

```json
{
  "ApiAccessLog": {
    "EnableRequestLogging": true,
    "DatabaseProvider": "Sqlite",
    "ConnectionString": "api_logs.db",
    "LogRequestHeaders": true,
    "MaxRequestBodySize": 4096,
    "MaxResponseBodySize": 4096,
    "ExcludedPaths": [
      "/health",
      "/metrics",
      "/log-viewer",
      "/api/logs"
    ],
    "IncludePaths": [
      "/api/"
    ],//默认只记录/api/开头的请求,如需其它请自行添加配置
    "SensitiveHeaders": [
      "Authorization",
      "Cookie",
      "X-API-Key"
    ],
    "AdminAuth": {
      "EnableDefaultAdmin": true,
      "DefaultAdminUsername": "admin",
      "DefaultAdminPassword": "Admin@123",
      "JwtSecretKey": "your-super-secret-key-at-least-32-characters-long",
      "JwtIssuer": "ApiAccessLog",
      "JwtAudience": "ApiAccessLog",
      "JwtExpirationMinutes": 60,
      "PasswordHashIterations": 10000
    }
  }
}
```

### 3. 访问日志查看器

启动应用后，访问：

```
http://localhost:5000/log-viewer
```

使用默认管理员账户登录：
- 用户名：`admin`
- 密码：`Admin@123`

> ⚠️ **重要**: 请在首次登录后修改默认密码！

## 配置说明

### 基本配置

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `EnableRequestLogging` | bool | `true` | 是否启用请求日志记录 |
| `DatabaseProvider` | enum | `Sqlite` | 数据库提供程序 (`Sqlite` 或 `AzureSql`) |
| `ConnectionString` | string | `api_logs.db` | 数据库连接字符串 |
| `LogRequestHeaders` | bool | `true` | 是否记录请求头 |
| `MaxRequestBodySize` | int | `4096` | 最大请求体记录大小（字节） |
| `MaxResponseBodySize` | int | `4096` | 最大响应体记录大小（字节） |
| `ExcludedPaths` | string[] | `[]` | 排除的路径前缀列表 |
| `SensitiveHeaders` | string[] | `["Authorization"]` | 需要脱敏的请求头 |

### 认证配置

| 配置项 | 类型 | 默认值         | 说明 |
|--------|------|-------------|------|
| `EnableDefaultAdmin` | bool | `true`      | 是否创建默认管理员账户 |
| `DefaultAdminUsername` | string | `admin`     | 默认管理员用户名 |
| `DefaultAdminPassword` | string | `Admin@123` | 默认管理员密码 |
| `JwtSecretKey` | string | -           | JWT 签名密钥（至少32字符） |
| `JwtExpirationMinutes` | int | `60`        | JWT 过期时间（分钟） |

## 使用 Azure SQL

生产环境建议使用 Azure SQL：

```json
{
  "ApiAccessLog": {
    "DatabaseProvider": "AzureSql",
    "ConnectionString": "Server=your-server.database.windows.net;Database=your-db;User Id=your-user;Password=your-password;Encrypt=True;TrustServerCertificate=False;"
  }
}
```

## API 端点

日志查看器提供以下 API 端点：

| 端点 | 方法 | 说明 |
|------|------|------|
| `/log-viewer` | GET | 日志查看器 Web UI |
| `/api/logs` | GET | 查询日志列表 |
| `/api/logs/{id}` | GET | 获取单条日志详情 |
| `/api/logs/statistics` | GET | 获取统计信息 |
| `/api/logs/replay` | POST | 重放请求 |
| `/api/logs/auth/login` | POST | 管理员登录 |
| `/api/logs/auth/me` | GET | 获取当前登录信息 |
| `/api/logs/auth/change-password` | POST | 修改密码 |

## 日志查询参数

`GET /api/logs` 支持以下查询参数：

| 参数 | 类型 | 说明 |
|------|------|------|
| `page` | int | 页码（默认 1） |
| `pageSize` | int | 每页数量（默认 20） |
| `method` | string | HTTP 方法过滤 |
| `path` | string | 路径模糊匹配 |
| `statusCode` | int | 状态码过滤 |
| `startDate` | datetime | 开始时间 |
| `endDate` | datetime | 结束时间 |

## 安全建议

1. **修改默认密码** - 首次登录后立即修改默认管理员密码
2. **使用强密钥** - 在生产环境使用强随机 JWT 密钥
3. **限制访问** - 考虑在生产环境中限制 `/log-viewer` 的访问
4. **敏感信息脱敏** - 确保所有敏感请求头都添加到 `SensitiveHeaders` 列表
5. **日志保留策略** - 定期清理旧日志以节省存储空间

## 许可证

MIT License - 详见 [LICENSE](LICENSE) 文件
