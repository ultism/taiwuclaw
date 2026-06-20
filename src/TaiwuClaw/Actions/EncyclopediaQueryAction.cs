using System;
using Newtonsoft.Json.Linq;
using TaiwuClaw.Core;

namespace TaiwuClaw.Actions
{
    /// <summary>
    /// 模糊/相关度检索：BM25 排序（中文按字 uni/bi-gram 分词），范围含标题路径、正文与引用表。
    /// 容错、支持多词，适合「大概想找…」。精确子串见 encyclopedia_fulltext / encyclopedia_title。
    /// </summary>
    public class EncyclopediaQueryAction : IAgentAction
    {
        public string Name => "encyclopedia_query";

        public string Description =>
            "模糊检索《太吾绘卷》百晓册（游戏内置百科），按相关度返回最匹配的条目——首选的通用检索。" +
            "keyword 可以是自然短语或多个空格分隔的词（如“促织 捕捉点 概率”），容错、无需精确匹配。" +
            "返回每条的标题路径 titlePath、正文 content 及引用表格 tables。" +
            "若要『正文里精确出现某串』用 encyclopedia_fulltext；若只想按词条名找用 encyclopedia_title。";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["keyword"] = new JObject { ["type"] = "string", ["description"] = "检索词/短语（可多词，空格分隔）" },
                ["limit"] = new JObject { ["type"] = "integer", ["description"] = "返回条数，默认 8" },
                ["withTables"] = new JObject { ["type"] = "boolean", ["description"] = "是否附带引用表格，默认 true" },
            },
            ["required"] = new JArray { "keyword" },
        };

        public JToken Execute(JObject args)
        {
            string keyword = (string)args["keyword"];
            if (string.IsNullOrEmpty(keyword))
                throw new Exception("missing 'keyword'");
            int limit = args["limit"]?.Value<int>() ?? 8;
            bool withTables = args["withTables"]?.Value<bool>() ?? true;

            var docs = EncyclopediaSearch.Bm25(keyword, limit);
            return EncyclopediaSearch.RenderHits(docs, withTables);
        }
    }
}
