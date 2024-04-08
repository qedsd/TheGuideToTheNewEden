using System;
using System.Collections.Generic;
using System.Text;
using static TheGuideToTheNewEden.Core.DBModels.IdName;

namespace TheGuideToTheNewEden.Core.Models
{
    public class IdNameLong
    {
        public IdNameLong(long id, string name, CategoryEnum category)
        {
            Id = id;
            Name = name;
            Category = category;
        }
        public long Id { get; set; }
        public string Name { get; set; }
        public CategoryEnum Category { get; set; }
    }
}
