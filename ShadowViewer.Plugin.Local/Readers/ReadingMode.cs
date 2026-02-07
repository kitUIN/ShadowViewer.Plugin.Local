namespace ShadowViewer.Plugin.Local.Readers;

/// <summary>
/// 漫画阅读器模式
/// </summary>
public enum ReadingMode
{
    /// <summary>
    /// 纵向滚动
    /// </summary>
    Scroll,

    /// <summary>
    /// 单页模式
    /// </summary>
    Single,

    /// <summary>
    /// 双页模式 (从右向左)
    /// </summary>
    SpreadRtl,

    /// <summary>
    /// 双页模式 (从左向右)
    /// </summary>
    SpreadLtr
}