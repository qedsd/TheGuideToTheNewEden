using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SqlSugar;
using TheGuideToTheNewEden.SDEBuilder.DeserializeModels;

namespace TheGuideToTheNewEden.SDEBuilder.Helpers
{
    /// <summary>
    /// 构建后自检：校验关键表是否存在、行数是否与源文件一致，
    /// 并把表清单、未解析文件、构建信息写到 db 同目录的 .report.txt。
    /// </summary>
    internal static class BuildVerifier
    {
        public static void Verify(SqlSugarScope db, string outputFile, Dictionary<string, List<BaseModel>> fileDatas, string[] sdeFiles)
        {
            var expected = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var expanded = new List<string>();

            void Map(string table, string file)
            {
                if (fileDatas.TryGetValue(file, out var list))
                {
                    expected[table] = list.Count;
                }
            }

            Map("categories", "categories");
            Map("groups", "groups");
            Map("mapRegions", "mapRegions");
            Map("mapSolarSystems", "mapSolarSystems");
            Map("mapConstellations", "mapConstellations");
            Map("marketGroups", "marketGroups");
            Map("types", "types");
            Map("planetResources", "planetResources");
            Map("mapSecondarySuns", "mapSecondarySuns");
            Map("blueprints", "blueprints");
            Map("planetSchematics", "planetSchematics");
            Map("factions", "factions");
            Map("races", "races");
            Map("metaGroups", "metaGroups");
            Map("stationServices", "stationServices");
            Map("npcCorporationDivisions", "npcCorporationDivisions");
            Map("archetypes", "archetypes");
            Map("compressibleTypes", "compressibleTypes");
            Map("typeLists", "typeLists");
            Map("cloneGrades", "cloneGrades");
            Map("npcCharacters", "npcCharacters");
            Map("landmarks", "landmarks");
            Map("mercenaryTacticalOperations", "mercenaryTacticalOperations");

            if (fileDatas.ContainsKey("mapStargates") && fileDatas.ContainsKey("mapSolarSystems"))
            {
                expected["mapSolarSystemJumps"] = fileDatas["mapStargates"].Count;
            }

            bool hasMapCore = fileDatas.ContainsKey("mapStars")
                && fileDatas.ContainsKey("mapPlanets")
                && fileDatas.ContainsKey("mapMoons")
                && fileDatas.ContainsKey("mapAsteroidBelts")
                && fileDatas.ContainsKey("mapSolarSystems")
                && fileDatas.ContainsKey("groups");
            bool hasStationDeps = hasMapCore
                && fileDatas.ContainsKey("npcStations")
                && fileDatas.ContainsKey("npcCorporations")
                && fileDatas.ContainsKey("stationOperations");

            if (hasMapCore)
            {
                int denormalizeCount = 0;
                foreach (var key in new[] { "mapStars", "mapPlanets", "mapMoons", "mapAsteroidBelts" })
                {
                    denormalizeCount += fileDatas[key].Count;
                }
                if (fileDatas.TryGetValue("mapSecondarySuns", out var secondarySuns))
                {
                    denormalizeCount += secondarySuns.Count;
                }
                if (fileDatas.TryGetValue("mapStargates", out var stargates))
                {
                    denormalizeCount += stargates.Count;
                }
                if (hasStationDeps)
                {
                    denormalizeCount += fileDatas["npcStations"].Count;
                    expected["stations"] = fileDatas["npcStations"].Count;
                }
                expected["mapDenormalizes"] = denormalizeCount;
            }

            if (fileDatas.ContainsKey("blueprints"))
            {
                expanded.AddRange(new[] { "blueprintActivities", "blueprintMaterials", "blueprintProducts", "blueprintSkills" });
            }
            if (fileDatas.ContainsKey("typeMaterials")) expanded.Add("typeMaterials");
            if (fileDatas.ContainsKey("typeBonus")) expanded.Add("typeBonus");
            if (fileDatas.ContainsKey("dogmaAttributeCategories")) expanded.Add("dogmaAttributeCategories");
            if (fileDatas.ContainsKey("dogmaAttributes")) expanded.Add("dogmaAttributes");
            if (fileDatas.ContainsKey("dogmaEffects")) expanded.Add("dogmaEffects");
            if (fileDatas.ContainsKey("dogmaUnits")) expanded.Add("dogmaUnits");
            if (fileDatas.ContainsKey("typeDogma")) expanded.Add("typeDogma");
            if (fileDatas.ContainsKey("masteries")) expanded.Add("masteries");
            if (fileDatas.ContainsKey("planetSchematics")) expanded.Add("planetSchematicsTypeMap");

            string sdeFolder = sdeFiles != null && sdeFiles.Length > 0 ? Path.GetDirectoryName(sdeFiles[0]) : null;
            if (!string.IsNullOrEmpty(sdeFolder) && File.Exists(Path.Combine(sdeFolder, "_sde.jsonl")))
            {
                expected["sdeInfo"] = 1;
            }

            var tableNames = db.DbMaintenance.GetTableInfoList(false).Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var lines = new List<string>
            {
                $"SDE build report {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"output: {outputFile}",
                ""
            };
            var failures = new List<string>();

            foreach (var pair in expected.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!tableNames.Contains(pair.Key))
                {
                    failures.Add($"missing table: {pair.Key}");
                    lines.Add($"{pair.Key}: MISSING (expected {pair.Value})");
                    continue;
                }
                int count = db.Ado.GetInt($"select count(*) from \"{pair.Key}\"");
                bool ok = count == pair.Value;
                if (!ok)
                {
                    failures.Add($"{pair.Key}: {count} rows, expected {pair.Value}");
                }
                lines.Add($"{pair.Key}: {count}" + (ok ? " OK" : $" MISMATCH (expected {pair.Value})"));
            }

            foreach (var table in expanded.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                if (!tableNames.Contains(table))
                {
                    failures.Add($"missing table: {table}");
                    lines.Add($"{table}: MISSING");
                    continue;
                }
                int count = db.Ado.GetInt($"select count(*) from \"{table}\"");
                if (count <= 0)
                {
                    failures.Add($"{table}: empty");
                }
                lines.Add($"{table}: {count}" + (count > 0 ? " OK" : " EMPTY"));
            }

            var parsed = new HashSet<string>(fileDatas.Keys, StringComparer.OrdinalIgnoreCase);
            var unparsed = sdeFiles.Select(Path.GetFileNameWithoutExtension)
                .Where(p => !parsed.Contains(p) && !p.Equals("_sde", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
            lines.Add("");
            lines.Add($"source files: {sdeFiles.Length}, parsed: {fileDatas.Count}, unparsed by design: {unparsed.Count}");
            if (unparsed.Count > 0)
            {
                lines.Add("unparsed: " + string.Join(", ", unparsed));
            }

            var reportPath = outputFile + ".report.txt";
            File.WriteAllText(reportPath, string.Join(Environment.NewLine, lines), Encoding.UTF8);

            if (failures.Count > 0)
            {
                throw new InvalidOperationException($"SDE build verification failed ({failures.Count}): {string.Join("; ", failures)}. See {reportPath}");
            }
        }
    }
}
