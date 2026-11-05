using System;
using System.Collections.Generic;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>界面上一块可拼的零件。配方决定拼哪几块、按什么顺序摆。</summary>
    public enum RecipePart { Label = 0, RequiredMark = 1, Note = 2, Icon = 3, Legend = 4, Input = 5, Error = 6 }

    /// <summary>某个字段的例外：只覆盖写了的那几项。</summary>
    [Serializable]
    public sealed class FieldOverride
    {
        public string fieldName = "";
        public bool hasInputOverride;
        public string inputOverride = "";
        public bool hasNoteWhen;
        public string noteWhen = "";
        public bool hasFriendlyName;
        public string friendlyName = "";
        public bool hasRequired;
        public bool required;
        public string requiredMessage = "";
        public bool removeParts;
        public List<RecipePart> removed = new List<RecipePart>();
        public bool addParts;
        public List<RecipePart> added = new List<RecipePart>();
    }

    /// <summary>一个类型的配方：输入元素、零件清单、必填与注释规则，外加逐字段例外。</summary>
    [Serializable]
    public sealed class FieldRecipe
    {
        /// <summary>匹配的类型全名（如 "UnityEngine.Vector2"）；也接受后缀写法 "Vector2"。</summary>
        public string typeName = "";
        public string inputElement = "input-field";
        public string noteWhen = "";
        public bool required;
        public string requiredMessage = "此项必填";
        public string friendlyName = "";
        public List<RecipePart> parts = new List<RecipePart> { RecipePart.Label, RecipePart.Input };
        public List<FieldOverride> overrides = new List<FieldOverride>();
    }

    /// <summary>一次解析的结果：拼哪些零件、用什么输入元素、必填规则、以及"是谁说了算"。</summary>
    public sealed class FieldPlan
    {
        public readonly List<RecipePart> parts = new List<RecipePart>();
        public string inputElement = "";
        public string noteWhen = "";
        public string friendlyName = "";
        public bool required;
        public string requiredMessage = "";
        /// <summary>"recipe:UnityEngine.Vector2" / "override:maxSize" / "" —— 每条决定都能回溯到来源。</summary>
        public string source = "";
    }

    /// <summary>
    /// 配方表的匹配与合并（纯逻辑，不引 Unity，可离线证）：
    ///
    /// 预制体只放**零件**，配方决定**拼哪几块**，逐字段例外写在 override 里——
    /// 这样"一个字段看着很小、其实要兼容所有场景"不再堆进同一个预制体。
    ///
    /// 两条硬规矩：① 匹配是"最具体的赢"（全名 &gt; 后缀，多条同长则报歧义，不随便挑一条）；
    /// ② 没匹配到就说"没有配方"，绝不回退到一个默认配方——凭空长出来的界面比空白更难查。
    /// </summary>
    public sealed class FieldRecipeSet
    {
        public readonly List<FieldRecipe> recipes = new List<FieldRecipe>();

        public bool TryPlan(string typeName, string fieldName, out FieldPlan plan, out string reason)
        {
            plan = null;
            reason = "";
            if (string.IsNullOrEmpty(typeName))
            {
                reason = "no type given";
                return false;
            }

            var exact = new List<FieldRecipe>();
            var suffix = new List<FieldRecipe>();
            for (var i = 0; i < recipes.Count; i++)
            {
                var r = recipes[i];
                if (r == null || string.IsNullOrEmpty(r.typeName)) continue;
                // 写了命名空间 = 只认这个类型（"My.Ns.Vector2" 不是 "UnityEngine.Vector2"）；
                // 只写裸名 = 按尾段匹配任意命名空间下的同名类型。两种具体度不同，不混在一起比。
                if (r.typeName.IndexOf('.') >= 0)
                {
                    if (string.Equals(r.typeName, typeName, StringComparison.Ordinal)) exact.Add(r);
                }
                else if (string.Equals(TypeNameTail(typeName), r.typeName, StringComparison.Ordinal)) suffix.Add(r);
            }

            var picked = exact.Count > 0 ? exact : suffix;
            if (picked.Count == 0)
            {
                reason = "no recipe for type \"" + typeName + "\" (" + recipes.Count + " recipe(s) in the table; " +
                         "add one instead of falling back to a guess)";
                return false;
            }
            if (picked.Count > 1)
            {
                var names = new List<string>();
                for (var i = 0; i < picked.Count; i++) names.Add(picked[i].typeName);
                names.Sort(StringComparer.Ordinal);
                reason = "ambiguous recipe for \"" + typeName + "\": " + string.Join(", ", names.ToArray()) +
                         " (fix the table: Validate flags duplicates; picking one silently would render the wrong widget)";
                return false;
            }

            var recipe = picked[0];
            plan = new FieldPlan
            {
                inputElement = recipe.inputElement,
                noteWhen = recipe.noteWhen,
                friendlyName = recipe.friendlyName,
                required = recipe.required,
                requiredMessage = recipe.requiredMessage,
                source = "recipe:" + recipe.typeName
            };
            for (var i = 0; i < recipe.parts.Count; i++) if (!plan.parts.Contains(recipe.parts[i])) plan.parts.Add(recipe.parts[i]);

            FieldOverride hit = null;
            for (var i = 0; i < recipe.overrides.Count; i++)
            {
                var o = recipe.overrides[i];
                if (o != null && string.Equals(o.fieldName, fieldName, StringComparison.Ordinal)) { hit = o; break; }
            }
            if (hit == null) return true;

            plan.source += " + override:" + hit.fieldName;
            if (hit.hasInputOverride) plan.inputElement = hit.inputOverride;
            if (hit.hasNoteWhen) plan.noteWhen = hit.noteWhen;
            if (hit.hasFriendlyName) plan.friendlyName = hit.friendlyName;
            if (hit.hasRequired) { plan.required = hit.required; plan.requiredMessage = hit.requiredMessage; }
            if (hit.removeParts) for (var i = 0; i < hit.removed.Count; i++) plan.parts.Remove(hit.removed[i]);
            if (hit.addParts) for (var i = 0; i < hit.added.Count; i++) if (!plan.parts.Contains(hit.added[i])) plan.parts.Add(hit.added[i]);
            return true;
        }

        /// <summary>表自身的毛病要能一次性列出来：空类型、重复类型、没零件的配方。</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < recipes.Count; i++)
            {
                var r = recipes[i];
                if (r == null) { problems.Add("recipe " + i + " is null"); continue; }
                if (string.IsNullOrEmpty(r.typeName)) problems.Add("recipe " + i + " has an empty typeName");
                else if (!seen.Add(r.typeName)) problems.Add("duplicate recipe for \"" + r.typeName + "\"");
                if (r.parts.Count == 0) problems.Add("\"" + r.typeName + "\" has no parts (it would render nothing)");
                for (var p = 0; p < r.parts.Count; p++)
                    if (!Enum.IsDefined(typeof(RecipePart), r.parts[p])) problems.Add("\"" + r.typeName + "\" part index " + p + " is not a RecipePart");
                for (var o = 0; o < r.overrides.Count; o++)
                    if (r.overrides[o] != null && string.IsNullOrEmpty(r.overrides[o].fieldName))
                        problems.Add("\"" + r.typeName + "\" override index " + o + " has no fieldName");
            }
            return problems;
        }

        static string TypeNameTail(string fullName)
        {
            var cut = fullName.LastIndexOf('.');
            return cut < 0 ? fullName : fullName.Substring(cut + 1);
        }
    }
}
