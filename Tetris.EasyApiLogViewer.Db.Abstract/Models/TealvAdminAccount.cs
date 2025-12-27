using System;

namespace Tetris.EasyApiLogViewer.Db.Abstract.Models
{
    /// <summary>
    /// 管理员账户
    /// </summary>
    public class TealvAdminAccount
    {
        /// <summary>
        /// 账户ID
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 用户名（唯一）
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 密码哈希
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// 密码盐值
        /// </summary>
        public string Salt { get; set; } = string.Empty;

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 角色（admin/viewer）
        /// </summary>
        public string Role { get; set; } = "viewer";

        /// <summary>
        /// 是否激活
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// 最后登录时间
        /// </summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// 最后登录IP
        /// </summary>
        public string LastLoginIp { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}
