using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XericUI.ShaderBake;

namespace XericUI.ShaderBake.Editor
{
	/// <summary>
	/// 右侧的"全局参数表"：一行一条 <see cref="ShaderBakeGlobalEntry"/>，勾选才参与烘焙。
	/// 结构与材质属性表同构——模板在 BakeShaderGlobalRow.uxml，这里只决定点亮哪个控件。
	/// </summary>
	internal sealed class ShaderBakeGlobalTable
	{
		private sealed class Row
		{
			public VisualElement root;
			public Toggle enabled;
			public Label label;
			public FloatField floatValue;
			public VisualElement vectorGroup;
			public FloatField[] parts;
			public ColorField color;
			public ObjectField texture;
			public ShaderBakeGlobalEntry entry;
			public bool updating;
		}

		private readonly VisualElement m_Container;
		private readonly VisualTreeAsset m_Template;
		private readonly List<Row> m_Rows = new List<Row>();

		public event Action changed;

		public int rowCount { get { return m_Rows.Count; } }

		public ShaderBakeGlobalTable(VisualElement root, VisualTreeAsset template)
		{
			m_Container = root.Q<VisualElement>("globals-rows");
			m_Template = template;
			if (m_Container == null)
				throw new InvalidOperationException("BakeShaderWindow.uxml 缺少 globals-rows 容器");
		}

		public void SetTarget(ShaderBakeGlobalSettings settings)
		{
			m_Settings = settings;
			Rebuild();
		}

		private ShaderBakeGlobalSettings m_Settings;

		public void Rebuild()
		{
			m_Rows.Clear();
			m_Container.Clear();
			if (m_Settings == null || m_Settings.entries == null || m_Template == null)
				return;

			foreach (ShaderBakeGlobalEntry entry in m_Settings.entries)
			{
				if (entry == null || string.IsNullOrEmpty(entry.name))
					continue;
				Row row = CreateRow(entry);
				m_Rows.Add(row);
				m_Container.Add(row.root);
			}
			RefreshValues();
		}

		/// <summary> 值被别处改过（粘贴参数、扫描新增）之后回写界面 </summary>
		public void RefreshValues()
		{
			foreach (Row row in m_Rows)
			{
				row.updating = true;
				try
				{
					row.enabled.SetValueWithoutNotify(row.entry.enabled);
					switch (row.entry.kind)
					{
						case ShaderBakeGlobalKind.Float:
							row.floatValue.SetValueWithoutNotify(row.entry.x);
							break;
						case ShaderBakeGlobalKind.Vector:
							row.parts[0].SetValueWithoutNotify(row.entry.x);
							row.parts[1].SetValueWithoutNotify(row.entry.y);
							row.parts[2].SetValueWithoutNotify(row.entry.z);
							row.parts[3].SetValueWithoutNotify(row.entry.w);
							break;
						case ShaderBakeGlobalKind.Color:
							row.color.SetValueWithoutNotify(row.entry.ToColor());
							break;
						case ShaderBakeGlobalKind.Texture:
							row.texture.SetValueWithoutNotify(row.entry.texture);
							break;
					}
				}
				finally
				{
					row.updating = false;
				}
			}
		}

		private Row CreateRow(ShaderBakeGlobalEntry entry)
		{
			VisualElement host = new VisualElement();
			m_Template.CloneTree(host);
			if (host.childCount == 0)
				return null;
			VisualElement root = host[0];
			root.RemoveFromHierarchy();

			var row = new Row
			{
				root = root,
				enabled = root.Q<Toggle>("global-enabled"),
				label = root.Q<Label>("global-name"),
				floatValue = root.Q<FloatField>("global-float"),
				vectorGroup = root.Q<VisualElement>("global-vector"),
				color = root.Q<ColorField>("global-color"),
				texture = root.Q<ObjectField>("global-texture"),
				entry = entry
			};
			row.parts = new[]
			{
				root.Q<FloatField>("global-x"),
				root.Q<FloatField>("global-y"),
				root.Q<FloatField>("global-z"),
				root.Q<FloatField>("global-w")
			};

			if (row.label != null)
			{
				row.label.text = entry.name;
				row.label.tooltip = entry.name + "\n" + DescribeKind(entry.kind);
			}

			Hide(row.floatValue);
			Hide(row.vectorGroup);
			Hide(row.color);
			Hide(row.texture);
			switch (entry.kind)
			{
				case ShaderBakeGlobalKind.Float: Show(row.floatValue); break;
				case ShaderBakeGlobalKind.Vector: Show(row.vectorGroup); break;
				case ShaderBakeGlobalKind.Color: Show(row.color); break;
				case ShaderBakeGlobalKind.Texture: Show(row.texture); break;
			}

			if (row.enabled != null)
			{
				row.enabled.RegisterValueChangedCallback(evt =>
				{
					if (row.updating) return;
					entry.enabled = evt.newValue;
					Notify();
				});
			}

			if (row.floatValue != null)
			{
				row.floatValue.RegisterValueChangedCallback(evt =>
				{
					if (row.updating) return;
					entry.x = evt.newValue;
					Notify();
				});
			}

			if (row.parts != null)
			{
				for (int i = 0; i < row.parts.Length; i++)
				{
					int component = i;
					FloatField field = row.parts[i];
					if (field == null) continue;
					field.RegisterValueChangedCallback(evt =>
					{
						if (row.updating) return;
						switch (component)
						{
							case 0: entry.x = evt.newValue; break;
							case 1: entry.y = evt.newValue; break;
							case 2: entry.z = evt.newValue; break;
							default: entry.w = evt.newValue; break;
						}
						Notify();
					});
				}
			}

			if (row.color != null)
			{
				row.color.RegisterValueChangedCallback(evt =>
				{
					if (row.updating) return;
					entry.x = evt.newValue.r;
					entry.y = evt.newValue.g;
					entry.z = evt.newValue.b;
					entry.w = evt.newValue.a;
					Notify();
				});
			}

			if (row.texture != null)
			{
				row.texture.objectType = typeof(Texture);
				row.texture.allowSceneObjects = false;
				row.texture.RegisterValueChangedCallback(evt =>
				{
					if (row.updating) return;
					entry.texture = evt.newValue as Texture;
					Notify();
				});
			}
			return row;
		}

		private static string DescribeKind(ShaderBakeGlobalKind kind)
		{
			switch (kind)
			{
				case ShaderBakeGlobalKind.Float: return "float";
				case ShaderBakeGlobalKind.Vector: return "float4";
				case ShaderBakeGlobalKind.Color: return "color";
				default: return "texture";
			}
		}

		private void Notify()
		{
			Action handler = changed;
			if (handler != null)
				handler.Invoke();
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
