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
using TheGuideToTheNewEden.SDEBuilder.Models;

namespace TheGuideToTheNewEden.SDEBuilder
{
    public static class Builder1
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
            var models = typeof(BaseModel).Assembly.GetTypes().Where(p => p.FullName.Contains(".Models.")).ToList();
            foreach (var file in sdeFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                switch (fileName)
                {
                    case "blueprints":
                        {
                            List<Blueprints> datas = new List<Blueprints>();
                            foreach (string line in File.ReadLines(file))
                            {
                                if (!string.IsNullOrWhiteSpace(line))
                                {
                                    var item = JsonConvert.DeserializeObject<Blueprints>(line);
                                    datas.Add(item);
                                    //if(item.Activities.Manufacturing == null &&
                                    //    item.Activities.Invention == null &&
                                    //    item.Activities.Copying == null &&
                                    //    item.Activities.ResearchMaterial == null &&
                                    //    item.Activities.ResearchTime == null &&
                                    //    item.Activities.Reaction == null
                                    //    )
                                    //{

                                    //}
                                }
                            }
                            db.CodeFirst.InitTables(typeof(BlueprintsDb));
                            db.CodeFirst.InitTables(typeof(BlueprintSkillDb));
                            db.CodeFirst.InitTables(typeof(BlueprintProductDb));
                            db.CodeFirst.InitTables(typeof(BlueprintMaterialDb));
                            db.CodeFirst.InitTables(typeof(BlueprintActivitieDb));
                            List<BlueprintsDb> blueprints = new List<BlueprintsDb>();
                            List<BlueprintSkillDb> blueprintSkills = new List<BlueprintSkillDb>();
                            List<BlueprintProductDb> blueprintProducts = new List<BlueprintProductDb>();
                            List<BlueprintMaterialDb> blueprintMaterials = new List<BlueprintMaterialDb>();
                            List<BlueprintActivitieDb> blueprintActivities = new List<BlueprintActivitieDb>();
                            foreach (var data in datas)
                            {
                                blueprints.Add(new BlueprintsDb()
                                {
                                    BlueprintTypeID = data.BlueprintTypeID,
                                    MaxProductionLimit = data.MaxProductionLimit,
                                });
                                void addActivity(ActivityRequire activityRequire, short activitieID)
                                {
                                    if (activityRequire == null) return;
                                    blueprintActivities.Add(new BlueprintActivitieDb()
                                    {
                                        ActivitieID = activitieID,
                                        BlueprintTypeID = data.BlueprintTypeID,
                                        Time = activityRequire.Time,
                                    });
                                    if(activityRequire.Materials != null)
                                    {
                                        foreach(var material in activityRequire.Materials)
                                        {
                                            blueprintMaterials.Add(new BlueprintMaterialDb()
                                            {
                                                ActivitieID = activitieID,
                                                BlueprintTypeID = data.BlueprintTypeID,
                                                Quantity = material.Quantity,
                                            });
                                        }
                                    }
                                    if (activityRequire.Products != null)
                                    {
                                        foreach (var product in activityRequire.Products)
                                        {
                                            blueprintProducts.Add(new BlueprintProductDb()
                                            {
                                                ActivitieID = activitieID,
                                                ProductTypeID = product.TypeID,
                                                BlueprintTypeID = data.BlueprintTypeID,
                                                Quantity = product.Quantity,
                                                Probability = product.Probability,
                                            });
                                        }
                                    }
                                    if (activityRequire.Skills != null)
                                    {
                                        foreach (var skill in activityRequire.Skills)
                                        {
                                            blueprintSkills.Add(new BlueprintSkillDb()
                                            {
                                                ActivitieID = activitieID,
                                                BlueprintTypeID = data.BlueprintTypeID,
                                                Level = skill.Level,
                                                SkillID = skill.TypeID
                                            });
                                        }
                                    }
                                }
                                addActivity(data.Activities.Manufacturing, 1);
                                addActivity(data.Activities.ResearchTime, 3);
                                addActivity(data.Activities.ResearchMaterial, 4);
                                addActivity(data.Activities.Copying, 5);
                                addActivity(data.Activities.Invention, 8);
                                addActivity(data.Activities.Reaction, 11);
                            }
                            await db.Insertable(blueprints).ExecuteCommandAsync();
                            await db.Insertable(blueprintSkills).ExecuteCommandAsync();
                            await db.Insertable(blueprintProducts).ExecuteCommandAsync();
                            await db.Insertable(blueprintMaterials).ExecuteCommandAsync();
                            await db.Insertable(blueprintActivities).ExecuteCommandAsync();
                        }
                        break;
                    default:
                        {
                            var targetModel = models.FirstOrDefault(p => p.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                            if (targetModel != null)
                            {
                                List<BaseModel> datas = new List<BaseModel>();
                                foreach (string line in File.ReadLines(file))
                                {
                                    if (!string.IsNullOrWhiteSpace(line))
                                    {
                                        datas.Add(JsonConvert.DeserializeObject(line, targetModel) as BaseModel);
                                    }
                                }
                                db.CodeFirst.InitTables(targetModel);

                                List<Dictionary<string, object>> dc = datas.Select(p => p.GetDict(language)).ToList();
                                await db.Insertable(dc).AS(targetModel.Name).ExecuteCommandAsync();
                            }
                        }
                        break;
                }
            }
        }
    }
}
