using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShadowViewer.Models
{
    public class ComicAuthor
    {
        [SugarColumn(IsPrimaryKey = true)]
        public long ComicId { get; set; }
        [SugarColumn(IsPrimaryKey = true)]
        public long AuthorId { get; set; }
    }
}
