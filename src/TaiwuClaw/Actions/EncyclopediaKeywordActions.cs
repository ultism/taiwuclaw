using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TaiwuClaw.Core;

namespace TaiwuClaw.Actions
{
    /// <summary>
    /// 全文精确子串检索：返回正文或引用表里<b>精确包含</b>关键词的条目（多词空格分隔取 AND）。
    /// 与模糊检索的区别：不分词、不容错、不按相关度——确切术语/数值/短语用它，命中即文本里真的有这串。
    /// </summary>
    public class EncyclopediaFullTextAction : IAgentAction
    {
        public string Name => "encyclopedia_fulltext";

        public string Description =>
            "全文精确检索百晓册：返回正文或引用表格中精确包含关键词的条目（不分词、不容错）。" +
            "多个词用空格分隔表示『都要出现』（AND）。适合查确切术语、数值、配方名、固定短语。" +
            "需要容错/相关度排序时改用 encyclopedia_query；只想按词条名定位用 encyclopedia_title。";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["keyword"] = new JObject { ["type"] = "string", ["description"] = "要精确出现的词/短语（多词空格分隔=都要出现）" },
                ["limit"] = new JObject { ["type"] = "integer", ["description"] = "返回条数，默认 10" },
                ["withTables"] = new JObject { ["type"] = "boolean", ["description"] = "是否附带引用表格，默认 true" },
            },
            ["required"] = new JArray { "keyword" },
        };

        public JToken Execute(JObject args)
        {
            string keyword = (string)args["keyword"];
            if (string.IsNullOrEmpty(keyword))
                throw new Exception("missing 'keyword'");
            int limit = args["limit"]?.Value<int>() ?? 10;
            bool withTables = args["withTables"]?.Value<bool>() ?? true;

            List<int> all = EncyclopediaSearch.FullText(keyword);
            return KeywordResult.Build(all, limit, withTables);
        }
    }

    /// <summary>
    /// 词条名精确子串检索：只在标题路径里找<b>精确包含</b>关键词的条目（多词空格分隔取 AND）。
    /// 适合「有没有叫 X 的功法/物品/词条」这类按名定位，不翻正文。
    /// </summary>
    public class EncyclopediaTitleAction : IAgentAction
    {
        public string Name => "encyclopedia_title";

        public string Description =>
            "按词条名检索百晓册：只在条目标题（标题路径）中精确包含关键词的条目，不搜正文。" +
            "多个词用空格分隔表示都要出现。适合『有没有叫 X 的功法/物品/词条』这类按名定位。" +
            "要搜正文用 encyclopedia_fulltext；要容错/相关度用 encyclopedia_query。";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["keyword"] = new JObject { ["type"] = "string", ["description"] = "词条名里要出现的词/短语（多词空格分隔=都要出现）" },
                ["limit"] = new JObject { ["type"] = "integer", ["description"] = "返回条数，默认 20" },
                ["withTables"] = new JObject { ["type"] = "boolean", ["description"] = "是否附带引用表格，默认 false（按名定位通常只看标题）" },
            },
            ["required"] = new JArray { "keyword" },
        };

        public JToken Execute(JObject args)
        {
            string keyword = (string)args["keyword"];
            if (string.IsNullOrEmpty(keyword))
                throw new Exception("missing 'keyword'");
            int limit = args["limit"]?.Value<int>() ?? 20;
            bool withTables = args["withTables"]?.Value<bool>() ?? false;

            List<int> all = EncyclopediaSearch.ByTitle(keyword);
            return KeywordResult.Build(all, limit, withTables);
        }
    }

    /// <summary>子串检索的统一收尾：截断到 limit、渲染、命中数超出时回报 total/truncated 让模型知道还有更多。</summary>
    internal static class KeywordResult
    {
        public static JObject Build(List<int> all, int limit, bool withTables)
        {
            if (limit < 1) limit = 1;
            var page = all.Take(limit).ToList();
            JObject obj = EncyclopediaSearch.RenderHits(page, withTables);
            if (all.Count > page.Count)
            {
                obj["total"] = all.Count;       // 实际命中总数
                obj["truncated"] = true;        // 已截断，可缩小关键词或加 limit
            }
            return obj;
        }
    }
}
