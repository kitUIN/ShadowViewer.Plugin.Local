using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// 漫画与标签中间表
/// </summary>
public class LocalComicTagMapping
{
    /// <summary>
    /// 漫画Id
    /// </summary>
    public long ComicId { get; set; }

    /// <summary>
    /// 标签Id
    /// </summary>
    public long TagId { get; set; }
}