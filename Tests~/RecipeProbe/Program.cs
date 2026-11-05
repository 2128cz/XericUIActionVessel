// 字段配方表的匹配/合并门：纯逻辑，不启 Unity，直接编包里的 Runtime/Form/FieldRecipeSet.cs。
// 判的是"最具体的赢 / 没配方就说不匹配 / 逐字段例外只覆盖写了的项"，不是画面。
using System;
using System.Collections.Generic;
using XericUI.ReflectionUIGenerator;

internal static class Program
{
    static int failures;
    static int checks;

    static void Check(bool ok, string what)
    {
        checks++;
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + what);
        if (!ok) failures++;
    }

    static FieldRecipe Recipe(string type, params RecipePart[] parts)
    {
        var r = new FieldRecipe { typeName = type };
        r.parts.Clear();
        r.parts.AddRange(parts);
        return r;
    }

    static int Main()
    {
        Console.WriteLine("== field recipe probe ==");
        var set = new FieldRecipeSet();
        set.recipes.Add(Recipe("UnityEngine.Vector2", RecipePart.Label, RecipePart.Input));
        set.recipes.Add(Recipe("Single", RecipePart.Label, RecipePart.Input, RecipePart.Note, RecipePart.Legend));
        var single = set.recipes[1];
        single.inputElement = "slider";
        single.noteWhen = "onHover";
        var overrideMax = new FieldOverride { fieldName = "maxSize", hasInputOverride = true, inputOverride = "input-field-numeric" };
        single.overrides.Add(overrideMax);
        single.overrides.Add(new FieldOverride
        {
            fieldName = "enabled", hasRequired = true, required = true, requiredMessage = "必须选",
            addParts = true, added = new List<RecipePart> { RecipePart.RequiredMark },
            removeParts = true, removed = new List<RecipePart> { RecipePart.Legend }
        });

        FieldPlan plan;
        string why;
        Check(set.TryPlan("UnityEngine.Vector2", "size", out plan, out why)
              && plan.parts.Count == 2 && plan.inputElement == "input-field",
              "exact type name matches and keeps the recipe's parts in order");

        set.recipes.Add(Recipe("Color", RecipePart.Label, RecipePart.Input));
        Check(set.TryPlan("UnityEngine.Color", "tint", out plan, out why) && plan.source == "recipe:Color",
              "a bare recipe name matches the tail of any namespace's type");
        Check(!set.TryPlan("My.Ns.Vector2", "size", out plan, out why) && why.Contains("no recipe"),
              "a namespaced recipe does NOT match a different type with the same tail");

        var specificity = new FieldRecipeSet();
        specificity.recipes.Add(Recipe("Vector2", RecipePart.Label, RecipePart.Input));
        specificity.recipes.Add(Recipe("UnityEngine.Vector2", RecipePart.Label, RecipePart.Note, RecipePart.Input));
        Check(specificity.TryPlan("UnityEngine.Vector2", "v", out plan, out why)
              && plan.source == "recipe:UnityEngine.Vector2" && plan.parts.Contains(RecipePart.Note),
              "the most specific recipe wins (full name beats bare name)");

        Check(!set.TryPlan("UnityEngine.Quaternion", "rot", out plan, out why) && why.Contains("no recipe"),
              "no recipe is reported as missing, not resolved to a guess: " + why);

        var ambiguous = new FieldRecipeSet();
        ambiguous.recipes.Add(Recipe("Vector2", RecipePart.Label, RecipePart.Input));
        ambiguous.recipes.Add(Recipe("Vector2", RecipePart.Label, RecipePart.Note, RecipePart.Input));
        Check(!ambiguous.TryPlan("Baz.Vector2", "v", out plan, out why) && why.Contains("ambiguous"),
              "two equally specific recipes are refused with both names: " + why);

        Check(set.TryPlan("Single", "volume", out plan, out why)
              && plan.inputElement == "slider" && plan.noteWhen == "onHover"
              && plan.parts.Contains(RecipePart.Legend) && !plan.parts.Contains(RecipePart.RequiredMark)
              && plan.source == "recipe:Single",
              "without an override the recipe decides everything");

        Check(set.TryPlan("Single", "maxSize", out plan, out why)
              && plan.inputElement == "input-field-numeric" && plan.noteWhen == "onHover"
              && plan.source.EndsWith("+ override:maxSize"),
              "an override replaces only what it writes");

        Check(set.TryPlan("Single", "enabled", out plan, out why)
              && plan.required && plan.requiredMessage == "必须选"
              && plan.parts.Contains(RecipePart.RequiredMark) && !plan.parts.Contains(RecipePart.Legend),
              "an override can add and remove parts (required mark in, legend out)");

        var before = plan.parts.Count;
        Check(set.TryPlan("Single", "enabled", out plan, out why) && plan.parts.Count == before
              && plan.parts.FindAll(p => p == RecipePart.RequiredMark).Count == 1,
              "re-planning the same field never duplicates a part (idempotent)");

        var broken = new FieldRecipeSet();
        broken.recipes.Add(Recipe("", RecipePart.Label, RecipePart.Input));
        broken.recipes.Add(Recipe("Single"));
        broken.recipes.Add(Recipe("Single", RecipePart.Label, RecipePart.Input));
        var bad = broken.Validate();
        Check(bad.Count == 3 && bad[0].Contains("empty typeName") && bad[1].Contains("no parts") && bad[2].Contains("duplicate"),
              "Validate names every table defect (empty type / no parts / duplicate): " + string.Join(" | ", bad.ToArray()));

        var keyless = new FieldRecipeSet();
        var withKeyless = Recipe("Single", RecipePart.Label, RecipePart.Input);
        withKeyless.overrides.Add(new FieldOverride { fieldName = "" });
        keyless.recipes.Add(withKeyless);
        Check(keyless.Validate().Count == 1 && keyless.Validate()[0].Contains("no fieldName"),
              "an override without a fieldName is a table error, not a silent no-op");

        // ---- 装配决策：槽位/元素对不上一句都不能少说 ----
        var simple = new FieldPlan { source = "recipe:Single" };
        simple.parts.Add(RecipePart.Label);
        simple.parts.Add(RecipePart.Input);
        simple.inputElement = "input-field";
        List<string> on;
        List<string> problems;
        Check(FieldLayout.Resolve(simple, new[] { "label", "input" }, new[] { "input-field", "slider" }, out on, out problems)
              && on.Contains("label") && on.Contains("input") && on.Contains("input/input-field") && problems.Count == 0,
              "a well-formed plan activates exactly its slots plus the chosen element");

        Check(!FieldLayout.Resolve(simple, new[] { "label" }, new[] { "input-field" }, out on, out problems)
              && problems[0].Contains("no such slot"),
              "a part the prefab has no slot for is a problem, not a silently dropped widget: " + problems[0]);

        var wrongElement = new FieldPlan { source = "recipe:Single" };
        wrongElement.parts.Add(RecipePart.Input);
        wrongElement.inputElement = "enum-dropdown";
        Check(!FieldLayout.Resolve(wrongElement, new[] { "input" }, new[] { "input-field", "slider" }, out on, out problems)
              && problems[0].Contains("enum-dropdown") && problems[0].Contains("slider"),
              "an unknown input element names itself AND lists what the prefab really has: " + problems[0]);

        var needsMark = new FieldPlan { source = "recipe:Single", required = true, requiredMessage = "必须填" };
        needsMark.parts.Add(RecipePart.Label);
        var markOk = FieldLayout.Resolve(needsMark, new[] { "label" }, null, out on, out problems);
        Check(markOk && problems[0].Contains("required-mark") && on.Contains("label"),
              "a required field with no place to say so still assembles, but reports it (ok=" + markOk + "): " + problems[0]);

        var withMark = new FieldPlan { source = "recipe:Single", required = true };
        withMark.parts.Add(RecipePart.Label);
        Check(FieldLayout.Resolve(withMark, new[] { "label", "required-mark", "error" }, null, out on, out problems)
              && on.Contains("required-mark") && on.Contains("error"),
              "required-mark joins the active list on its own, and the error slot is always ready");

        Check(!FieldLayout.Resolve(withMark, new string[0], null, out on, out problems)
              && problems[0].Contains("no slots at all"),
              "a prefab exposing nothing is called out (that is a broken prefab, not an empty layout)");

        var elementWithoutInput = new FieldPlan { source = "recipe:Single", inputElement = "slider" };
        elementWithoutInput.parts.Add(RecipePart.Label);
        Check(!FieldLayout.Resolve(elementWithoutInput, new[] { "label" }, new[] { "slider" }, out on, out problems)
              && problems[0].Contains("no Input part"),
              "naming an input element without planning the Input slot is refused");

        Console.WriteLine();
        if (failures == 0)
        {
            Console.WriteLine("VERDICT RECIPE-PROBE PASS (" + checks + "/" + checks + ")");
            return 0;
        }
        Console.WriteLine("VERDICT RECIPE-PROBE FAIL (" + failures + "/" + checks + " failed)");
        return 1;
    }
}
