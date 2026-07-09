using System;
using UnityEngine;
using XericLibrary.Runtime.SuperStyleSheet;

namespace XericUI.XTable.Core
{
	/// <summary>单元格内容类型——决定渲染时使用哪种内容</summary>
	public enum CellContentType
	{
		/// <summary>文本内容</summary>
		Text,
		/// <summary>图片纹理</summary>
		Image,
		/// <summary>预制体实例</summary>
		Prefab,
	}

	/// <summary>
	/// 单元格数据模型——仅保存数据内容，不保存自身尺寸。
	/// 尺寸由渲染阶段的行列标题（行高/列宽）决定。
	/// 样式通过 StyleNamespace 字符串引用 StyleManager 中的命名空间。
	/// </summary>
	[Serializable]
	public class XTableCellData
	{
		/// <summary>内容类型——决定渲染时使用 Text / Image / Prefab 中的哪一个</summary>
		public CellContentType ContentType;

		/// <summary>文本内容（ContentType == Text 时生效）</summary>
		public string Text;

		/// <summary>图片纹理（ContentType == Image 时生效）</summary>
		public Texture2D Image;

		/// <summary>预制体对象（ContentType == Prefab 时生效，用于嵌入复杂 UI 元素）</summary>
		public GameObject Prefab;

		/// <summary>
		/// 样式命名空间——对应 StyleManager 中的命名空间。
		/// 为空或 "xeric_table_default" 时使用默认表格样式。
		/// 渲染时从此命名空间读取 /cell/bg/color、/cell/font/size 等路径。
		/// </summary>
		public string StyleNamespace;

		/// <summary>
		/// 单元格级别样式覆盖——通过命名空间 + 路径引用样式表中的值。
		/// 渲染时优先使用此样式路径派生颜色/尺寸（如 {StylePath}/bg/color），未设置时回退到 StyleNamespace。
		/// </summary>
		public StyleMember CellStyle;

		/// <summary>预制体实例从对象池取出时回调——外部可在此初始化实例内容</summary>
		[NonSerialized] public System.Action<GameObject> OnPrefabAcquire;

		/// <summary>预制体实例归还对象池前回调——外部可在此清理引用</summary>
		[NonSerialized] public System.Action<GameObject> OnPrefabRelease;

		public XTableCellData()
		{ }

		public XTableCellData(string text) => Text = text;

		public XTableCellData(string text, string styleNamespace = null) : this(text) =>
			StyleNamespace = styleNamespace;

		public XTableCellData(string text, Texture2D image, string styleNamespace = null) :
			this(text, styleNamespace) => Image = image;

		/// <summary>获取该单元格渲染时使用的样式命名空间（CellStyle 优先，否则 fallback 到 StyleNamespace）</summary>
		public string GetEffectiveStyleNamespace(string fallbackNamespace)
		{
			if (CellStyle != null && !string.IsNullOrEmpty(CellStyle.CurrentNamespace))
				return CellStyle.CurrentNamespace;
			if (!string.IsNullOrEmpty(StyleNamespace))
				return StyleNamespace;
			return fallbackNamespace;
		}

		public void ClearEvent()
		{
			OnPrefabAcquire = null;
			OnPrefabRelease = null;
		}
	}
}
