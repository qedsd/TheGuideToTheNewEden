using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SqlSugar;
using TheGuideToTheNewEden.SDEBuilder;

namespace TheGuideToTheNewEden.Probe;

/// <summary>
/// SDEBuilder 离线探针（阶段 157）：
/// 用最小 JSONL fixture 跑真实 <see cref="Builder.StartBuilder"/>，断言
/// types 字段/NULL 语义、typeBonus 方向、mapDenormalizes 扩展、新增查找表、
/// 以及构建自检报告。不依赖完整 SDE 目录或 ESI。
/// </summary>
internal static class SdeSuite
{
    public static void Run(Check c)
    {
        var root = Path.Combine(Path.GetTempPath(), $"dsh-sde-probe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "probe.db");
        SqlSugarClient? db = null;
        try
        {
            var files = BuildFixtures();
            foreach (var pair in files)
            {
                File.WriteAllLines(Path.Combine(root, pair.Key + ".jsonl"), pair.Value);
            }

            var paths = files.Keys.Select(k => Path.Combine(root, k + ".jsonl")).ToArray();
            Builder.StartBuilder(paths, LanguageEnum.En, dbPath, false).GetAwaiter().GetResult();

            c.Section("SdeSuite · 构建产物");
            c.That(File.Exists(dbPath), "SDEBuilder 生成数据库");
            db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"DataSource={dbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });

            c.Section("SdeSuite · types 字段与 NULL 语义");
            var columns = db.DbMaintenance.GetColumnInfosByTableName("types", false)
                .Select(p => p.DbColumnName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var column in new[] { "MarketGroupID", "Capacity", "FactionID", "GraphicID", "MetaLevel", "Radius", "SoundID" })
            {
                c.That(columns.Contains(column), $"types 含列 {column}");
            }
            c.Equal(db.Ado.GetInt("select count(*) from types where MarketGroupID is null"), 1, "缺失 marketGroupID 写 NULL");
            c.Equal(db.Ado.GetInt("select count(*) from types where MarketGroupID = 516"), 1, "存在的 marketGroupID 保留");

            c.Section("SdeSuite · typeBonus 方向");
            c.Equal(db.Ado.GetInt("select count(*) from typeBonus where TypeID=582 and SkillTypeID=3330 and Bonus=10"), 1,
                "技能加成：船体 582 ← 技能 3330");
            c.Equal(db.Ado.GetInt("select count(*) from typeBonus where TypeID=582 and SkillTypeID=0 and Bonus=300"), 1,
                "roleBonus 的 SkillTypeID=0");

            c.Section("SdeSuite · mapDenormalizes 扩展");
            var groups = db.Ado.SqlQuery<int>("select distinct GroupID from mapDenormalizes order by GroupID").ToHashSet();
            foreach (var groupId in new[] { 6, 7, 8, 9, 10, 15, 995 })
            {
                c.That(groups.Contains(groupId), $"mapDenormalizes 含 GroupID {groupId}");
            }
            c.Equal(db.Ado.GetInt("select count(*) from mapDenormalizes"), 8, "mapDenormalizes = 恒星+行星+月球+小行星带+次级恒星+星门+空间站");
            c.Equal(db.Ado.GetInt("select count(*) from mapSolarSystemJumps"), 2, "星门跳转行数");
            c.Equal(db.Ado.GetInt("select count(*) from stations"), 1, "NPC 空间站行数");

            c.Section("SdeSuite · 新增查找表");
            foreach (var table in new[]
                     {
                         "factions", "races", "metaGroups", "stationServices", "npcCorporationDivisions",
                         "archetypes", "compressibleTypes", "typeLists", "masteries", "cloneGrades",
                         "npcCharacters", "landmarks", "mercenaryTacticalOperations", "mapSecondarySuns",
                         "sdeInfo", "blueprints", "blueprintActivities", "typeMaterials", "typeDogma",
                         "planetSchematics", "planetSchematicsTypeMap",
                         "industryActivities", "industryAssemblyLines", "industryAssemblyLineDetails",
                         "industryInstallationTypes", "industryModifierSources",
                         "industryTargetFilters", "industryTargetFilterCategories", "industryTargetFilterGroups"
                     })
            {
                c.That(db.Ado.GetInt($"select count(*) from \"{table}\"") > 0, $"{table} 有数据");
            }
            c.Equal(db.Ado.GetInt("select BuildNumber from sdeInfo where Id='sde'"), 3365090, "sdeInfo 记录 buildNumber");

            c.Section("SdeSuite · 构建自检报告");
            var report = dbPath + ".report.txt";
            c.That(File.Exists(report), "生成 .report.txt");
            var text = File.Exists(report) ? File.ReadAllText(report) : string.Empty;
            c.That(text.Contains("OK"), "报告含 OK 行");
            c.That(!text.Contains("MISMATCH"), "报告无 MISMATCH");
            c.That(!text.Contains("MISSING"), "报告无 MISSING");
        }
        finally
        {
            db?.Close();
            db?.Dispose();
            try
            {
                Directory.Delete(root, true);
            }
            catch
            {
                // 临时目录清理失败不影响断言结果
            }
        }
    }

    private static Dictionary<string, string[]> BuildFixtures()
    {
        return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["_sde"] = new[] { """{"_key":"sde","buildNumber":3365090,"releaseDate":"2026-05-28T12:16:29Z"}""" },
            ["categories"] = new[] { """{"_key":2,"name":{"en":"Celestial"},"published":true}""" },
            ["groups"] = new[]
            {
                """{"_key":6,"name":{"en":"Sun"},"categoryID":2,"published":true}""",
                """{"_key":7,"name":{"en":"Planet"},"categoryID":2,"published":true}""",
                """{"_key":8,"name":{"en":"Moon"},"categoryID":2,"published":true}""",
                """{"_key":9,"name":{"en":"Asteroid Belt"},"categoryID":2,"published":true}""",
                """{"_key":10,"name":{"en":"Stargate"},"categoryID":2,"published":true}""",
                """{"_key":15,"name":{"en":"Station"},"categoryID":3,"published":true}""",
                """{"_key":995,"name":{"en":"Secondary Sun"},"categoryID":2,"published":true}"""
            },
            ["mapRegions"] = new[]
            {
                """{"_key":10000001,"name":{"en":"Derelik"},"position":{"x":1,"y":2,"z":3},"nebulaID":11799,"wormholeClassID":7}"""
            },
            ["mapConstellations"] = new[]
            {
                """{"_key":20000001,"name":{"en":"San Matar"},"regionID":10000001,"position":{"x":1,"y":2,"z":3},"factionID":500007,"wormholeClassID":7,"solarSystemIDs":[30000001,30000002]}"""
            },
            ["mapSolarSystems"] = new[]
            {
                """{"_key":30000001,"name":{"en":"Test System"},"regionID":10000001,"constellationID":20000001,"position":{"x":1,"y":2,"z":3},"position2D":{"x":1,"y":2},"stargateIDs":[50000001],"securityStatus":0.5,"securityClass":"C"}""",
                """{"_key":30000002,"name":{"en":"Other System"},"regionID":10000001,"constellationID":20000001,"position":{"x":4,"y":5,"z":6},"position2D":{"x":4,"y":5},"stargateIDs":[50000002],"securityStatus":0.5,"securityClass":"C"}"""
            },
            ["mapStars"] = new[] { """{"_key":40000001,"solarSystemID":30000001,"typeID":45041,"radius":63350000}""" },
            ["mapPlanets"] = new[] { """{"_key":40000002,"solarSystemID":30000001,"celestialIndex":1,"orbitID":40000001,"typeID":11,"moonIDs":[40000003],"position":{"x":1,"y":2,"z":3}}""" },
            ["mapMoons"] = new[] { """{"_key":40000003,"solarSystemID":30000001,"orbitID":40000002,"orbitIndex":1,"typeID":14,"position":{"x":1,"y":2,"z":3}}""" },
            ["mapAsteroidBelts"] = new[] { """{"_key":40000004,"solarSystemID":30000001,"orbitID":40000002,"orbitIndex":1,"typeID":15,"position":{"x":1,"y":2,"z":3}}""" },
            ["mapSecondarySuns"] = new[] { """{"_key":40000005,"solarSystemID":30000001,"typeID":30577,"effectBeaconTypeID":30869,"position":{"x":1,"y":2,"z":3}}""" },
            ["mapStargates"] = new[]
            {
                """{"_key":50000001,"solarSystemID":30000001,"typeID":29633,"destination":{"solarSystemID":30000002,"stargateID":50000002},"position":{"x":1,"y":2,"z":3}}""",
                """{"_key":50000002,"solarSystemID":30000002,"typeID":29633,"destination":{"solarSystemID":30000001,"stargateID":50000001},"position":{"x":1,"y":2,"z":3}}"""
            },
            ["npcStations"] = new[]
            {
                """{"_key":60000001,"solarSystemID":30000001,"orbitID":40000002,"ownerID":1000001,"operationID":1,"typeID":1531,"useOperationName":true}"""
            },
            ["npcCorporations"] = new[]
            {
                """{"_key":1000001,"name":{"en":"Test Corp"},"description":{"en":"Test"},"deleted":false,"uniqueName":true}"""
            },
            ["stationOperations"] = new[]
            {
                """{"_key":1,"operationName":{"en":"Plantation"},"services":[3,5],"stationTypes":[{"_key":1,"_value":1531}]}"""
            },
            ["marketGroups"] = new[]
            {
                """{"_key":2,"name":{"en":"Blueprints"},"description":{"en":"Blueprints"},"hasTypes":false,"iconID":2703}"""
            },
            ["types"] = new[]
            {
                """{"_key":18,"groupID":458,"marketGroupID":516,"name":{"en":"Plagioclase"},"published":true,"volume":0.35,"basePrice":12800,"portionSize":100}""",
                """{"_key":34,"groupID":18,"name":{"en":"Tritanium"},"published":true,"volume":0.01,"basePrice":2,"portionSize":1}""",
                """{"_key":582,"groupID":25,"marketGroupID":61,"name":{"en":"Bantam"},"published":true,"volume":27289,"mass":1067000,"capacity":100,"factionID":0,"graphicID":0,"metaLevel":0,"radius":0,"soundID":0}""",
                """{"_key":3330,"groupID":257,"marketGroupID":377,"name":{"en":"Caldari Frigate"},"published":true,"volume":0.01,"portionSize":1}"""
            },
            ["planetResources"] = new[]
            {
                """{"_key":40013180,"power":740,"workforce":100,"reagent":{"amount_per_cycle":41,"cycle_period":3600,"secured_capacity":59860,"type_id":81144,"unsecured_capacity":59860}}"""
            },
            ["blueprints"] = new[]
            {
                """{"_key":681,"activities":{"manufacturing":{"materials":[{"quantity":86,"typeID":38}],"products":[{"quantity":1,"typeID":165}],"skills":[{"level":1,"typeID":3380}],"time":600}},"blueprintTypeID":681,"maxProductionLimit":300}"""
            },
            ["typeMaterials"] = new[] { """{"_key":18,"materials":[{"materialTypeID":34,"quantity":175}]}""" },
            ["typeBonus"] = new[]
            {
                """{"_key":582,"roleBonuses":[{"bonus":300,"bonusText":{"en":"role"},"importance":1,"unitID":105}],"types":[{"_key":3330,"_value":[{"bonus":10,"bonusText":{"en":"skill"},"importance":1,"unitID":105}]}]}"""
            },
            ["dogmaAttributeCategories"] = new[] { """{"_key":1,"name":"Fitting","description":"Fitting capabilities of a ship"}""" },
            ["dogmaAttributes"] = new[] { """{"_key":2,"attributeCategoryID":9,"dataType":0,"defaultValue":0.0,"description":"Boolean","displayWhenZero":false,"highIsGood":true,"name":"isOnline","published":false,"stackable":true}""" },
            ["dogmaEffects"] = new[] { """{"_key":4,"effectCategoryID":1,"name":"shieldBoosting","published":false,"isAssistance":false,"isOffensive":false,"isWarpSafe":true}""" },
            ["dogmaUnits"] = new[] { """{"_key":1,"name":"Length","description":{"en":"Meter"},"displayName":{"en":"m"}}""" },
            ["typeDogma"] = new[] { """{"_key":18,"dogmaAttributes":[{"attributeID":182,"value":3386.0},{"attributeID":277,"value":1.0}]}""" },
            ["planetSchematics"] = new[]
            {
                """{"_key":65,"cycleTime":3600,"name":{"en":"Superconductors"},"pins":[1],"types":[{"_key":2389,"isInput":true,"quantity":40},{"_key":9838,"isInput":false,"quantity":5}]}"""
            },
            ["factions"] = new[]
            {
                """{"_key":500001,"name":{"en":"Caldari State"},"description":{"en":"State"},"shortDescription":{"en":"State"},"corporationID":1000035,"militiaCorporationID":1000180,"solarSystemID":30000145,"iconID":1439,"sizeFactor":5.0,"uniqueName":true,"memberRaces":[1]}"""
            },
            ["races"] = new[]
            {
                """{"_key":1,"name":{"en":"Caldari"},"description":{"en":"Race"},"iconID":1439,"shipTypeID":601,"skills":[{"_key":3300,"_value":4}]}"""
            },
            ["metaGroups"] = new[] { """{"_key":1,"color":{"r":0.78,"g":0.66,"b":0.04},"name":{"en":"Tech I"}}""" },
            ["stationServices"] = new[] { """{"_key":1,"serviceName":{"en":"Bounty Missions"}}""" },
            ["npcCorporationDivisions"] = new[] { """{"_key":18,"displayName":"R&D","internalName":"R&D","leaderTypeName":{"en":"Chief Researcher"},"name":{"en":"R&D"}}""" },
            ["archetypes"] = new[] { """{"_key":2,"title":{"en":"Landmarks"},"description":{"en":"Landmarks"}}""" },
            ["compressibleTypes"] = new[] { """{"_key":18,"compressedTypeID":62528}""" },
            ["typeLists"] = new[] { """{"_key":4,"includedTypeIDs":[27674],"name":"ShipyardStructureTargets"}""" },
            ["masteries"] = new[] { """{"_key":582,"_value":[{"_key":0,"_value":[96,139]},{"_key":1,"_value":[96,139]}]}""" },
            ["cloneGrades"] = new[] { """{"_key":1,"name":"Alpha Caldari","skills":[{"level":5,"typeID":3300}]}""" },
            ["npcCharacters"] = new[] { """{"_key":3000001,"bloodlineID":1,"ceo":true,"corporationID":1000001,"gender":false,"locationID":60000001,"name":{"en":"Dame Hel"},"raceID":1,"startDate":"2003-03-12 20:04:00","uniqueName":false}""" },
            ["landmarks"] = new[] { """{"_key":1,"name":{"en":"EVE Gate"},"description":{"en":"Gate"},"position":{"x":1,"y":2,"z":3}}""" },
            ["mercenaryTacticalOperations"] = new[] { """{"_key":12367,"name":{"en":"Rogue Drone Emergence"},"description":{"en":"Drones"},"anarchyImpact":-20,"developmentImpact":10,"dungeonID":12367,"infomorphBonus":350}""" },
            ["industryActivities"] = new[]
            {
                """{"_key":1,"description":"Manufacturing of things","name":"Manufacturing"}""",
                """{"_key":9,"description":"The process of combining raw and intermediate materials to create advanced components","name":"Reactions"}"""
            },
            ["industryAssemblyLines"] = new[]
            {
                """{"_key":10,"activityID":1,"baseCostMultiplier":1.0,"baseMaterialMultiplier":1.0,"baseTimeMultiplier":1.0,"description":"Capital Ship Assembly","name":"Capital Ship Assembly","detailsPerGroup":[{"costMultiplier":1.0,"groupID":485,"materialMultiplier":1.0,"timeMultiplier":0.75}]}"""
            },
            ["industryInstallationTypes"] = new[]
            {
                """{"_key":16216,"assemblyLines":[{"assemblyLineID":28},{"assemblyLineID":29}]}"""
            },
            ["industryModifierSources"] = new[]
            {
                """{"_key":35825,"manufacturing":{"cost":[{"dogmaAttributeID":2601}],"material":[{"dogmaAttributeID":2600}],"time":[{"dogmaAttributeID":2602}]}}""",
                """{"_key":43920,"manufacturing":{"material":[{"dogmaAttributeID":2538,"filterID":2}]}}"""
            },
            ["industryTargetFilters"] = new[]
            {
                """{"_key":3,"categoryIDs":[6,32],"groupIDs":[963],"name":"Ships"}"""
            }
        };
    }
}
