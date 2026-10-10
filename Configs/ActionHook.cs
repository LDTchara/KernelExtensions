using System.Xml;
using System.Xml.Serialization;
using Hacknet;
using KernelExtensions.Utilities;

namespace KernelExtensions.Configs
{
    /// <summary>
    /// 一个**动作点**（action hook）。
    ///
    /// <para>
    /// 两种写法都支持，且可以同时出现在同一个钩子里：
    /// </para>
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
    /// 内联内容原样交给 Pathfinder 的 <c>EventExecutor</c> 解析，因此写法与动作文件**完全一致**：
    /// <list type="bullet">
    ///   <item><c>&lt;Actions&gt;</c> —— 列表内的动作**立即**逐个执行；</item>
    ///   <item><c>&lt;ConditionalActions&gt;</c> —— 交给 <c>os.ConditionalActions</c>，
    ///     由其中的条件决定何时执行，例如 <c>&lt;Instantly&gt;</c> 在加载后立即触发、
    ///     <c>&lt;OnConnect&gt;</c> / <c>&lt;HasFlags&gt;</c> 等则等待条件满足。</item>
    /// </list>
    /// 两者可混用。
    /// </para>
    ///
    /// <para>
    /// ⚠️ 实现要点：用 <see cref="XmlAnyElementAttribute"/> 接住节点，**不做反序列化** ——
    /// 于是"元素内是纯文本路径"与"元素内是子元素"都能原样拿到。
    /// （若改成 <c>XElement</c> 属性，纯文本内容会因 XmlSerializer 把文本当 XML 解析而抛异常，
    /// 现有路径写法会全部失效。）
    /// </para>
    /// </summary>
    /// <summary>
    /// ⚠️ <see cref="XmlTypeAttribute"/> 是**必需**的，不是装饰：<c>XmlSerializer</c> 默认按
    /// .NET 类名决定元素名，而本类被用作 <c>OnStart</c> / <c>OnComplete</c> / <c>OnFail</c>
    /// 等属性的类型时，元素名取自**属性名**；若这里不显式声明，一旦类名被重命名，
    /// 序列化元数据会跟着变，而 XML 里的写法是作者的契约、不能跟着变。
    /// </summary>
    [XmlType("ActionHook")]
    public class ActionHook
    {
        /// <summary>
        /// 钩子元素里的**纯文本** —— 视为动作文件路径（相对于扩展根）。
        ///
        /// <para>
        /// 用 <see cref="XmlTextAttribute"/> 而不是 <c>XmlAnyElement</c> 接文本：
        /// <c>XmlAnyElement</c> **只捕获元素节点、不捕获文本**（实测：纯文本时它整个是 null，
        /// 路径会被静默丢掉）。两者分工明确 —— 文本走这里，元素走下面那个。
        /// </para>
        /// </summary>
        [XmlText]
        public string FilePath { get; set; }

        /// <summary>
        /// 钩子元素里的**内联动作元素**（<c>&lt;Actions&gt;</c> / <c>&lt;ConditionalActions&gt;</c> 等），
        /// 原样保留、不做反序列化。
        /// </summary>
        [XmlAnyElement]
        public XmlElement[] InlineActions { get; set; }

        /// <summary>是否有任何内容（空元素 / 纯空白 = 没配这个钩子）。</summary>
        public bool HasContent
        {
            get
            {
                bool hasPath = !string.IsNullOrWhiteSpace(FilePath)
                               || (InlineActions != null && InlineActions.Length > 0);
                return hasPath;
            }
        }

        /// <summary>内联内容（元素部分，拼成 XML 文本）；没有内联元素时返回 null。</summary>
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

        /// <summary>
        /// 执行本钩子：先文件（若有）再内联（若有），两者都执行。
        ///
        /// <para>
        /// ⚠️ <b>顺序是"先路径、后内联"</b>：两条路各自已有兜底，但把内联放后面更稳妥 ——
        /// 内联内容由作者直接写、出错概率更高，放在后面就不会挡住已经写好并验证过的动作文件。
        /// </para>
        /// </summary>
        /// <param name="os">当前 OS。</param>
        /// <param name="source">日志定位用，形如 <c>MyConfig/ssh</c>。</param>
        /// <param name="point">日志定位用，动作点名（如 <c>OnComplete</c>）。</param>
        public void Execute(OS os, string source, string point)
        {
            if (os == null || !HasContent) return;   // 作者没配这个钩子：正常情况，不记日志

            string path = FilePath?.Trim();
            if (!string.IsNullOrEmpty(path)) ActionHelper.ExecuteFile(os, path, $"{source}:<{point}>");

            string inline = InlineXml;
            if (inline != null) ActionHelper.ExecuteInline(os, inline, $"{source}:<{point}>");
        }
    }
}
