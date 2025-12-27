using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tetris.EasyApiLogViewer.Db.Abstract.Models;

namespace Tetris.EasyApiLogViewer.Db.Abstract.Repository
{
    /// <summary>
    /// 管理员账户仓储接口
    /// </summary>
    public interface ITevlaAdminAccountRepository
    {
        /// <summary>
        /// 根据用户名获取账户
        /// </summary>
        Task<TealvAdminAccount> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

        /// <summary>
        /// 根据ID获取账户
        /// </summary>
        Task<TealvAdminAccount> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取所有账户
        /// </summary>
        Task<List<TealvAdminAccount>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 创建账户
        /// </summary>
        Task<long> CreateAsync(TealvAdminAccount account, CancellationToken cancellationToken = default);

        /// <summary>
        /// 更新账户
        /// </summary>
        Task<bool> UpdateAsync(TealvAdminAccount account, CancellationToken cancellationToken = default);

        /// <summary>
        /// 删除账户
        /// </summary>
        Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 更新最后登录时间
        /// </summary>
        Task UpdateLastLoginAsync(long id, DateTime loginTime, CancellationToken cancellationToken = default);

        /// <summary>
        /// 检查用户名是否存在
        /// </summary>
        Task<bool> ExistsAsync(string username, CancellationToken cancellationToken = default);

        /// <summary>
        /// 初始化数据库表
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);
    }
}
