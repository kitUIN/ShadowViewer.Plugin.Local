using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShadowViewer.Plugin.Local.Models.Interfaces;

/// <summary>
/// 阅读记录
/// </summary>
public interface IReadingRecord
{
    /// <summary>
    /// 雪花Id
    /// </summary>
    long Id { get; set; }

    /// <summary>
    /// 额外的漫画Id
    /// </summary>
    string? ExtraComicId { get; set; }

    /// <summary>
    /// 阅读进度
    /// </summary>
    decimal Percent { get; set; }

    /// <summary>
    /// 上次阅读-页
    /// </summary>
    int LastPicture { get; set; }

    /// <summary>
    /// 上次阅读-话
    /// </summary>
    int LastEpisode { get; set; }
    /// <summary>
    /// 创建日期
    /// </summary>
    DateTime CreatedDateTime { get; set; }
    /// <summary>
    /// 更新日期
    /// </summary>
    DateTime UpdatedDateTime { get; set; }
}