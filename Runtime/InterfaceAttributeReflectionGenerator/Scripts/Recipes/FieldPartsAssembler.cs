using System;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>宿主库自己的 Unity 菜单位置（只此一处）。蓝图包的路径表不能被宿主反向引用，
    /// 所以这里不共享常量；check_menu_paths.sh 的字面量普查只扫 fork 与蓝图包两棵树。</summary>
    internal static class HostMenuRoots
    {
        public const string ComponentTable = "Xeric Library/Component/Table";
    }
    /// <summary>配方表资产：类型 → 配方（含逐字段例外）。匹配与合并的规矩在 <see cref="FieldRecipeSet"/>。</summary>
    [CreateAssetMenu(menuName = HostMenuRoots.ComponentTable + "/Field Recipe Table", fileName = "FieldRecipeTable")]
    public sealed class FieldRecipeTable : ScriptableObject
    {
        [SerializeField] FieldRecipeSet recipes = new FieldRecipeSet();

        public FieldRecipeSet Recipes { get { return recipes; } }

        public bool TryPlan(string typeName, string fieldName, out FieldPlan plan, out string reason)
        {
            return recipes.TryPlan(typeName, fieldName, out plan, out reason);
        }

        /// <summary>表自身的毛病（空类型/重复/没零件/没 fieldName 的 override）。</summary>
        public List<string> Validate() { return recipes.Validate(); }
    }

    /// <summary>
    /// 一个字段预制体上的零件槽位。预制体只放零件（label/note/icon/legend/input/error/required-mark），
    /// input 之下再放各类型的输入元素（input-field/slider/enum-dropdown…），运行时按配方激活。
    ///
    /// 所以"某字段要多一个小字注释"是改配方，不是改预制体；预制体少了那块零件才是预制体的事，
    /// 而它会作为 problem 报出来，不是安静地少装一块。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu(HostMenuRoots.ComponentTable + "/Field Parts Assembler")]
    public sealed class FieldPartsAssembler : MonoBehaviour
    {

        [Serializable]
        struct SlotRef
        {
            public string slot;
            public GameObject go;
        }

        [SerializeField] List<SlotRef> slots = new List<SlotRef>();
        [SerializeField] List<GameObject> inputElements = new List<GameObject>();

        public int SlotCount { get { return slots.Count; } }
        public int ElementCount { get { return inputElements.Count; } }

        /// <summary>按约定的槽位名收集一层子物体（省掉一个个手拖）。</summary>
        public int Collect()
        {
            slots.Clear();
            inputElements.Clear();
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                var known = SlotName(child.name);
                if (known == null) continue;
                slots.Add(new SlotRef { slot = known, go = child.gameObject });
                if (known != "input") continue;
                for (var j = 0; j < child.childCount; j++)
                {
                    var element = child.GetChild(j);
                    if (string.IsNullOrEmpty(SlotName(element.name)) && element.name != "input")
                        inputElements.Add(element.gameObject);
                }
            }
            return slots.Count;
        }

        /// <summary>Activate what the plan asks for, hide the rest. Problems are returned, never swallowed.</summary>
        public bool Apply(FieldPlan plan, out List<string> problems)
        {
            var slotNames = new List<string>(slots.Count);
            for (var i = 0; i < slots.Count; i++) slotNames.Add(slots[i].slot);
            var elementNames = new List<string>(inputElements.Count);
            for (var i = 0; i < inputElements.Count; i++)
                if (inputElements[i] != null) elementNames.Add(inputElements[i].name);

            List<string> activate;
            var ok = FieldLayout.Resolve(plan, slotNames, elementNames, out activate, out problems);

            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s.go == null) { problems.Add("slot \"" + s.slot + "\" points at nothing"); ok = false; continue; }
                var want = activate.Contains(s.slot);
                if (s.slot == "input") want = want && activate.Contains("input/" + (plan == null ? "" : plan.inputElement));
                s.go.SetActive(want);
            }
            for (var i = 0; i < inputElements.Count; i++)
            {
                var element = inputElements[i];
                if (element == null) continue;
                element.SetActive(activate.Contains("input/" + element.name));
            }
            return ok;
        }

        static string SlotName(string name)
        {
            switch (name)
            {
                case "label": case "required-mark": case "note": case "icon":
                case "legend": case "input": case "error": return name;
                default: return null;
            }
        }
    }
}
