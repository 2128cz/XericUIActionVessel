using System.Collections.Generic;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace XericUI.XTable.Rendering.Elements
{
	/// <summary>
	/// 表格单元动态拼装器——通过对象池管理 Image + TMP_Text 的组合创建与回收。
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
		}

		public class PooledText
		{
			public GameObject Root;
			public RectTransform Rect;
			public TMP_Text Text;
			public StyleBoundElement Style;
		}

		#endregion

		#region 字段

		private Transform m_Parent;

		/// <summary>运行时的默认样式命名空间（编辑器下为 null，不绑定）</summary>
		private string m_DefaultStyleNamespace;

		/// <summary>所有已绑定的 StyleBoundElement 回调集合（用于样式变更时通知外部）</summary>
		public event System.Action OnStyleChangeDetected;

		private Queue<PooledImage> m_ImagePool = new Queue<PooledImage>();
		private Queue<PooledText> m_TextPool = new Queue<PooledText>();
		private List<PooledImage> m_ActiveImages = new List<PooledImage>();
		private List<PooledText> m_ActiveTexts = new List<PooledText>();

		/// <summary>HideFlags 应用于所有动态创建的对象（不保存、不在 Hierarchy 显示）</summary>
		private const HideFlags POOL_FLAGS = HideFlags.DontSave | HideFlags.HideInHierarchy;

		/// <summary>渲染项目总数上限，超出后 GetImage/GetText 返回 null 并发出警告</summary>
		private const int MAX_POOL_ITEMS = 256;

		private int m_ImageCreatedCount;
		private int m_TextCreatedCount;

		#endregion

		#region 构造函数与析构

		public CellAssembler(Transform parent, string defaultStyleNamespace = null)
		{
			m_Parent = parent;
			m_DefaultStyleNamespace = defaultStyleNamespace;
		}

		/// <summary>彻底销毁所有池对象（包括活跃和休眠的）</summary>
		public void DestroyAll()
		{
			// 杀死活跃对象
			for (int i = m_ActiveImages.Count - 1; i >= 0; i--)
			{
				KillObject(m_ActiveImages[i].Root);
			}
			m_ActiveImages.Clear();

			for (int i = m_ActiveTexts.Count - 1; i >= 0; i--)
			{
				KillObject(m_ActiveTexts[i].Root);
			}
			m_ActiveTexts.Clear();

			// 杀死池中对象
			while (m_ImagePool.Count > 0)
			{
				KillObject(m_ImagePool.Dequeue().Root);
			}
			while (m_TextPool.Count > 0)
			{
				KillObject(m_TextPool.Dequeue().Root);
			}
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

		public PooledImage GetImage(string name = "cell_img", string styleNamespace = null)
		{
			PooledImage item;
			if (m_ImagePool.Count > 0)
			{
				item = m_ImagePool.Dequeue();
				ResetImageDefaults(item);
			}
			else if (m_ImageCreatedCount < MAX_POOL_ITEMS)
			{
				item = CreateImage(name);
				m_ImageCreatedCount++;
			}
			else
			{
				Debug.LogWarning($"[XericUIActionTable] Image 池已达上限 {MAX_POOL_ITEMS}，无法分配新对象");
				return null;
			}
			BindStyle(item.Style, styleNamespace);
			m_ActiveImages.Add(item);
			return item;
		}

		public PooledText GetText(string name = "cell_text", string styleNamespace = null)
		{
			PooledText item;
			if (m_TextPool.Count > 0)
			{
				item = m_TextPool.Dequeue();
				ResetTextDefaults(item);
			}
			else if (m_TextCreatedCount < MAX_POOL_ITEMS)
			{
				item = CreateText(name);
				m_TextCreatedCount++;
			}
			else
			{
				Debug.LogWarning($"[XericUIActionTable] Text 池已达上限 {MAX_POOL_ITEMS}，无法分配新对象");
				return null;
			}
			BindStyle(item.Style, styleNamespace);
			m_ActiveTexts.Add(item);
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

		public void ReturnAll()
		{
			foreach (var img in m_ActiveImages) ReturnImage(img);
			m_ActiveImages.Clear();

			foreach (var txt in m_ActiveTexts) ReturnText(txt);
			m_ActiveTexts.Clear();
		}

		/// <summary>归还单个 Image 池对象（从活跃列表移除并推入休眠栈）</summary>
		public void ReleaseImage(PooledImage item)
		{
			if (item == null) return;
			m_ActiveImages.Remove(item);
			ReturnImage(item);
		}

		/// <summary>归还单个 Text 池对象（从活跃列表移除并推入休眠栈）</summary>
		public void ReleaseText(PooledText item)
		{
			if (item == null) return;
			m_ActiveTexts.Remove(item);
			ReturnText(item);
		}

		private void ReturnImage(PooledImage item)
		{
			item.Style?.Unbind();
			m_ImagePool.Enqueue(item);
		}

		private void ReturnText(PooledText item)
		{
			item.Style?.Unbind();
			m_TextPool.Enqueue(item);
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
