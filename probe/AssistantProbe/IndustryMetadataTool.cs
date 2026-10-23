using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EVEStandard.Enumerations;
using SqlSugar;
using TheGuideToTheNewEden.SDEBuilder;
using TheGuideToTheNewEden.SDEBuilder.DBModels;

namespace TheGuideToTheNewEden.Probe;

/// <summary>把最新 SDE 的工业元数据表增量导入现有 main.db（不重建主数据）。</summary>
internal static class IndustryMetadataTool
{
    private static readonly string[] FileNames =
    {
        "industryActivities.jsonl",
        "industryAssemblyLines.jsonl",
        "industryInstallationTypes.jsonl",
        "industryModifierSources.jsonl",
        "industryTargetFilters.jsonl",
    };

    public static void Run(string sdeDir, string dbPath)
    {
        var files = FileNames.Select(p => Path.Combine(sdeDir, p)).Where(File.Exists).ToArray();
        if (files.Length == 0)
        {
            throw new InvalidOperationException($"找不到工业元数据文件：{sdeDir}");
        }

        var data = Builder.ReadAllFiles(files, LanguageEnum.En);
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"DataSource={dbPath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
        });
        try
        {
            // 元数据表只由本工具维护；先删后建，确保新增列（如 FilterID）也能落到已有库。
            foreach (var table in new[]
                     {
                         "industryActivities",
                         "industryAssemblyLines",
                         "industryAssemblyLineDetails",
                         "industryInstallationTypes",
                         "industryModifierSources",
                         "industryTargetFilters",
                         "industryTargetFilterCategories",
                         "industryTargetFilterGroups",
                     })
            {
                try { db.DbMaintenance.DropTable(table); } catch { /* 表不存在时忽略 */ }
            }

            db.CodeFirst.InitTables(
                typeof(IndustryActivityDefinition),
                typeof(IndustryAssemblyLine),
                typeof(IndustryAssemblyLineDetail),
                typeof(IndustryInstallationType),
                typeof(IndustryModifierSource),
                typeof(IndustryTargetFilterDefinition),
                typeof(IndustryTargetFilterCategory),
                typeof(IndustryTargetFilterGroup));

            db.Deleteable<IndustryActivityDefinition>().ExecuteCommand();
            db.Deleteable<IndustryAssemblyLine>().ExecuteCommand();
            db.Deleteable<IndustryAssemblyLineDetail>().ExecuteCommand();
            db.Deleteable<IndustryInstallationType>().ExecuteCommand();
            db.Deleteable<IndustryModifierSource>().ExecuteCommand();
            db.Deleteable<IndustryTargetFilterDefinition>().ExecuteCommand();
            db.Deleteable<IndustryTargetFilterCategory>().ExecuteCommand();
            db.Deleteable<IndustryTargetFilterGroup>().ExecuteCommand();

            var activities = Builder.GetIndustryActivities(data, LanguageEnum.En) ?? new List<IndustryActivityDefinition>();
            var assembly = Builder.GetIndustryAssemblyLines(data, LanguageEnum.En);
            var installations = Builder.GetIndustryInstallationTypes(data) ?? new List<IndustryInstallationType>();
            var modifiers = Builder.GetIndustryModifierSources(data) ?? new List<IndustryModifierSource>();
            var filters = Builder.GetIndustryTargetFilters(data);

            db.Insertable(activities).ExecuteCommand();
            db.Insertable(assembly.Item1 ?? new List<IndustryAssemblyLine>()).ExecuteCommand();
            db.Insertable(assembly.Item2 ?? new List<IndustryAssemblyLineDetail>()).ExecuteCommand();
            db.Insertable(installations).ExecuteCommand();
            db.Insertable(modifiers).ExecuteCommand();
            db.Insertable(filters.Item1 ?? new List<IndustryTargetFilterDefinition>()).ExecuteCommand();
            db.Insertable(filters.Item2 ?? new List<IndustryTargetFilterCategory>()).ExecuteCommand();
            db.Insertable(filters.Item3 ?? new List<IndustryTargetFilterGroup>()).ExecuteCommand();

            Console.WriteLine($"activities={activities.Count}, assemblyLines={assembly.Item1?.Count ?? 0}, details={assembly.Item2?.Count ?? 0}, installations={installations.Count}, modifiers={modifiers.Count}, filters={filters.Item1?.Count ?? 0}, filterCategories={filters.Item2?.Count ?? 0}, filterGroups={filters.Item3?.Count ?? 0}");
        }
        finally
        {
            db.Close();
            db.Dispose();
        }
    }
}
