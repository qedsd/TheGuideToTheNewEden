using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SqlSugar;
using TheGuideToTheNewEden.SDEBuilder.DeserializeModels;


namespace TheGuideToTheNewEden.SDEBuilder
{
    public static class Builder
    {
        public static async Task StartBuilder(string[] sdeFiles, LanguageEnum language, string outputFile)
        {
            string dbPath = Path.GetFullPath(outputFile);
            SqlSugarScope db = new SqlSugarScope(new ConnectionConfig()
            {
                ConnectionString = @"DataSource=" + dbPath,
                DbType = SqlSugar.DbType.Sqlite,
                IsAutoCloseConnection = true,
                ConfigureExternalServices = new ConfigureExternalServices
                {
                    EntityService = (c, p) =>
                    {
                        // int?  decimal?这种 isnullable=true
                        if (c.PropertyType.IsGenericType &&
                        c.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                        {
                            p.IsNullable = true;
                        }
                    }
                }
            });
            if (!System.IO.File.Exists(dbPath))
            {
                db.DbMaintenance.CreateDatabase();
            }
            var fileDatas = await Task.Run(()=> ReadAllFiles(sdeFiles, language));
            if (fileDatas.Any())
            {

            }
        }

        private static Dictionary<string, List<BaseModel>> ReadAllFiles(string[] sdeFiles, LanguageEnum language)
        {
            Dictionary<string, List<BaseModel>> fileDatas = new Dictionary<string, List<BaseModel>>();
            var models = typeof(BaseModel).Assembly.GetTypes().Where(p => p.FullName.Contains(".DeserializeModels.")).ToList();
            foreach (var file in sdeFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                var targetModel = models.FirstOrDefault(p => p.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                if (targetModel != null)
                {
                    List<BaseModel> datas = new List<BaseModel>();
                    fileDatas.Add(file, datas);
                    foreach (string line in File.ReadLines(file))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            //var formatStr = line.Replace("<b>", "").Replace("</b>", "").Replace("\\r", "").Replace("\\n", "");
                            try
                            {
                                datas.Add(JsonConvert.DeserializeObject(line, targetModel) as BaseModel);
                            }
                            catch (Exception ex)
                            {

                            }
                        }
                    }
                }
            }
            return fileDatas;
        }
    }
}
