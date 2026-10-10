using System.Xml;
using System.Xml.Serialization;
using Hacknet;
using KernelExtensions.Utilities;

namespace KernelExtensions.Configs
{
    /// <summary>
    /// 一个**动作点**（action hook）。
    ///
    /// <para><b>内联</b>（推荐 —— 逻辑与配置放在一起，不用为一两行动作单独建文件）：</para>
    /// <code>
    /// &lt;OnStart&gt;
    ///     &lt;Actions&gt;
    ///         &lt;TerminalWrite Text="starting..." /&gt;
    ///     &lt;/Actions&gt;
    /// &lt;/OnStart&gt;
    /// </code>
    ///
    /// <para><b>路径</b>（引用一个动作文件，相对于扩展根）：</para>
    /// <code>
    /// &lt;OnStart&gt;Actions/sshStart.xml&lt;/OnStart&gt;
    /// </code>
    ///
    /// <para>
    /// 内联内容原样交给 Pathfinder 的动作装载器，写法与动作文件**完全一致**：
    /// <list type="bullet">
    ///   <item><c>&lt;Actions&gt;</c> —— 列表内的动作**立即**逐个执行；</item>
    ///   <item><c>&lt;ConditionalActions&gt;</c> —— 交给 <c>os.ConditionalActions</c>，
    ///     由其中的条件决定何时执行（<c>&lt;Instantly&gt;</c> 加载后立即触发，
    ///     <c>&lt;OnConnect&gt;</c> / <c>&lt;HasFlags&gt;</c> 等等待条件满足）。</item>
    /// </list>
    /// 两者可混用，也可与路径写法同时使用（先文件、后内联）。
    /// </para>
    ///
    /// <para>
    /// ⚠️ 实现要点：<c>[XmlText]</c> 接**纯文本**（= 路径）、<c>[XmlAnyElement]</c> 接**子元素**（= 内联）。
    /// 两者分工明确 —— <c>[XmlAnyElement]</c> **只捕获元素、不捕获文本**（实测：纯文本时它整个为 null，
    /// 路径会被静默丢掉），所以文本必须单独用 <c>[XmlText]</c> 接。
    /// 该组合已用真实的 <c>ActionHook</c> 类验证：独立反序列化与**作为配置类的嵌套属性**
    /// 两种情况都正确（含内联、纯路径、路径+内联、并列双容器）。
    /// </para>
    /// </summary>
    [XmlType("ActionHook")]
    public class ActionHook
    {
        /// <summary>
        /// 钩子元素里的**纯文本** —— 视为动作文件路径（相对于扩展根）。
        /// </summary>
        [XmlText]
        public string FilePath { get; set; }

        /// <summary>
        /// 钩子元素里的**内联动作元素**（<c>&lt;Actions&gt;</c> / <c>&lt;ConditionalActions&gt;</c> 等），
        /// 原样保留、不做反序列化。
        /// </summary>
        [XmlAnyElement]
        public XmlElement[] InlineActions { get; set; }

        /// <summary>
        /// 内联内容（元素部分，拼成 XML 文本）；没有内联元素时返回 null。
        /// </summary>
        public string InlineXml
        {
            get
            {
                if (InlineActions == null || InlineActions.Length == 0) return null;

                var sb = new System.Text.StringBuilder();
                foreach (XmlElement e in InlineActions) sb.Append(e.OuterXml);
                return sb.Length > 0 ? sb.ToString() : null;
            }
        }

        /// <summary>是否有任何内容（空元素 / 纯空白 = 没配这个钩子）。</summary>
        public bool HasContent
            => !string.IsNullOrWhiteSpace(FilePath) || (InlineActions != null && InlineActions.Length > 0);

        /// <summary>
        /// 执行本钩子：先文件（若有）再内联（若有），两者都执行。
        ///
        /// <para>
        /// ⚠️ 整个方法兜底：调用方位于 <c>OS.Update</c> 驱动的流程里，异常冒出去会被上层吞掉、
        /// <b>连日志都不留</b>。这里连同属性读取一起包住，保证任何失败都留下可查的记录。
        /// </para>
        /// </summary>
        /// <param name="os">当前 OS。</param>
        /// <param name="source">日志定位用，形如 <c>MyConfig/ssh</c>。</param>
        /// <param name="point">日志定位用，动作点名（如 <c>OnComplete</c>）。</param>
        public void Execute(OS os, string source, string point)
        {
            if (os == null) return;

            try
            {
                if (!HasContent)
                {
                    // 记 Debug 而不是静默返回：钩子是否被识别是排查的关键线索
                    KELog.Debug($"[ActionHook] '{source}:<{point}>' has no content, skipped.");
                    return;
                }

                string path = FilePath?.Trim();
                if (!string.IsNullOrEmpty(path))
                {
                    KELog.Debug($"[ActionHook] '{source}:<{point}>' file = '{path}'");
                    ActionHelper.ExecuteFile(os, path, $"{source}:<{point}>");
                }

                string inline = InlineXml;
                if (inline != null)
                {
                    KELog.Debug($"[ActionHook] '{source}:<{point}>' inline = {inline}");
                    ActionHelper.ExecuteInline(os, inline, $"{source}:<{point}>");
                }
            }
            catch (Exception e)
            {
                KELog.Error($"[ActionHook] '{source}:<{point}>' aborted: {e}");
            }
        }
    }
}
