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
		private Transform m_PoolRoot;

		/// <summary>池根节点（所有回收对象均挂在此节点下，节点本身处于 inactive）</summary>
		public Transform PoolRoot => m_PoolRoot;

		private Stack<PooledImage> m_ImagePool = new Stack<PooledImage>();
		private Stack<PooledText> m_TextPool = new Stack<PooledText>();
		private List<PooledImage> m_ActiveImages = new List<PooledImage>();
		private List<PooledText> m_ActiveTexts = new List<PooledText>();

		/// <summary>HideFlags 应用于所有动态创建的对象（不保存、不在 Hierarchy 显示）</summary>
		private const HideFlags POOL_FLAGS = HideFlags.DontSave | HideFlags.HideInHierarchy;

		#endregion

		#region 构造函数与析构

		public CellAssembler(Transform parent)
		{
			m_Parent = parent;

			GameObject poolRoot = new GameObject("_CellPool");
			poolRoot.hideFlags = POOL_FLAGS;
			poolRoot.transform.SetParent(parent, false);
			poolRoot.SetActive(false);
			m_PoolRoot = poolRoot.transform;
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
				KillObject(m_ImagePool.Pop().Root);
			}
			while (m_TextPool.Count > 0)
			{
				KillObject(m_TextPool.Pop().Root);
			}

			// 杀死池根
			if (m_PoolRoot != null)
			{
				KillObject(m_PoolRoot.gameObject);
				m_PoolRoot = null;
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

		public PooledImage GetImage(string name = "cell_img")
		{
			PooledImage item;
			if (m_ImagePool.Count > 0)
			{
				item = m_ImagePool.Pop();
				item.Root.SetActive(true);
				item.Root.transform.SetParent(m_Parent, false);
				ResetImageDefaults(item);
			}
			else
			{
				item = CreateImage(name);
			}
			m_ActiveImages.Add(item);
			return item;
		}

		public PooledText GetText(string name = "cell_text")
		{
			PooledText item;
			if (m_TextPool.Count > 0)
			{
				item = m_TextPool.Pop();
				item.Root.SetActive(true);
				item.Root.transform.SetParent(m_Parent, false);
				ResetTextDefaults(item);
			}
			else
			{
				item = CreateText(name);
			}
			m_ActiveTexts.Add(item);
			return item;
		}

		public void ReturnAll()
		{
			foreach (var img in m_ActiveImages) ReturnImage(img);
			m_ActiveImages.Clear();

			foreach (var txt in m_ActiveTexts) ReturnText(txt);
			m_ActiveTexts.Clear();
		}

		private void ReturnImage(PooledImage item)
		{
			item.Style?.Unbind();
			item.Root.SetActive(false);
			item.Root.transform.SetParent(m_PoolRoot, false);
			m_ImagePool.Push(item);
		}

		private void ReturnText(PooledText item)
		{
			item.Style?.Unbind();
			item.Text.text = "";
			item.Root.SetActive(false);
			item.Root.transform.SetParent(m_PoolRoot, false);
			m_TextPool.Push(item);
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
			item.Text.text = "";
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
