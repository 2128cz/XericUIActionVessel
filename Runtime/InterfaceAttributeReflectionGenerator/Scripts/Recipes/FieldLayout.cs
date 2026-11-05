using System;
using System.Collections.Generic;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 一次装配的决策：给定配方结果 + 预制体上真实存在的槽位/输入元素，
    /// 算出"该开哪些、该关哪些、哪里对不上"。
    ///
    /// 单独拎出来是因为这一步最容易静默错：槽位改名、元素少一个、配方多要一块——
    /// 都可能"什么都没报错，界面少一块"。所以任何对不上都必须进 problems，
    /// 由调用方决定是响亮地报错还是退回空白，绝不悄悄少装。
    /// </summary>
    public static class FieldLayout
    {
        /// <summary>槽位在预制体上的名字：就是零件名（小写连字符），输入元素挂在 "input" 之下。</summary>
        public static string SlotOf(RecipePart part)
        {
            switch (part)
            {
                case RecipePart.Label: return "label";
                case RecipePart.RequiredMark: return "required-mark";
                case RecipePart.Note: return "note";
                case RecipePart.Icon: return "icon";
                case RecipePart.Legend: return "legend";
                case RecipePart.Input: return "input";
                case RecipePart.Error: return "error";
                default: return part.ToString().ToLowerInvariant();
            }
        }

        /// <summary>
        /// 返回值只回答"能不能装"：缺零件槽、缺输入元素、压根没槽 —— 不能装。
        /// 缺一个装饰件（必填标记）仍然能装，但一定进 problems：
        /// 所以调用方两个都要看，不能只信 bool。
        /// </summary>
        public static bool Resolve(FieldPlan plan, IEnumerable<string> slotNames, IEnumerable<string> elementNames,
                                   out List<string> activate, out List<string> problems)
        {
            activate = new List<string>();
            problems = new List<string>();
            if (plan == null) { problems.Add("no plan given"); return false; }

            var slots = new HashSet<string>(StringComparer.Ordinal);
            if (slotNames != null) foreach (var s in slotNames) if (!string.IsNullOrEmpty(s)) slots.Add(s);
            var elements = new HashSet<string>(StringComparer.Ordinal);
            if (elementNames != null) foreach (var e in elementNames) if (!string.IsNullOrEmpty(e)) elements.Add(e);

            if (slots.Count == 0) { problems.Add("the field prefab exposes no slots at all (nothing to assemble into)"); return false; }

            var ok = true;
            for (var i = 0; i < plan.parts.Count; i++)
            {
                var slot = SlotOf(plan.parts[i]);
                if (!slots.Contains(slot))
                {
                    problems.Add("recipe wants part \"" + slot + "\" (" + plan.source + ") but the prefab has no such slot");
                    ok = false;
                    continue;
                }
                activate.Add(slot);
            }

            // 必填没标出来是"看起来能空着提交"，属于界面说谎：要么有槽，要么报问题
            if (plan.required && slots.Contains(SlotOf(RecipePart.RequiredMark)) && !activate.Contains(SlotOf(RecipePart.RequiredMark)))
                activate.Add(SlotOf(RecipePart.RequiredMark));
            else if (plan.required && !slots.Contains(SlotOf(RecipePart.RequiredMark)))
                problems.Add("field is required but the prefab has no \"required-mark\" slot to say so");

            if (!activate.Contains(SlotOf(RecipePart.Error)) && slots.Contains(SlotOf(RecipePart.Error)))
                activate.Add(SlotOf(RecipePart.Error));   // 错误位常驻（初始为空），否则校验失败时没地方显示

            if (!string.IsNullOrEmpty(plan.inputElement))
            {
                if (!activate.Contains(SlotOf(RecipePart.Input)))
                {
                    problems.Add("the recipe names input element \"" + plan.inputElement + "\" but no Input part is planned");
                    ok = false;
                }
                else if (elements.Count == 0)
                {
                    problems.Add("the \"input\" slot has no element variants to choose from");
                    ok = false;
                }
                else if (!elements.Contains(plan.inputElement))
                {
                    problems.Add("input element \"" + plan.inputElement + "\" is not on the prefab (there: " +
                                 string.Join(", ", Sorted(elements).ToArray()) + ")");
                    ok = false;
                }
                else activate.Add("input/" + plan.inputElement);
            }

            return ok;
        }

        static List<string> Sorted(HashSet<string> set)
        {
            var list = new List<string>(set);
            list.Sort(StringComparer.Ordinal);
            return list;
        }
    }
}
