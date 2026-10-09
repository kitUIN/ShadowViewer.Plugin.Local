namespace ShadowViewer.Plugin.Local.Enums;

/// <summary>
/// 书架筛选的阅读状态。
/// </summary>
public enum ReadingStatus
{
    /// <summary>不限阅读状态。</summary>
    All,
    /// <summary>进度为零或尚无阅读记录。</summary>
    Unread,
    /// <summary>进度大于零且小于百分之百。</summary>
    Reading,
    /// <summary>进度达到百分之百。</summary>
    Completed
}
