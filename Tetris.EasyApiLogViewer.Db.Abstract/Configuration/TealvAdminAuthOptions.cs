namespace Tetris.EasyApiLogViewer.Db.Abstract.Configuration
{
    /// <summary>
    /// 管理员认证配置
    /// </summary>
    public class TealvAdminAuthOptions
    {
        /// <summary>
        /// JWT密钥（用于管理员令牌，至少32字符）
        /// </summary>
        public string JwtSecretKey { get; set; } = "ApiAccessLogAdminSecretKey2025!@#MinLength32Chars";

        /// <summary>
        /// JWT令牌有效期（分钟）
        /// </summary>
        public int JwtExpirationMinutes { get; set; } = 60;

        /// <summary>
        /// JWT签发者
        /// </summary>
        public string JwtIssuer { get; set; } = "ApiAccessLog";

        /// <summary>
        /// JWT受众
        /// </summary>
        public string JwtAudience { get; set; } = "ApiAccessLogAdmin";

        /// <summary>
        /// 是否启用默认管理员账户
        /// </summary>
        public bool EnableDefaultAdmin { get; set; } = true;

        /// <summary>
        /// 默认管理员用户名
        /// </summary>
        public string DefaultAdminUsername { get; set; } = "admin";

        /// <summary>
        /// 默认管理员密码（仅首次初始化使用）
        /// </summary>
        public string DefaultAdminPassword { get; set; } = "Admin@123";

        /// <summary>
        /// 密码哈希迭代次数
        /// </summary>
        public int PasswordHashIterations { get; set; } = 10000;

        /// <summary>
        /// 管理员账户表名
        /// </summary>
        public string AdminAccountTableName { get; set; } = "tealv_admin_account";
    }
}