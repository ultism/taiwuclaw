using System;
using System.Collections.Generic;
using Game.Views.Encyclopedia;
using Newtonsoft.Json.Linq;
using TaiwuClaw.Core;

namespace TaiwuClaw.Actions
{
    /// <summary>一条可跳转的百晓册命中：标题路径 + 精确 key（= EncyclopediaContentItem.Key）。</summary>
    public struct EntryRef
    {
        public string Title;
        public string Key;
        public EntryRef(string title, string key) { Title = title; Key = key; }
    }

    /// <summary>
    /// 百晓册检索。BM25 相关度排序（中文按字 uni/bi-gram 分词），
    /// 检索范围含标题路径、正文与引用表，复用游戏已解析数据。
    /// </summary>
    public class EncyclopediaQueryAction : IAgentAction
    {
        /// <summary>最近一次检索命中的可跳转词条，供 UI 渲染"在游戏内打开原文"链接。
        /// 整个引用一次性替换（主线程写、OnGUI 读），读侧拿到的始终是完整快照。</summary>
        public static IReadOnlyList<EntryRef> RecentHits = Array.Empty<EntryRef>();

        public string Name => "encyclopedia_query";

        public string Description =>
            "检索《太吾绘卷》百晓册（游戏内置百科），按相关度返回最匹配的条目。" +
            "keyword 可以是自然短语或多个空格分隔的词（如“促织 捕捉点 概率”），无需精确匹配。" +
            "返回每条的标题路径 titlePath、正文 content 及引用表格 tables。";

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

            if (EncyclopediaContent.DataArray.Count == 0)
                EncyclopediaDataProcessor.Init();

            var results = new JArray();
            var hits = new List<EntryRef>();
            foreach (int docId in EncyclopediaIndex.Search(keyword, limit))
            {
                var item = EncyclopediaContent.DataArray[docId];
                results.Add(EncyclopediaText.Render(item, withTables));
                hits.Add(new EntryRef(EncyclopediaText.TitlePath(item), item.Key));
            }
            RecentHits = hits; // 引用整体替换，读侧无需加锁

            return new JObject { ["count"] = results.Count, ["results"] = results };
        }
    }
}
