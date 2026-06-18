using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace TaiwuClaw
{
    /// <summary>
    /// 极简百晓册查询 harness 的 MOD 入口。
    /// 加载器规则：每个 DLL 取第一个 public 且继承 TaiwuRemakePlugin 的类型作为入口，
    /// 类上必须带 [PluginConfig(name, creatorId, version)] 特性。
    /// </summary>
    [PluginConfig("TaiwuClaw", "ultism", "0.1.0")]
    public class ModEntry : TaiwuRemakePlugin
    {
        public override void Initialize()
        {
            // 阶段一：仅验证加载链路。后续在此挂接百晓册全文查询。
            Debug.Log("[TaiwuClaw] Initialized — 百晓册查询 harness 已加载。");
        }

        public override void Dispose()
        {
            Debug.Log("[TaiwuClaw] Disposed.");
        }
    }
}
