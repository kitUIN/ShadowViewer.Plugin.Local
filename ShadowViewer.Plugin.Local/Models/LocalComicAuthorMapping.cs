using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShadowViewer.Plugin.Local.Models
{
    public class LocalComicAuthorMapping
    {
        
        public long ComicId { get; set; }
        

        public long AuthorId { get; set; }
    }
}
