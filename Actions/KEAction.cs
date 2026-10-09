using Pathfinder.Action;
using Pathfinder.Util.XML;

namespace KernelExtensions.Actions
{
    /// <summary>
    /// KE 的 Action 基类：在 Pathfinder 解析 XML 参数前，把属性表临时换成**大小写不敏感**的副本，
    /// 使 <c>[XMLStorage]</c> 字段名不受大小写影响（作者写 <c>delay=</c> 也能命中 <c>Delay</c>）。
    ///
    /// 背景：Pathfinder 的 <c>XMLStorageAttribute</c> 按字段名**精确匹配** <c>info.Attributes</c>
    /// （<c>EventReader</c> 构造的是默认 Ordinal 字典），大小写不一致会**静默失效**（读不到、不报错）。
    ///
    /// 实现要点（最小外溢）：
    ///   · 只替换**本次 LoadFromXml 期间**的字典，finally 中**立即还原**原对象——
    ///     ElementInfo 后续若被用于序列化/遍历（如 ToString/WriteToXML），仍是原字典；
    ///   · 副本内容与原字典完全一致，唯一差异是比较器；
    ///   · 用索引器赋值而非 Add，XML 中同时出现 <c>Delay</c> 与 <c>delay</c> 时后者覆盖，不抛异常。
    ///
    /// ⚠️ 大小写不敏感后，XML 里同时写两种大小写的同一属性时，**无法预测哪一个生效**（取决于枚举顺序），
    /// 作者不应这样写。
    /// </summary>
    public abstract class KEAction : DelayablePathfinderAction
    {
        public override void LoadFromXml(ElementInfo info)
        {
            if (info?.Attributes == null)
            {
                base.LoadFromXml(info);
                return;
            }

            var original = info.Attributes;
            var caseInsensitive = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in original) caseInsensitive[kv.Key] = kv.Value;

            info.Attributes = caseInsensitive;
            try
            {
                base.LoadFromXml(info);
            }
            finally
            {
                info.Attributes = original;
            }
        }
    }
}
