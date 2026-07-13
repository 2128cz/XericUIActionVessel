using UnityEngine;

namespace XericUI.XTable.Core
{
	/// <summary>
	/// 表格系统全局配置资源——统一管理布局、样式回退值、运行时颜色等参数。
	/// 表格组件在 Awake 时自动加载 Resources 中的 "XTableDefaultConfig"，
	/// 或由外部脚本在初始化阶段显式赋值。
	/// </summary>
	[CreateAssetMenu(fileName = "XTableConfig", menuName = "Xeric Library/Table/Table Config", order = 100)]
	public class XTableConfig : ScriptableObject
	{
		#region 布局

		[Header("布局")]
		[Tooltip("新增行时的默认行高（像素）")]
		public float DefaultRowHeight = 40f;

		[Tooltip("新增列时的默认列宽（像素）")]
		public float DefaultColumnWidth = 100f;

		[Tooltip("单元格内容水平内边距（左右各此值）")]
		public float CellHorizontalPadding = 4f;

		#endregion

		#region 四叉树

		[Header("四叉树")]
		[Tooltip("单节点最大单元格数，超过此值强制空间分裂以提升可见性裁剪效率")]
		public int QuadtreeMaxCellsPerNode = 64;

		#endregion

		#region 对象池

		[Header("对象池")]
		[Tooltip("通用对象池初始容量")]
		public int PoolInitialCapacity = 64;

		#endregion

		#region 样式回退值

		[Header("样式回退值（当 StyleManager 中查不到时使用）")]
		[Tooltip("默认样式命名空间")]
		public string DefaultStyleNamespace = "xeric_table_default";

		[Tooltip("单元格背景色")]
		public Color BgColor = new Color(0.18f, 0.18f, 0.18f, 1f);

		[Tooltip("单元格前景色（文字颜色）")]
		public Color FgColor = new Color(0.05f, 0.05f, 0.05f);

		[Tooltip("编辑器选中色")]
		public Color SelectionColor = new Color(0.2f, 0.5f, 1f, 0.3f);

		[Tooltip("边框颜色")]
		public Color BorderColor = new Color(0.35f, 0.35f, 0.35f, 0.5f);

		[Tooltip("边框宽度")]
		public float BorderWidth = 1f;

		[Tooltip("字体大小")]
		public int FontSize = 14;

		#endregion

		#region 运行时颜色

		[Header("运行时选中/边框颜色")]
		[Tooltip("运行时单选高亮")]
		public Color RuntimeSelectionColor = new Color(0.18f, 0.42f, 0.82f, 0.25f);

		[Tooltip("运行时多选高亮")]
		public Color RuntimeMultiSelectColor = new Color(0.18f, 0.42f, 0.82f, 0.12f);

		[Tooltip("运行时焦点高亮（当前选中格）")]
		public Color RuntimeFocusColor = new Color(0.22f, 0.55f, 0.95f, 0.35f);

		[Tooltip("运行时边框色")]
		public Color RuntimeBorderColor = new Color(0.35f, 0.35f, 0.35f, 0.5f);

		[Tooltip("运行时边框宽度")]
		public float RuntimeBorderWidth = 1f;

		#endregion

		#region 编辑器多选颜色因子

		[Header("编辑器多选/焦点颜色因子（基于 SelectionColor 计算）")]
		[Tooltip("多选透明因子")]
		[Range(0, 1)] public float MultiSelectAlphaFactor = 0.4f;

		[Tooltip("焦点色 R 增量")]
		[Range(0, 0.5f)] public float FocusRAdd = 0.04f;

		[Tooltip("焦点色 G 增量")]
		[Range(0, 0.5f)] public float FocusGAdd = 0.13f;

		[Tooltip("焦点色 B 增量")]
		[Range(0, 0.5f)] public float FocusBAdd = 0.13f;

		[Tooltip("焦点色 Alpha 倍数")]
		[Range(0.1f, 3f)] public float FocusAlphaMul = 1.4f;

		#endregion

		#region 资源路径

		/// <summary>Resources 目录下的默认配置文件名（不含扩展名）</summary>
		public const string DEFAULT_RESOURCE_PATH = "XTableDefaultConfig";

		/// <summary>从 Resources 加载默认配置，若失败则返回 ScriptableObject.CreateInstance 的默认值</summary>
		public static XTableConfig LoadOrDefault()
		{
			var cfg = Resources.Load<XTableConfig>(DEFAULT_RESOURCE_PATH);
			if (cfg != null) return cfg;
			return CreateInstance<XTableConfig>();
		}

		#endregion
	}
}
