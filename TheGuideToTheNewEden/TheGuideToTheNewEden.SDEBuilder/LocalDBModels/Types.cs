using System;
using System.Collections.Generic;
using System.Text;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.LocalDBModels
{
    [SugarTable("types")]
    [SugarIndex("index_types_id", nameof(Id), OrderByType.Asc)]
    [SugarIndex("index_types_name", nameof(Name), OrderByType.Asc)]
    public class Types
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public string Name { get; set; }
        [SqlSugar.SugarColumn(IsNullable = true)]
        public string Description { get; set; }
       
        public Types() { }
        public Types(DBModels.Types model)
        {
            Id = model.Id;
            Name = model.Name;
            Description = model.Description;
        }
    }
}
