using System;
using System.Collections.Generic;
using System.Text;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.LocalDBModels
{
    [SugarTable("groups")]
    [SugarIndex("index_groups_id", nameof(Id), OrderByType.Asc)]
    public class Groups 
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public string Name { get; set; }
        public Groups() { }
        public Groups(DBModels.Groups model)
        {
            Id = model.Id;
            Name = model.Name;
        }
    }
}
