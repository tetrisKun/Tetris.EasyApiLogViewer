namespace Tetris.EasyApiLogViewer.Db.Abstract.Models
{
    /// <summary>
    /// 状态码统计项
    /// </summary>
    public class StatusCodeStat
    {
        /// <summary>
        /// 状态码
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// 请求数量
        /// </summary>
        public int Count { get; set; }
    }
}