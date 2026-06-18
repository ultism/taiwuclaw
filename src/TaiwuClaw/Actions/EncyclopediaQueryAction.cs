using System;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Views.Encyclopedia;
using Newtonsoft.Json.Linq;
using TaiwuClaw.Core;

namespace TaiwuClaw.Actions
{
    /// <summary>
    /// 百晓册全文检索。复用游戏已解析的 EncyclopediaContent.DataArray，
    /// 对标题路径与正文做关键词匹配，并顺着 Inserts 拼接引用表格。
    /// （游戏内置搜索只搜标题，故此处自己做全文。）
    /// </summary>
    public class EncyclopediaQueryAction : IAgentAction
    {
        public string Name => "encyclopedia.query";

        public string Description =>
            "全文检索百晓册。参数：keyword(string，必填)、limit(int，默认20)、withTables(bool，默认true)。" +
            "返回匹配条目的 key、标题路径 titlePath、清洗后的正文 content，以及引用的表格 tables。";

        private static readonly Regex TagRegex = new Regex("<[^>]+>", RegexOptions.Compiled);

        public JToken Execute(JObject args)
        {
            string keyword = (string)args["keyword"];
            if (string.IsNullOrEmpty(keyword))
                throw new Exception("missing 'keyword'");
            int limit = args["limit"]?.Value<int>() ?? 20;
            bool withTables = args["withTables"]?.Value<bool>() ?? true;

            EnsureLoaded();

            var results = new JArray();
            foreach (var it in EncyclopediaContent.DataArray)
            {
                string titlePath = TitlePath(it);
                string content = Clean(it.Content);

                bool hit = titlePath.IndexOf(keyword, StringComparison.Ordinal) >= 0
                        || (content != null && content.IndexOf(keyword, StringComparison.Ordinal) >= 0);
                if (!hit) continue;

                var obj = new JObject
                {
                    ["key"] = it.Key,
                    ["titlePath"] = titlePath,
                    ["content"] = content,
                };
                if (withTables)
                {
                    var tables = RenderTables(it);
                    if (tables.Count > 0) obj["tables"] = tables;
                }
                results.Add(obj);
                if (results.Count >= limit) break;
            }

            return new JObject { ["count"] = results.Count, ["results"] = results };
        }

        private static void EnsureLoaded()
        {
            // 进入游戏前百晓册可能尚未初始化；按需触发一次。
            if (EncyclopediaContent.DataArray.Count == 0)
                EncyclopediaDataProcessor.Init();
        }

        private static string TitlePath(EncyclopediaContentItem it)
        {
            var parts = new[] { it.Title1, it.Title2, it.Title3, it.Title4, it.Title5 }
                .Where(s => !string.IsNullOrEmpty(s));
            return string.Join(" / ", parts);
        }

        private static JArray RenderTables(EncyclopediaContentItem it)
        {
            var arr = new JArray();
            if (it.Inserts == null) return arr;
            foreach (var insertIndex in it.Inserts)
            {
                var reference = EncyclopediaReference.Instance[insertIndex];
                if (reference == null || reference.InsertType != EEncyclopediaReferenceInsertType.ConfigTable)
                    continue;

                var tableObj = new JObject { ["title"] = Clean(reference.Title) };
                if (reference.Desc != null)
                    tableObj["header"] = new JArray(reference.Desc.Select(Clean));

                var rows = new JArray();
                try
                {
                    foreach (var row in EncyclopediaDataProcessor.GetTable(reference.Param))
                        rows.Add(new JArray(row.Select(Clean)));
                }
                catch { /* 子表文件缺失则只保留表头 */ }
                tableObj["rows"] = rows;
                arr.Add(tableObj);
            }
            return arr;
        }

        /// <summary>
        /// 文本清洗：Init/GetTable 已做 \n、,、ColorReplace；此处补 < 还原（数据层未做），
        /// 再剥离所有 TMP 富文本标签，得到适合喂 agent 的纯文本。
        /// </summary>
        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Replace("\\u003c", "<").Replace("\\u003e", ">").Replace("\\u005c", "\\");
            s = TagRegex.Replace(s, "");
            return s;
        }
    }
}
