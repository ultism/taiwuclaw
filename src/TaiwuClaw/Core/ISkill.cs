using System.Collections.Generic;

namespace TaiwuClaw.Core
{
    /// <summary>
    /// 一个「技能」：一组相关 action + 一段说明书，作为<b>渐进披露</b>的最小单元。
    ///
    /// 动机：每个 action 的 input_schema 都要进每次请求的 tools 数组，能力一多上下文线性膨胀。
    /// 技能把相关能力打包，<b>默认关闭</b>——平时只有 Id/Summary 进上下文（系统提示里占一行目录），
    /// 模型调 open_skill(id) 后才回灌 Instructions 并把该技能的 Actions 激活进 tools。
    ///
    /// <b>来源无关</b>：当前实现是代码技能（<see cref="CodeSkill"/>，Actions 为真正的 C# 能力）。
    /// 将来的「磁盘 markdown playbook」（用户在 Skills/*/SKILL.md 投放的纯提示词）只要实现本接口、
    /// Actions 返回空、Instructions 给 markdown 正文，即可并入同一 open_skill 目录，模型无需区分来源。
    /// 见 SkillRegistry 顶部的 TODO(磁盘技能)。
    /// </summary>
    public interface ISkill
    {
        /// <summary>open_skill 的参数值。注意它是<b>参数值</b>不是工具名，故允许中文（工具名才限 ASCII）。</summary>
        string Id { get; }

        /// <summary>人类可读标题。</summary>
        string Title { get; }

        /// <summary>一句话用途，进常驻目录（system prompt）。务必简短——这是关闭态唯一的上下文成本。</summary>
        string Summary { get; }

        /// <summary>完整说明书。open 时作为 tool_result 回灌；可含使用约定、参数细节、注意事项。</summary>
        string Instructions { get; }

        /// <summary>open 后激活的工具。playbook 类技能可返回空集合（只注入说明、不加工具）。</summary>
        IEnumerable<IAgentAction> Actions { get; }
    }
}
