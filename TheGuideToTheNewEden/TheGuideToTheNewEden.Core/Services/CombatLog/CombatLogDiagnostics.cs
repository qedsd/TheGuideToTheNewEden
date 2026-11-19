using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TheGuideToTheNewEden.Core.Services.CombatLog
{
    /// <summary>
    /// 战斗日志解析诊断（阶段 357）：记录没能识别的 <c>(combat)</c> 行。
    /// <para>
    /// 中文客户端的正文/装备名与英文不同，词表第一次不可能 100% 覆盖；把"漏网"的行留在内存里，
    /// 由页面/探针导出成文件，补 <see cref="CombatLogVocabulary"/> 即可闭环。
    /// </para>
    /// </summary>
    public static class CombatLogDiagnostics
    {
        private const int MaxEntries = 300;

        private static readonly List<string> UnparsedLines = new List<string>();
        private static readonly object Locker = new object();

        /// <summary>最近没能识别的行（去重，最多 300 条）。</summary>
        public static IReadOnlyList<string> Unparsed
        {
            get
            {
                lock (Locker)
                {
                    return UnparsedLines.ToList();
                }
            }
        }

        public static void ReportUnparsed(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            lock (Locker)
            {
                if (UnparsedLines.Contains(line))
                {
                    return;
                }

                UnparsedLines.Add(line);
                if (UnparsedLines.Count > MaxEntries)
                {
                    UnparsedLines.RemoveAt(0);
                }
            }
        }

        public static void Clear()
        {
            lock (Locker)
            {
                UnparsedLines.Clear();
            }
        }

        /// <summary>导出到文本文件（UTF-8），用于回传分析。</summary>
        public static void DumpTo(string path)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var builder = new StringBuilder();
                foreach (var line in Unparsed)
                {
                    builder.AppendLine(line);
                }

                File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }
    }
}
