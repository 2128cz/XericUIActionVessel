using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace XericUI.XTable.Rendering.Elements
{
	/// <summary>
	/// 表格单元动态拼装器——通过 SlotCounterPool 管理 Image + TMP_Text 的组合创建与回收。
	/// 每个单元格通过 cellId 映射到固定索引的计数器槽位，空闲槽位经 FrozenSlots 全局流转复用。
	/// 所有元素统一使用 anchor=(0,1) pivot=(0,1) 左上角坐标系。
	/// 所有动态生成对象标记 HideFlags.DontSave | HideFlags.HideInHierarchy，防止泄漏到场景。
	/// </summary>
	public class CellAssembler
	{
		#region 对象池定义

		public class PooledImage
		{
			public GameObject Root;
			public RectTransform Rect;
			public Image Image;
			public StyleBoundElement Style;
			/// <summary>绑定的单元格 ID，用于 Release 定位槽位</summary>
			public int CellId;
		}

		public class PooledText
		{
			public GameObject Root;
			public RectTransform Rect;
			public TMP_Text Text;
			public StyleBoundElement Style;
			/// <summary>绑定的单元格 ID，用于 Release 定位槽位</summary>
			public int CellId;
		}

		#endregion

		#region 字段

		private Transform m_Parent;

		/// <summary>运行时的默认样式命名空间（编辑器下为 null，不绑定）</summary>
		private string m_DefaultStyleNamespace;

		/// <summary>所有已绑定的 StyleBoundElement 回调集合（用于样式变更时通知外部）</summary>
		public event System.Action OnStyleChangeDetected;

		/// <summary>基于计数器的 Image 槽位对象池</summary>
		private SlotCounterPool<PooledImage> m_ImagePool;
		/// <summary>基于计数器的 Text 槽位对象池</summary>
		private SlotCounterPool<PooledText> m_TextPool;

		/// <summary>HideFlags 应用于所有动态创建的对象（不保存、不在 Hierarchy 显示）</summary>
		private const HideFlags POOL_FLAGS = HideFlags.DontSave | HideFlags.HideInHierarchy;

		#endregion

		#region 构造函数与析构

		public CellAssembler(Transform parent, string defaultStyleNamespace = null)
		{
			m_Parent = parent;
			m_DefaultStyleNamespace = defaultStyleNamespace;
			m_ImagePool = new SlotCounterPool<PooledImage>(() => CreateImage("cell_img"));
			m_TextPool = new SlotCounterPool<PooledText>(() => CreateText("cell_text"));
		}

		/// <summary>彻底销毁所有池对象（包括计数器内活跃槽位和冻结槽位）</summary>
		public void DestroyAll()
		{
			m_ImagePool.DestroyAll(item => KillObject(item.Root));
			m_TextPool.DestroyAll(item => KillObject(item.Root));
		}

		private static void KillObject(GameObject go)
		{
			if (go == null) return;
			if (Application.isPlaying)
				Object.Destroy(go);
			else
				Object.DestroyImmediate(go);
		}

		#endregion

		#region 常量

		private static readonly Vector2 ANCHOR_TOPLEFT_MIN = new Vector2(0, 1);
		private static readonly Vector2 ANCHOR_TOPLEFT_MAX = new Vector2(0, 1);
		private static readonly Vector2 PIVOT_TOPLEFT = new Vector2(0, 1);

		#endregion

		#region 池操作

		/// <summary>为指定 cellId 获取一个池化 Image</summary>
		public PooledImage GetImage(int cellId, string name = "cell_img", string styleNamespace = null,
			Transform parent = null)
		{
			var item = m_ImagePool.Acquire(cellId);
			if (item == null) return null;

			if (parent != null && item.Root.transform.parent != parent)
				item.Root.transform.SetParent(parent, false);
			item.CellId = cellId;
			item.Root.name = name;
			ResetImageDefaults(item);
			BindStyle(item.Style, styleNamespace);
			return item;
		}

		/// <summary>为指定 cellId 获取一个池化 TMP_Text</summary>
		public PooledText GetText(int cellId, string name = "cell_text", string styleNamespace = null,
			Transform parent = null)
		{
			var item = m_TextPool.Acquire(cellId);
			if (item == null) return null;

			if (parent != null && item.Root.transform.parent != parent)
				item.Root.transform.SetParent(parent, false);
			item.CellId = cellId;
			item.Root.name = name;
			ResetTextDefaults(item);
			BindStyle(item.Style, styleNamespace);
			return item;
		}

		/// <summary>运行时绑定样式到池元素的 StyleBoundElement，实现被动更新（push）</summary>
		private void BindStyle(StyleBoundElement style, string cellNamespace)
		{
			if (!Application.isPlaying) return;

			string ns = cellNamespace ?? m_DefaultStyleNamespace;
			if (string.IsNullOrEmpty(ns)) return;

			if (style != null && style.BoundNamespace != ns)
			{
				style.OnStyleChangedCallback -= OnStyleChangedNotify;
				style.OnStyleChangedCallback += OnStyleChangedNotify;
				style.Bind(ns);
			}
		}

		private void OnStyleChangedNotify()
		{
			OnStyleChangeDetected?.Invoke();
		}

		/// <summary>归还所有槽位至 FrozenSlots（全量刷新时调用）</summary>
		public void ReturnAll()
		{
			// Unbind 所有活跃槽位的样式回调
			m_ImagePool.ReturnAll();
			m_TextPool.ReturnAll();
		}

		/// <summary>归还单个 Image 池对象（通过 CellId 定位槽位并移入 FrozenSlots）</summary>
		public void ReleaseImage(PooledImage item)
		{
			if (item == null) return;
			item.Style?.Unbind();
			m_ImagePool.ReleaseByObject(item);
		}

		/// <summary>归还单个 Text 池对象（通过 CellId 定位槽位并移入 FrozenSlots）</summary>
		public void ReleaseText(PooledText item)
		{
			if (item == null) return;
			item.Style?.Unbind();
			m_TextPool.ReleaseByObject(item);
		}

		#endregion

		#region 默认值重置

		private void ResetImageDefaults(PooledImage item)
		{
			item.Rect.anchorMin = ANCHOR_TOPLEFT_MIN;
			item.Rect.anchorMax = ANCHOR_TOPLEFT_MAX;
			item.Rect.pivot = PIVOT_TOPLEFT;
			item.Rect.anchoredPosition = Vector2.zero;
			item.Rect.sizeDelta = Vector2.zero;
			item.Image.color = Color.white;
			item.Image.sprite = null;
			item.Image.raycastTarget = false;
		}

		private void ResetTextDefaults(PooledText item)
		{
			item.Rect.anchorMin = ANCHOR_TOPLEFT_MIN;
			item.Rect.anchorMax = ANCHOR_TOPLEFT_MAX;
			item.Rect.pivot = PIVOT_TOPLEFT;
			item.Rect.anchoredPosition = Vector2.zero;
			item.Rect.sizeDelta = Vector2.zero;
			item.Text.SetText(string.Empty);
			item.Text.color = Color.black;
			item.Text.fontSize = 14;
			item.Text.alignment = TextAlignmentOptions.Midline;
			item.Text.raycastTarget = false;
		}

		#endregion

		#region 创建

		private PooledImage CreateImage(string name)
		{
			GameObject go = new GameObject(name,
				typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			go.hideFlags = POOL_FLAGS;
			go.transform.SetParent(m_Parent, false);

			Image img = go.GetComponent<Image>();
			img.raycastTarget = false;

			RectTransform rect = go.GetComponent<RectTransform>();
			rect.anchorMin = ANCHOR_TOPLEFT_MIN;
			rect.anchorMax = ANCHOR_TOPLEFT_MAX;
			rect.pivot = PIVOT_TOPLEFT;

			return new PooledImage
			{
				Root = go,
				Rect = rect,
				Image = img,
				Style = new StyleBoundElement { ImageComponent = img }
			};
		}

		private PooledText CreateText(string name)
		{
			GameObject go = new GameObject(name,
				typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
			go.hideFlags = POOL_FLAGS;
			go.transform.SetParent(m_Parent, false);

			TMP_Text text = go.GetComponent<TMP_Text>();
			text.raycastTarget = false;
			text.alignment = TextAlignmentOptions.Midline;
			text.fontSize = 14;

			RectTransform rect = go.GetComponent<RectTransform>();
			rect.anchorMin = ANCHOR_TOPLEFT_MIN;
			rect.anchorMax = ANCHOR_TOPLEFT_MAX;
			rect.pivot = PIVOT_TOPLEFT;

			return new PooledText
			{
				Root = go,
				Rect = rect,
				Text = text,
				Style = new StyleBoundElement { TextComponent = text }
			};
		}

		#endregion
	}
}
