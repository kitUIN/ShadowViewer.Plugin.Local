using System.Collections.Generic;
using System.Linq;
using DryIoc;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Core.I18n;
using SqlSugar;

namespace ShadowViewer.Plugin.Local.Models
{
    /// <summary>
    /// 路径树
    /// </summary>
    public class ShadowPath
    {
        private readonly LocalComic comic;
        public string Name => comic.Name;
        public long Id => comic.Id;
        public string Thumb => comic.Thumb;
        public bool IsFolder => comic.IsFolder;
        public List<ShadowPath> Children { get; } = [];
        public ShadowPath(LocalComic comic)
        {
            this.comic = comic;
        }
        
        
    }
}
