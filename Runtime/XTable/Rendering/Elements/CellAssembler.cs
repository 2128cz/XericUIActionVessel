using System.Collections.Generic;
using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace XericUI.XTable.Rendering.Elements
{
	/// <summary>
	/// 表格单元动态拼装器——通过 SlotCounterPool 管理 Image + TMP_Text 的组合创建与回收。
	/// cellId 在绑定期间稳定复用同一对象；对象显隐仅随所属 Counter 整体借出或归还变更。
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
			public int CellId;
		}

		public class PooledText
		{
			public GameObject Root;
			public RectTransform Rect;
			public TMP_Text Text;
			public StyleBoundElement Style;
			public int CellId;
		}

		#endregion

		#region 字段

		private Transform m_Parent;
		private string m_DefaultStyleNamespace;
		public event System.Action OnStyleChangeDetected;
		private SlotCounterPool<PooledImage> m_ImagePool;
		private SlotCounterPool<PooledText> m_TextPool;
		private const HideFlags POOL_FLAGS = HideFlags.DontSave | HideFlags.HideInHierarchy;

		#endregion

		#region 构造函数与析构

		public CellAssembler(Transform parent, string defaultStyleNamespace = null, int textPoolMaxCapacity = 256,
			int imagePoolMaxCapacity = 256, int textPoolRouteBucketCount = 256, int imagePoolRouteBucketCount = 256)
		{
			m_Parent = parent;
			m_DefaultStyleNamespace = defaultStyleNamespace;
			m_ImagePool = new SlotCounterPool<PooledImage>(() => CreateImage("cell_img"), imagePoolMaxCapacity,
				SetImageCounterActive, SetImageCounterInactive, imagePoolRouteBucketCount);
			m_TextPool = new SlotCounterPool<PooledText>(() => CreateText("cell_text"), textPoolMaxCapacity,
				SetTextCounterActive, SetTextCounterInactive, textPoolRouteBucketCount);
		}

		/// <summary>彻底销毁所有池对象。</summary>
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

		/// <summary>为指定 cellId 获取池化 Image；已有绑定原样返回。</summary>
		public PooledImage GetImage(int cellId, string name = "cell_img", string styleNamespace = null,
			Transform parent = null)
		{
			var item = m_ImagePool.Acquire(cellId, out bool isNewBinding);
			if (item == null) return null;
			if (!isNewBinding)
			{
				if (parent != null && item.Root.transform.parent != parent)
					item.Root.transform.SetParent(parent, false);
				BindStyle(item.Style, styleNamespace);
				return item;
			}

			if (parent != null && item.Root.transform.parent != parent)
				item.Root.transform.SetParent(parent, false);
			item.CellId = cellId;
			item.Root.name = name;
			ResetImageDefaults(item);
			BindStyle(item.Style, styleNamespace);
			return item;
		}

		/// <summary>为指定 cellId 获取池化 TMP_Text；已有绑定原样返回。</summary>
		public PooledText GetText(int cellId, string name = "cell_text", string styleNamespace = null,
			Transform parent = null)
		{
			var item = m_TextPool.Acquire(cellId, out bool isNewBinding);
			if (item == null) return null;
			if (!isNewBinding)
			{
				if (parent != null && item.Root.transform.parent != parent)
					item.Root.transform.SetParent(parent, false);
				BindStyle(item.Style, styleNamespace);
				return item;
			}

			if (parent != null && item.Root.transform.parent != parent)
				item.Root.transform.SetParent(parent, false);
			item.CellId = cellId;
			item.Root.name = name;
			ResetTextDefaults(item);
			BindStyle(item.Style, styleNamespace);
			return item;
		}

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

		/// <summary>解除指定 cellId 的 Image 与 Text 槽位绑定。</summary>
		public void ReleaseCell(int cellId)
		{
			m_ImagePool.ReleaseCell(cellId);
			m_TextPool.ReleaseCell(cellId);
		}

		/// <summary>解除全部 Image 与 Text 槽位绑定。</summary>
		public void ReleaseAllBindings()
		{
			m_ImagePool.ReleaseAllBindings();
			m_TextPool.ReleaseAllBindings();
		}

		/// <summary>兼容旧 API：解除全部槽位绑定。</summary>
		public void ReturnAll()
		{
			ReleaseAllBindings();
		}

		/// <summary>解除指定 Image 的槽位绑定；显隐仅在所属 Counter 完整归还时变更。</summary>
		public void ReleaseImage(PooledImage item)
		{
			if (item != null) m_ImagePool.ReleaseByObject(item);
		}

		/// <summary>解除指定 Text 的槽位绑定；显隐仅在所属 Counter 完整归还时变更。</summary>
		public void ReleaseText(PooledText item)
		{
			if (item != null) m_TextPool.ReleaseByObject(item);
		}

		private void SetImageCounterActive(IReadOnlyList<PooledImage> items)
		{
			for (int i = 0; i < items.Count; i++)
			{
				// Counter 整体重新借出前清空历史 Slot 的视觉状态；未重新绑定的 Slot
				// 即使随 Counter 激活，也不会保留冻结区域的旧内容。
				ResetImageDefaults(items[i]);
				items[i].Root.SetActive(true);
			}
		}

		private static void SetImageCounterInactive(IReadOnlyList<PooledImage> items)
		{
			for (int i = 0; i < items.Count; i++)
			{
				items[i].Style?.Unbind();
				items[i].Root.SetActive(false);
			}
		}

		private void SetTextCounterActive(IReadOnlyList<PooledText> items)
		{
			for (int i = 0; i < items.Count; i++)
			{
				// Counter 整体重新借出前清空历史 Slot 的视觉状态；未重新绑定的 Slot
				// 即使随 Counter 激活，也不会保留冻结区域的旧内容。
				ResetTextDefaults(items[i]);
				items[i].Root.SetActive(true);
			}
		}

		private static void SetTextCounterInactive(IReadOnlyList<PooledText> items)
		{
			for (int i = 0; i < items.Count; i++)
			{
				items[i].Style?.Unbind();
				items[i].Root.SetActive(false);
			}
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
			return new PooledImage { Root = go, Rect = rect, Image = img, Style = new StyleBoundElement { ImageComponent = img } };
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
			return new PooledText { Root = go, Rect = rect, Text = text, Style = new StyleBoundElement { TextComponent = text } };
		}

		#endregion
	}
}
