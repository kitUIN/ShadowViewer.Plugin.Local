namespace ShadowViewer.Plugin.Local.Enums;

/// <summary>
/// 排序
/// </summary>
public enum LocalSort
{
    /// <summary>
    /// 阅读进度小-大
    /// </summary>
    Pa,
    /// <summary>
    /// 阅读进度大-小
    /// </summary>
    Pz,
    /// <summary>
    /// 字母顺序A-Z
    /// </summary>
    Az,
    /// <summary>
    /// 字母顺序Z-A
    /// </summary>
    Za,
    /// <summary>
    /// 阅读时间早-晚
    /// </summary>
    Ra,
    /// <summary>
    /// 阅读时间晚-早(默认)
    /// </summary>
    Rz,
    /// <summary>
    /// 创建时间早-晚
    /// </summary>
    Ca,
    /// <summary>
    /// 创建时间晚-早
    /// </summary>
    Cz
}