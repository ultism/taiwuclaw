using System.Collections.Generic;

namespace TaiwuClaw.Core
{
    /// <summary>
    /// 代码技能基类：把一组 C# <see cref="IAgentAction"/> 打包成一个技能。
    /// 子类给 Id/Title/Summary/Instructions，并在 Actions 里列出本技能的能力即可。
    /// </summary>
    public abstract class CodeSkill : ISkill
    {
        public abstract string Id { get; }
        public abstract string Title { get; }
        public abstract string Summary { get; }

        /// <summary>默认用 Summary 当说明书；能力复杂时覆写给更详细的使用约定。</summary>
        public virtual string Instructions => Summary;

        public abstract IEnumerable<IAgentAction> Actions { get; }
    }
}
