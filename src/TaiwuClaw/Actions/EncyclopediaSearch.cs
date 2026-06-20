using System;
using System.Collections.Generic;
using Game.Views.Encyclopedia;
using Newtonsoft.Json.Linq;

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
    /// 百晓册检索的共享服务：被所有检索 action 复用，统一「加载语料 → 检索原语 → 渲染+记录命中」三件事，
    /// 使各 action 退化成薄壳、UI 跳转链接对任何检索方式都一致可用。
    ///
    /// 三种检索原语：
    ///   - <see cref="Bm25"/>     模糊/相关度（容错、多词、按相关度排序，走 EncyclopediaIndex）
    ///   - <see cref="FullText"/> 全文精确子串（正文+引用表，多词取 AND；适合确切术语/数值/短语）
    ///   - <see cref="ByTitle"/>  词条名精确子串（仅标题路径；适合「有没有叫 X 的词条」）
    /// </summary>
    internal static class EncyclopediaSearch
    {
        /// <summary>最近一次检索命中的可跳转词条，供 UI 渲染「在游戏内打开原文」链接。
        /// 整个引用一次性替换（主线程写、OnGUI 读），读侧拿到的始终是完整快照。</summary>
        public static IReadOnlyList<EntryRef> RecentHits = Array.Empty<EntryRef>();

        /// <summary>确保游戏已解析百晓册数据。</summary>
        public static void EnsureLoaded()
        {
            if (EncyclopediaContent.DataArray.Count == 0)
                EncyclopediaDataProcessor.Init();
        }

        // 子串检索用的小写语料缓存：仅当首次用到全文/词条检索时构建，避免重复展开表格。
        private static string[] _blobLower;
        private static string[] _titleLower;

        private static void EnsureCorpus()
        {
            EnsureLoaded();
            if (_blobLower != null) return;
            var arr = EncyclopediaContent.DataArray;
            int n = arr.Count;
            var blob = new string[n];
            var title = new string[n];
            for (int i = 0; i < n; i++)
            {
                blob[i] = EncyclopediaText.SearchBlob(arr[i]).ToLowerInvariant();
                title[i] = EncyclopediaText.TitlePath(arr[i]).ToLowerInvariant();
            }
            _titleLower = title;
            _blobLower = blob; // 最后赋值：其它线程看到非空即代表两数组都已就绪
        }

        /// <summary>模糊/相关度检索：返回相关度 Top-N 的 DataArray 索引。</summary>
        public static List<int> Bm25(string query, int limit)
        {
            EnsureLoaded();
            return EncyclopediaIndex.Search(query ?? "", limit);
        }

        /// <summary>全文精确子串检索：正文+引用表里包含全部关键词（空格分隔取 AND）的条目，DataArray 顺序。</summary>
        public static List<int> FullText(string keyword) => Substring(keyword, false);

        /// <summary>词条名精确子串检索：标题路径里包含全部关键词的条目，DataArray 顺序。</summary>
        public static List<int> ByTitle(string keyword) => Substring(keyword, true);

        private static List<int> Substring(string keyword, bool titleOnly)
        {
            EnsureCorpus();
            var hits = new List<int>();
            string[] terms = (keyword ?? "").ToLowerInvariant()
                .Split(new[] { ' ', '\t', '　' }, StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length == 0) return hits;

            string[] corpus = titleOnly ? _titleLower : _blobLower;
            for (int i = 0; i < corpus.Length; i++)
            {
                string hay = corpus[i];
                bool all = true;
                foreach (var t in terms)
                    if (hay.IndexOf(t, StringComparison.Ordinal) < 0) { all = false; break; }
                if (all) hits.Add(i);
            }
            return hits;
        }

        /// <summary>把 docId 列表渲染成结果对象，并把命中写入 RecentHits（供 UI 跳转）。</summary>
        public static JObject RenderHits(IEnumerable<int> docIds, bool withTables)
        {
            var results = new JArray();
            var hits = new List<EntryRef>();
            foreach (int id in docIds)
            {
                var item = EncyclopediaContent.DataArray[id];
                results.Add(EncyclopediaText.Render(item, withTables));
                hits.Add(new EntryRef(EncyclopediaText.TitlePath(item), item.Key));
            }
            RecentHits = hits; // 引用整体替换，读侧无需加锁
            return new JObject { ["count"] = results.Count, ["results"] = results };
        }
    }
}
