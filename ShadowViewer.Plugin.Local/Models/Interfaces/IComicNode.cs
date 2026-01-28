using System;

namespace ShadowViewer.Plugin.Local.Models.Interfaces
{
    /// <summary>
    /// 漫画节点基础接口，定义了漫画层级结构中的通用属性。
    /// </summary>
    public interface IComicNode
    {
        /// <summary>
        /// 获取或设置节点的唯一标识符。
        /// </summary>
        long Id { get; set; }

        /// <summary>
        /// 获取或设置父节点的唯一标识符。如果为根节点，通常为 0 或特定值。
        /// </summary>
        long ParentId { get; set; }

        /// <summary>
        /// 获取或设置节点类型（例如：“Series”、“Volume”、“Chapter” 等）。
        /// </summary>
        string NodeType { get; set; }

        /// <summary>
        /// 获取或设置节点的显示名称。
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// 获取或设置节点缩略图的路径或 URL。
        /// </summary>
        string? Thumb { get; set; }

        /// <summary>
        /// 获取或设置该节点的创建日期和时间。
        /// </summary>
        DateTime CreatedDateTime { get; set; }

        /// <summary>
        /// 获取或设置该节点最后一次更新的日期和时间。
        /// </summary>
        DateTime UpdatedDateTime { get; set; }

        /// <summary>
        /// 获取或设置当前用户的本地阅读进度/记录。
        /// </summary>
        LocalReadingRecord ReadingRecord { get; set; }

        #region 排序

        /// <summary>
        /// 字母顺序A-Z
        /// </summary>
        public static int AzSort(IComicNode x, IComicNode y) => x.Name?.CompareTo(y.Name) ?? 1;

        /// <summary>
        /// 字母顺序Z-A
        /// </summary>
        public static int ZaSort(IComicNode x, IComicNode y) => y.Name?.CompareTo(x.Name) ?? 1;

        /// <summary>
        /// 更新时间早-晚
        /// </summary>
        public static int RaSort(IComicNode x, IComicNode y) => x.UpdatedDateTime.CompareTo(y.UpdatedDateTime);

        /// <summary>
        /// 更新时间晚-早(默认)
        /// </summary>
        public static int RzSort(IComicNode x, IComicNode y) => y.UpdatedDateTime.CompareTo(x.UpdatedDateTime);

        /// <summary>
        /// 创建时间早-晚
        /// </summary>
        public static int CaSort(IComicNode x, IComicNode y) => x.CreatedDateTime.CompareTo(y.CreatedDateTime);

        /// <summary>
        /// 创建时间晚-早
        /// </summary>
        public static int CzSort(IComicNode x, IComicNode y) => y.CreatedDateTime.CompareTo(x.CreatedDateTime);

        /// <summary>
        /// 阅读进度小-大
        /// </summary>
        public static int PaSort(IComicNode x, IComicNode y)
        {
            var xPercent = x.ReadingRecord?.Percent ?? 0;
            var yPercent = y.ReadingRecord?.Percent ?? 0;
            return xPercent.CompareTo(yPercent);
        }

        /// <summary>
        /// 阅读进度大-小
        /// </summary>
        public static int PzSort(IComicNode x, IComicNode y)
        {
            var xPercent = x.ReadingRecord?.Percent ?? 0;
            var yPercent = y.ReadingRecord?.Percent ?? 0;
            return yPercent.CompareTo(xPercent);
        }

        #endregion
    }
}