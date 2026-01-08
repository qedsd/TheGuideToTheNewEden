using System;
using System.Collections.Generic;
using System.Text;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.LocalDBModels
{
    [SugarTable("categories")]
    [SugarIndex("index_categories_id", nameof(Id), OrderByType.Asc)]
    [SugarIndex("index_categories_name", nameof(Name), OrderByType.Asc)]
    public class Categories
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public string Name { get; set; }
        public Categories() { }
        public Categories(DBModels.Categories model)
        {
            Id = model.Id;
            Name = model.Name;
        }
    }
}
