using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Game.Views.Encyclopedia;
using Newtonsoft.Json.Linq;

namespace TaiwuClaw.Actions
{
    /// <summary>百晓册条目的文本处理：清洗、标题路径、表格渲染。供查询与索引共用。</summary>
    internal static class EncyclopediaText
    {
        private static readonly Regex TagRegex = new Regex("<[^>]+>", RegexOptions.Compiled);

        /// <summary>补 &lt; 还原（数据层未做）再剥离 TMP 富文本标签，得纯文本。</summary>
        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Replace("\\u003c", "<").Replace("\\u003e", ">").Replace("\\u005c", "\\");
            return TagRegex.Replace(s, "");
        }

        public static string TitlePath(EncyclopediaContentItem it)
        {
            var parts = new[] { it.Title1, it.Title2, it.Title3, it.Title4, it.Title5 }
                .Where(s => !string.IsNullOrEmpty(s));
            return string.Join(" / ", parts);
        }

        /// <summary>条目引用的 ConfigTable 渲染为 JSON（给查询结果用）。</summary>
        public static JArray RenderTables(EncyclopediaContentItem it)
        {
            var arr = new JArray();
            if (it.Inserts == null) return arr;
            foreach (var insertIndex in it.Inserts)
            {
                var r = EncyclopediaReference.Instance[insertIndex];
                if (r == null || r.InsertType != EEncyclopediaReferenceInsertType.ConfigTable) continue;

                var tableObj = new JObject { ["title"] = Clean(r.Title) };
                if (r.Desc != null) tableObj["header"] = new JArray(r.Desc.Select(Clean));
                var rows = new JArray();
                try
                {
                    foreach (var row in EncyclopediaDataProcessor.GetTable(r.Param))
                        rows.Add(new JArray(row.Select(Clean)));
                }
                catch { /* 子表缺失则只保留表头 */ }
                tableObj["rows"] = rows;
                arr.Add(tableObj);
            }
            return arr;
        }

        /// <summary>条目的完整可检索纯文本：标题路径 + 正文 + 引用表（标题/表头/数据）。</summary>
        public static string SearchBlob(EncyclopediaContentItem it)
        {
            var sb = new StringBuilder();
            sb.AppendLine(TitlePath(it));
            sb.AppendLine(Clean(it.Content));
            if (it.Inserts != null)
            {
                foreach (var insertIndex in it.Inserts)
                {
                    var r = EncyclopediaReference.Instance[insertIndex];
                    if (r == null || r.InsertType != EEncyclopediaReferenceInsertType.ConfigTable) continue;
                    sb.AppendLine(Clean(r.Title));
                    if (r.Desc != null) sb.AppendLine(string.Join(" ", r.Desc.Select(Clean)));
                    try
                    {
                        foreach (var row in EncyclopediaDataProcessor.GetTable(r.Param))
                            sb.AppendLine(string.Join(" ", row.Select(Clean)));
                    }
                    catch { }
                }
            }
            return sb.ToString();
        }

        /// <summary>查询命中条目的输出对象。</summary>
        public static JObject Render(EncyclopediaContentItem it, bool withTables)
        {
            var obj = new JObject
            {
                ["key"] = it.Key,
                ["titlePath"] = TitlePath(it),
                ["content"] = Clean(it.Content),
            };
            if (withTables)
            {
                var tables = RenderTables(it);
                if (tables.Count > 0) obj["tables"] = tables;
            }
            return obj;
        }
    }
}
