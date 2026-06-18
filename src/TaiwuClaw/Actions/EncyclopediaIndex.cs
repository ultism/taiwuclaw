using System;
using System.Collections.Generic;
using System.Linq;
using Game.Views.Encyclopedia;
using UnityEngine;

namespace TaiwuClaw.Actions
{
    /// <summary>
    /// 百晓册 BM25 检索索引。中文按字 uni/bi-gram 分词，英数按词；
    /// 文档 = 标题路径 + 正文 + 引用表（见 EncyclopediaText.SearchBlob）。懒构建一次、缓存。
    /// </summary>
    internal static class EncyclopediaIndex
    {
        private struct Posting { public int Doc; public int Tf; }

        private const double K1 = 1.5;
        private const double B = 0.75;

        private static bool _built;
        private static int _n;
        private static double _avgLen;
        private static int[] _docLen;
        private static Dictionary<string, List<Posting>> _postings;

        public static void EnsureBuilt()
        {
            if (_built) return;
            if (EncyclopediaContent.DataArray.Count == 0) EncyclopediaDataProcessor.Init();

            var arr = EncyclopediaContent.DataArray;
            _n = arr.Count;
            _docLen = new int[_n];
            _postings = new Dictionary<string, List<Posting>>();
            long total = 0;

            for (int i = 0; i < _n; i++)
            {
                var counts = new Dictionary<string, int>();
                int len = 0;
                foreach (var tok in Tokenize(EncyclopediaText.SearchBlob(arr[i])))
                {
                    counts.TryGetValue(tok, out int c);
                    counts[tok] = c + 1;
                    len++;
                }
                _docLen[i] = len;
                total += len;
                foreach (var kv in counts)
                {
                    if (!_postings.TryGetValue(kv.Key, out var lst))
                    {
                        lst = new List<Posting>();
                        _postings[kv.Key] = lst;
                    }
                    lst.Add(new Posting { Doc = i, Tf = kv.Value });
                }
            }
            _avgLen = _n > 0 ? (double)total / _n : 1;
            _built = true;
            Debug.Log($"[TaiwuClaw] 百晓册索引完成：{_n} 条，{_postings.Count} 词项。");
        }

        /// <summary>BM25 检索，返回相关度 Top-N 的 DataArray 索引。</summary>
        public static List<int> Search(string query, int topN)
        {
            EnsureBuilt();
            var qTokens = new HashSet<string>(Tokenize(query));
            var scores = new Dictionary<int, double>();
            foreach (var t in qTokens)
            {
                if (!_postings.TryGetValue(t, out var lst)) continue;
                double idf = Math.Log(1.0 + (_n - lst.Count + 0.5) / (lst.Count + 0.5));
                foreach (var p in lst)
                {
                    double dl = _docLen[p.Doc];
                    double denom = p.Tf + K1 * (1 - B + B * dl / _avgLen);
                    double s = idf * (p.Tf * (K1 + 1)) / denom;
                    scores.TryGetValue(p.Doc, out double cur);
                    scores[p.Doc] = cur + s;
                }
            }
            return scores.OrderByDescending(kv => kv.Value).Take(topN).Select(kv => kv.Key).ToList();
        }

        // 中文：CJK run 的 unigram + 相邻 bigram；英数：连续 alnum 词（小写）。其余作分隔符。
        private static IEnumerable<string> Tokenize(string s)
        {
            if (string.IsNullOrEmpty(s)) yield break;
            int i = 0, n = s.Length;
            while (i < n)
            {
                char c = s[i];
                if (IsAsciiAlnum(c))
                {
                    int j = i + 1;
                    while (j < n && IsAsciiAlnum(s[j])) j++;
                    yield return s.Substring(i, j - i).ToLowerInvariant();
                    i = j;
                }
                else if (IsCjk(c))
                {
                    int j = i + 1;
                    while (j < n && IsCjk(s[j])) j++;
                    for (int k = i; k < j; k++) yield return s[k].ToString();
                    for (int k = i; k + 1 < j; k++) yield return s.Substring(k, 2);
                    i = j;
                }
                else i++;
            }
        }

        private static bool IsAsciiAlnum(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');

        private static bool IsCjk(char c) => c >= 0x4E00 && c <= 0x9FFF;
    }
}
