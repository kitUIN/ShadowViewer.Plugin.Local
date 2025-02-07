using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using ShadowViewer.Core.Enums;
using ShadowViewer.Core.Services;
using ShadowViewer.Core.Helpers;
using ShadowViewer.Core.Models;
using ShadowViewer.Plugin.Local.Models;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace ShadowViewer.ViewModels
{
    public partial class BookShelfViewModel: ObservableObject
    {
        /// <summary>
        /// 该文件夹内是否为空
        /// </summary>
        [ObservableProperty]
        private bool isEmpty = true;
        /// <summary>
        /// 文件夹内总数量
        /// </summary>
        [ObservableProperty]
        private int folderTotalCounts;
        /// <summary>
        /// 当前文件夹名称
        /// </summary>
        public string CurrentName { get; private set; }
        /// <summary>
        /// 当前文件夹ID
        /// </summary>
        public long Path { get; private set; } = -1;
        /// <summary>
        /// 原始地址
        /// </summary>
        public Uri OriginPath { get; private set; }
        /// <summary>
        /// 排序-<see cref="ShadowSorts"/>
        /// </summary>
        public ShadowSorts Sorts { get; set; } = ShadowSorts.RZ;
        /// <summary>
        /// 该文件夹下的漫画
        /// </summary>
        public ObservableCollection<LocalComic> LocalComics { get; } = new ObservableCollection<LocalComic>();
        private ISqlSugarClient Db { get; }
        private ILogger Logger { get; } 
        private readonly ICallableService caller;
        public BookShelfViewModel(ICallableService callableService,ISqlSugarClient sqlSugarClient, ILogger logger)
        {
            Db = sqlSugarClient;
            caller = callableService;
            caller.RefreshBookEvent += Caller_RefreshBookEvent;
            Logger = logger;
        }
        private void Caller_RefreshBookEvent(object sender, EventArgs e)
        {
            RefreshLocalComic();
        }
        public void Init(Uri parameter)
        {
            LocalComics.CollectionChanged += LocalComics_CollectionChanged;
            OriginPath = parameter;
            var path = parameter.AbsolutePath.Split(['/',], StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            Path = -1;
            if (path != "bookshelf")
            {
                try
                {
                    Path = long.Parse(path ?? "-1");
                }
                catch (FormatException)
                {
                    
                }
            }
            Logger.Information("导航到{Path},Path={P}", OriginPath, Path);
            RefreshLocalComic();
            CurrentName = Path == -1 ? "本地" : Db.Queryable<LocalComic>().First(x => x.Id == Path).Name;
        }

        private void LocalComics_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Remove:
                {
                    if (e.OldItems != null)
                        foreach (LocalComic item in e.OldItems)
                        {
                            Db.Deleteable(item).ExecuteCommand();
                        }
                    break;
                }
                case NotifyCollectionChangedAction.Add:
                {
                    if (e.NewItems != null)
                        foreach (LocalComic item in e.NewItems)
                        {
                            // if (item.Id is null) continue;
                            if (!Db.Queryable<LocalComic>().Any(x => x.Id == item.Id))
                            {
                                Db.Insertable(item).ExecuteCommand();
                            }
                        }

                    break;
                }
            }
            IsEmpty = LocalComics.Count == 0;
            FolderTotalCounts = LocalComics.Count;
        }
        /// <summary>
        /// 刷新
        /// </summary>
        public void RefreshLocalComic()
        {
            LocalComics.Clear();
            var comics = Db.Queryable<LocalComic>().Where(x => x.ParentId == Path).ToList();
            switch (Sorts)
            {
                case ShadowSorts.AZ:
                    comics.Sort(LocalComic.AzSort); break;
                case ShadowSorts.ZA:
                    comics.Sort(LocalComic.ZaSort); break;
                case ShadowSorts.CA:
                    comics.Sort(LocalComic.CaSort); break;
                case ShadowSorts.CZ:
                    comics.Sort(LocalComic.CzSort); break;
                case ShadowSorts.RA:
                    comics.Sort(LocalComic.RaSort); break;
                case ShadowSorts.RZ:
                    comics.Sort(LocalComic.RzSort); break;
                // case ShadowSorts.PA:
                //     comics.Sort(LocalComic.PaSort); break;
                // case ShadowSorts.PZ:
                //     comics.Sort(LocalComic.PzSort); break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            foreach (var item in comics)
            {
                LocalComics.Add(item);
            }
        }
    }
}
