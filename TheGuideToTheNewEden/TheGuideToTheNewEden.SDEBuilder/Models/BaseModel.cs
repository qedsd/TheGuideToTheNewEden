using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.Models
{
    public class DBTable
    {
        public Type TableModel { get; set; }
        public Dictionary<string, object> TableValues { get; set; }
    }
    public abstract class BaseModel
    {
        [SqlSugar.SugarColumn(IsPrimaryKey =true)]
        [JsonProperty("_key")]
        public int Id { get; set; }
        public abstract Dictionary<string, object> GetDict(LanguageEnum language);

        //public abstract List<DBTable> GetDBTables(LanguageEnum language);
    }
}
