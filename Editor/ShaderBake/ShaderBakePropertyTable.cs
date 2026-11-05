using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace XericUI.ShaderBake.Editor
{
	/// <summary>
	/// 右侧的"材质字段属性表"：把 shader 声明的属性铺成一行行可编辑控件，改动直接写进那张
	/// （临时）材质并通知外部重画。
	///
	/// <para><b>行从模板克隆，不在代码里造控件</b>：结构在 BakeShaderPropertyRow.uxml，
	/// 这里只决定"这一行点亮哪个控件、其余收起来"。</para>
	///
	/// <para><b>2022.1 没有 GetPropertyDisplayName</b>，所以标签只能由属性名推（下划线去掉、
	/// 驼峰拆词），description 拿来当 tooltip 并用于识别 <c>[Toggle]</c> 与 <c>[Enum]</c> 两类属性。
	/// 这两个识别都是"猜形状"，猜不中就退回普通数字框——功能不丢，只是少了个勾。</para>
	/// </summary>
	internal sealed class ShaderBakePropertyTable
	{
		/// <summary> 一行的所有控件；按类型只挂出一个 </summary>
		private sealed class Row
		{
			public VisualElement root;
			public Label label;
			public FloatField floatValue;
			public IntegerField intValue;
			public Toggle toggle;
			public DropdownField enumField;
			public ColorField colorField;
			public ObjectField textureField;
			public VisualElement rangeGroup;
			public Slider slider;
			public FloatField rangeValue;
			public VisualElement vectorGroup;
			public FloatField[] vectorParts;

			public int propIndex;
			public string propName;
			public ShaderPropertyType propType;
			public string keyword;
			public float rangeMin;
			public float rangeMax;

			/// <summary> 程序性赋值（RefreshValues）期间压掉回调，免得一次刷新触发一串重画 </summary>
			public bool updating;
		}

		private readonly VisualElement m_Container;
		private readonly VisualTreeAsset m_Template;
		private readonly List<Row> m_Rows = new List<Row>();

		private Shader m_Shader;
		private Material m_Material;

		/// <summary> 任一属性被改动（外部据此重画） </summary>
		public event Action changed;

		/// <summary> 行数（状态栏用来写"共 N 项属性"） </summary>
		public int rowCount { get { return m_Rows.Count; } }

		public ShaderBakePropertyTable(VisualElement root, VisualTreeAsset rowTemplate)
		{
			m_Container = root.Q<VisualElement>("props-rows");
			m_Template = rowTemplate;
			if (m_Container == null)
				throw new InvalidOperationException("BakeShaderWindow.uxml 缺少 props-rows 容器");
		}

		/// <summary>
		/// 换目标。shader 变了重建行，只有材质变了就重新取值——重建一次要克隆几十行，
		/// 不该在每次点导出的时候发生。
		/// </summary>
		public void SetTarget(Shader shader, Material material)
		{
			m_Material = material;
			if (shader != m_Shader)
			{
				m_Shader = shader;
				Rebuild();
			}
			else
			{
				RefreshValues();
			}
		}

		public void Rebuild()
		{
			m_Rows.Clear();
			m_Container.Clear();
			if (m_Shader == null || m_Template == null)
				return;

			int count = ShaderUtil.GetPropertyCount(m_Shader);
			for (int i = 0; i < count; i++)
			{
				if (ShaderUtil.IsShaderPropertyHidden(m_Shader, i))
					continue;
				Row row = CreateRow(i);
				if (row != null)
				{
					m_Rows.Add(row);
					m_Container.Add(row.root);
				}
			}
			RefreshValues();
		}

		/// <summary> 把材质当前的值读回界面（写回/丢弃改动、或外部改了材质之后用） </summary>
		public void RefreshValues()
		{
			foreach (Row row in m_Rows)
			{
				if (m_Material == null || !m_Material.HasProperty(row.propName))
				{
					row.root.style.display = DisplayStyle.None;
					continue;
				}
				row.root.style.display = DisplayStyle.Flex;
				row.updating = true;
				try
				{
					FillFromMaterial(row);
				}
				finally
				{
					row.updating = false;
				}
			}
		}

		public void Dispose()
		{
			m_Rows.Clear();
			m_Container.Clear();
		}

		// ── 建行 ────────────────────────────────────────────────

		private Row CreateRow(int propIndex)
		{
			VisualElement host = new VisualElement();
			m_Template.CloneTree(host);
			if (host.childCount == 0)
				return null;
			VisualElement root = host[0];
			root.RemoveFromHierarchy();

			Shader shader = m_Shader;
			string name = ShaderUtil.GetPropertyName(shader, propIndex);
			// 两个同名枚举分属编辑器与运行时命名空间，但取值顺序一致（Color..Int..Constants）
			var type = (ShaderPropertyType)(int)ShaderUtil.GetPropertyType(shader, propIndex);
			string description = ShaderUtil.GetPropertyDescription(shader, propIndex);

			var row = new Row
			{
				root = root,
				label = root.Q<Label>("prop-label"),
				floatValue = root.Q<FloatField>("prop-float"),
				intValue = root.Q<IntegerField>("prop-int"),
				toggle = root.Q<Toggle>("prop-toggle"),
				enumField = root.Q<DropdownField>("prop-enum"),
				colorField = root.Q<ColorField>("prop-color"),
				textureField = root.Q<ObjectField>("prop-texture"),
				rangeGroup = root.Q<VisualElement>("prop-range"),
				slider = root.Q<Slider>("prop-slider"),
				rangeValue = root.Q<FloatField>("prop-range-value"),
				vectorGroup = root.Q<VisualElement>("prop-vector"),
				propIndex = propIndex,
				propName = name,
				propType = type,
				keyword = ReadKeyword(type, description)
			};
			row.vectorParts = new[]
			{
				root.Q<FloatField>("prop-vec-x"),
				root.Q<FloatField>("prop-vec-y"),
				root.Q<FloatField>("prop-vec-z"),
				root.Q<FloatField>("prop-vec-w")
			};

			string displayName = ReadDisplayName(shader, propIndex, name);
			if (row.label != null)
			{
				row.label.text = displayName;
				row.label.tooltip = name + (string.IsNullOrEmpty(description) ? string.Empty : "\n" + description);
			}
			root.tooltip = row.label != null ? row.label.tooltip : name;

			// 先把所有候选控件收起来，下面按类型只放一个出来
			HideAll(row);

			switch (type)
			{
				case ShaderPropertyType.Range:
					row.rangeMin = ShaderUtil.GetRangeLimits(shader, propIndex, 0);
					row.rangeMax = ShaderUtil.GetRangeLimits(shader, propIndex, 1);
					Show(row.rangeGroup);
					WireSlider(row);
					WireRangeValue(row);
					break;

				case ShaderPropertyType.Float:
					if (row.keyword != null)
					{
						Show(row.toggle);
						WireToggle(row);
					}
					else
					{
						Show(row.floatValue);
						WireFloat(row);
					}
					break;

				case ShaderPropertyType.Int:
					List<string> choices = ReadEnumChoices(description);
					if (choices != null)
					{
						Show(row.enumField);
						WireEnum(row, choices);
					}
					else if (row.keyword != null)
					{
						Show(row.toggle);
						WireToggle(row);
					}
					else
					{
						Show(row.intValue);
						WireInt(row);
					}
					break;

				case ShaderPropertyType.Color:
					Show(row.colorField);
					if (row.colorField != null)
					{
						row.colorField.hdr = description != null && description.IndexOf("HDR", StringComparison.Ordinal) >= 0;
						row.colorField.showAlpha = true;
					}
					WireColor(row);
					break;

				case ShaderPropertyType.Vector:
					Show(row.vectorGroup);
					WireVector(row);
					break;

				case ShaderPropertyType.Texture:
					Show(row.textureField);
					if (row.textureField != null)
					{
						row.textureField.objectType = typeof(Texture);
						row.textureField.allowSceneObjects = false;
						// [NonModifiableTexture] 的图是 shader 写死的，给个能看的框但别让人以为改得动
						row.textureField.SetEnabled(!ShaderUtil.IsShaderPropertyNonModifiableTexureProperty(shader, propIndex));
					}
					WireTexture(row);
					break;
			}
			return row;
		}

		private static string ReadDisplayName(Shader shader, int propIndex, string propName)
		{
#if UNITY_2022_2_OR_NEWER
			string displayName = ShaderUtil.GetPropertyDisplayName(shader, propIndex);
			if (!string.IsNullOrEmpty(displayName))
				return displayName;
#endif
			return HumanizePropertyName(propName);
		}

		/// <summary> <c>_BaseColor</c> → <c>Base Color</c>：下划线只是 shader 的命名习惯，不该出现在界面上 </summary>
		private static string HumanizePropertyName(string propName)
		{
			string text = propName.StartsWith("_", StringComparison.Ordinal) ? propName.Substring(1) : propName;
			var builder = new System.Text.StringBuilder(text.Length + 4);
			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				if (i > 0 && char.IsUpper(c) && !char.IsUpper(text[i - 1]) && builder.Length > 0)
					builder.Append(' ');
				builder.Append(c);
			}
			return builder.ToString();
		}

		/// <summary>
		/// 从 description 里认 <c>[Toggle(KEYWORD)]</c> / <c>[IntRange(KEYWORD)]</c> 的关键字。
		/// Unity 在这里给到的形状不止一种（"<c>_MY_KEYWORD</c>"、"My Toggle (<c>_MY_KEYWORD</c>)"、
		/// "Toggle - <c>_MY_KEYWORD</c> on off"），所以规则是：挑一个下划线开头、或以
		/// <c>_ON</c> / <c>_OFF</c> 结尾的标识符。
		/// <para>故意不收全大写裸词（<c>SPECULAR</c> 这种散文会被误判成关键字，把数字属性变成勾）；
		/// 代价是 <c>[Toggle(FOO)]</c> 这类不带下划线的写法认不出来，退回数字框——功能不丢，只是少了个勾。</para>
		/// </summary>
		private static string ReadKeyword(ShaderPropertyType type, string description)
		{
			if (string.IsNullOrEmpty(description))
				return null;
			if (type != ShaderPropertyType.Float && type != ShaderPropertyType.Int)
				return null;

			MatchCollection matches = Regex.Matches(description, @"_?[A-Za-z][A-Za-z0-9_]*");
			foreach (Match match in matches)
			{
				string token = match.Value;
				if (token.Length < 2)
					continue;
				if (token.StartsWith("_", StringComparison.Ordinal))
					return token;
				if (token.EndsWith("_ON", StringComparison.Ordinal) || token.EndsWith("_OFF", StringComparison.Ordinal))
					return token;
			}
			return null;
		}

		/// <summary> <c>[Enum(A|B|C)]</c> 与 <c>[KeywordEnum]</c> 的取值表；不是枚举就返回 null </summary>
		private static List<string> ReadEnumChoices(string description)
		{
			if (string.IsNullOrEmpty(description))
				return null;
			char[] separators = description.IndexOf('|') >= 0 ? new[] { '|' } : new[] { ',' };
			if (description.IndexOf('|') < 0 && description.IndexOf(',') < 0)
				return null;

			string[] parts = description.Split(separators);
			var choices = new List<string>(parts.Length);
			foreach (string part in parts)
			{
				string trimmed = part.Trim();
				if (trimmed.Length == 0)
					return null;
				choices.Add(trimmed);
			}
			// 只有一个分隔符的散文（"1, 000"之类）不该被当成枚举
			return choices.Count >= 2 ? choices : null;
		}

		// ── 取值 / 写值 ─────────────────────────────────────────

		private void FillFromMaterial(Row row)
		{
			switch (row.propType)
			{
				case ShaderPropertyType.Range:
					float range = m_Material.GetFloat(row.propName);
					// shader 的范围声明可能写反（Range(1, 0)），滑块吃下反区间会直接画不出把手
					float min = Mathf.Min(row.rangeMin, row.rangeMax);
					float max = Mathf.Max(row.rangeMin, row.rangeMax);
					if (row.slider != null) { row.slider.lowValue = min; row.slider.highValue = max; }
					if (range < min || range > max)
					{
						// 值掉在区间外时把区间撑开，否则滑块会把值就地改掉——那是"我动了你的参数"
						if (row.slider != null) { row.slider.lowValue = Mathf.Min(min, range); row.slider.highValue = Mathf.Max(max, range); }
					}
					if (row.slider != null) row.slider.SetValueWithoutNotify(Mathf.Clamp(range, row.slider.lowValue, row.slider.highValue));
					if (row.rangeValue != null) row.rangeValue.SetValueWithoutNotify(range);
					break;

				case ShaderPropertyType.Float:
					float value = m_Material.GetFloat(row.propName);
					if (row.toggle != null && row.keyword != null) row.toggle.SetValueWithoutNotify(!Mathf.Approximately(value, 0f));
					if (row.floatValue != null) row.floatValue.SetValueWithoutNotify(value);
					break;

				case ShaderPropertyType.Int:
					int intValue = m_Material.GetInt(row.propName);
					if (row.toggle != null && row.keyword != null) row.toggle.SetValueWithoutNotify(intValue != 0);
					if (row.intValue != null) row.intValue.SetValueWithoutNotify(intValue);
					if (row.enumField != null)
						row.enumField.SetValueWithoutNotify(ChoiceLabelFor(intValue, row.enumField.choices));
					break;

				case ShaderPropertyType.Color:
					if (row.colorField != null) row.colorField.SetValueWithoutNotify(m_Material.GetColor(row.propName));
					break;

				case ShaderPropertyType.Vector:
					Vector4 v = m_Material.GetVector(row.propName);
					if (row.vectorParts != null)
					{
						row.vectorParts[0].SetValueWithoutNotify(v.x);
						row.vectorParts[1].SetValueWithoutNotify(v.y);
						row.vectorParts[2].SetValueWithoutNotify(v.z);
						row.vectorParts[3].SetValueWithoutNotify(v.w);
					}
					break;

				case ShaderPropertyType.Texture:
					if (row.textureField != null) row.textureField.SetValueWithoutNotify(m_Material.GetTexture(row.propName));
					break;
			}
		}

		private void Write(Row row)
		{
			Action handler = changed;
			if (handler != null)
				handler.Invoke();
		}

		private void WriteKeywordState(Row row, bool on)
		{
			if (row.keyword == null || m_Material == null)
				return;
			// Unity 的原生材质面板就是这么做的：值写 float，关键字另写一份，两者必须同步
			m_Material.SetFloat(row.propName, on ? 1f : 0f);
			if (on) m_Material.EnableKeyword(row.keyword);
			else m_Material.DisableKeyword(row.keyword);
		}

		private void WireSlider(Row row)
		{
			if (row.slider == null) return;
			row.slider.RegisterValueChangedCallback(evt =>
			{
				if (row.updating || m_Material == null) return;
				m_Material.SetFloat(row.propName, evt.newValue);
				if (row.rangeValue != null) row.rangeValue.SetValueWithoutNotify(evt.newValue);
				Write(row);
			});
		}

		private void WireRangeValue(Row row)
		{
			if (row.rangeValue == null) return;
			row.rangeValue.RegisterValueChangedCallback(evt =>
			{
				if (row.updating || m_Material == null) return;
				m_Material.SetFloat(row.propName, evt.newValue);
				if (row.slider != null)
					row.slider.SetValueWithoutNotify(Mathf.Clamp(evt.newValue, row.slider.lowValue, row.slider.highValue));
				Write(row);
			});
		}

		private void WireFloat(Row row)
		{
			if (row.floatValue == null) return;
			row.floatValue.RegisterValueChangedCallback(evt =>
			{
				if (row.updating || m_Material == null) return;
				m_Material.SetFloat(row.propName, evt.newValue);
				Write(row);
			});
		}

		private void WireInt(Row row)
		{
			if (row.intValue == null) return;
			row.intValue.RegisterValueChangedCallback(evt =>
			{
				if (row.updating || m_Material == null) return;
				m_Material.SetInt(row.propName, evt.newValue);
				Write(row);
			});
		}

		private void WireToggle(Row row)
		{
			if (row.toggle == null) return;
			row.toggle.RegisterValueChangedCallback(evt =>
			{
				if (row.updating || m_Material == null) return;
				WriteKeywordState(row, evt.newValue);
				Write(row);
			});
		}

		private void WireEnum(Row row, List<string> choices)
		{
			if (row.enumField == null) return;
			row.enumField.choices = choices;
			row.enumField.RegisterValueChangedCallback(evt =>
			{
				if (row.updating || m_Material == null) return;
				int index = choices.IndexOf(evt.newValue);
				m_Material.SetInt(row.propName, Mathf.Max(index, 0));
				Write(row);
			});
		}

		private void WireColor(Row row)
		{
			if (row.colorField == null) return;
			row.colorField.RegisterValueChangedCallback(evt =>
			{
				if (row.updating || m_Material == null) return;
				m_Material.SetColor(row.propName, evt.newValue);
				Write(row);
			});
		}

		private void WireVector(Row row)
		{
			for (int i = 0; i < row.vectorParts.Length; i++)
			{
				FloatField field = row.vectorParts[i];
				if (field == null) continue;
				int component = i;
				field.RegisterValueChangedCallback(evt =>
				{
					if (row.updating || m_Material == null) return;
					Vector4 v = m_Material.GetVector(row.propName);
					if (component == 0) v.x = evt.newValue;
					else if (component == 1) v.y = evt.newValue;
					else if (component == 2) v.z = evt.newValue;
					else v.w = evt.newValue;
					m_Material.SetVector(row.propName, v);
					Write(row);
				});
			}
		}

		private void WireTexture(Row row)
		{
			if (row.textureField == null) return;
			row.textureField.RegisterValueChangedCallback(evt =>
			{
				if (row.updating || m_Material == null) return;
				m_Material.SetTexture(row.propName, evt.newValue as Texture);
				Write(row);
			});
		}

		private static string ChoiceLabelFor(int value, List<string> choices)
		{
			if (choices == null || choices.Count == 0)
				return string.Empty;
			if (value < 0 || value >= choices.Count)
				return value.ToString();
			return choices[value];
		}

		private static void HideAll(Row row)
		{
			Hide(row.floatValue);
			Hide(row.intValue);
			Hide(row.toggle);
			Hide(row.enumField);
			Hide(row.colorField);
			Hide(row.textureField);
			Hide(row.rangeGroup);
			Hide(row.vectorGroup);
		}

		private static void Show(VisualElement element)
		{
			if (element != null) element.style.display = DisplayStyle.Flex;
		}

		private static void Hide(VisualElement element)
		{
			if (element != null) element.style.display = DisplayStyle.None;
		}
	}
}
