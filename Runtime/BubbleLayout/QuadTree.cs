using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
#if Unity_Runtime
namespace XericLibrary.Runtime.Type.SpatialAlgorithm
{
	/// <summary>
	/// 灵活的四叉树实现，支持多种坐标类型
	/// </summary>
	public class QuadTree<T>
	{
		#region 字段属性

		private int maxObjectsPerNode = 5;
		private int maxDepth = 4;

		private Node root;
		private Func<T, Rect> getRectFunc; // 获取对象边界的委托

		/// <summary>
		/// 获取此四叉树的范围
		/// </summary>
		public Rect RootRect => root != null ? root.boundary : Rect.zero;

		public int Count { get; private set; }

		/// <summary>
		/// 清空四叉树，保留边界和配置以便重用
		/// </summary>
		public void Clear()
		{
			if (root != null)
			{
				root.ClearAll();
			}
			Count = 0;
		}

		/// <summary>
		/// 重建四叉树，使用相同边界和配置
		/// </summary>
		public void Rebuild(IEnumerable<T> items)
		{
			Clear();
			foreach (var item in items)
			{
				Insert(item);
			}
		}

		/// <summary>
		/// 获取四叉树中所有元素
		/// </summary>
		public void GetAllItems(HashSet<T> result)
		{
			if (result == null)
				return;
			root?.CollectAllItems(result);
		}
		
		#endregion

		#region 构造函数

		/// <summary>
		/// 初始化四叉树
		/// </summary>
		/// <param name="boundary">四叉树边界</param>
		/// <param name="getRectFunc">获取对象边界的函数</param>
		public QuadTree(Rect boundary, Func<T, Rect> getRectFunc, int maxObjectsPerNode = 5, int maxDepth = 4)
		{
			this.maxObjectsPerNode = Math.Max(1, maxObjectsPerNode);
			this.maxDepth = Math.Max(1, maxDepth);
			this.root = new Node(boundary, 0, getRectFunc, this);
			this.getRectFunc = getRectFunc ?? throw new ArgumentNullException(nameof(getRectFunc));
		}

		#endregion

		#region 公共方法

		/// <summary>
		/// 插入元素
		/// </summary>
		public bool Insert(T item)
		{
			return root.Insert(item);
		}

		/// <summary>
		/// 查找范围内的元素
		/// </summary>
		/// <param name="area">查找范围</param>
		/// <param name="result">建议使用数据容器对象池控制，避免频繁GC，默认空将自动创建新的数据容器</param>
		/// <returns></returns>
		public HashSet<T> Retrieve(Rect area, HashSet<T> result = null)
		{
			return root.Retrieve(area, result);
		}

		/// <summary>
		/// 查找最近的元素
		/// </summary>
		public T FindNearest(Vector2 target, float maxDistance = float.MaxValue)
		{
			T nearest = default;
			float minDistanceSquared = maxDistance * maxDistance;
			root.FindNearest(target, ref nearest, ref minDistanceSquared);
			return nearest;
		}

		/// <summary>
		/// 调试绘制网格
		/// </summary>
		public void DrawGrid()
		{
			root.DrawGrid(Color.white);
		}

		#endregion

		#region 节点类

		private class Node : IEnumerable<Node>
		{
			internal Rect boundary;
			internal HashSet<T> objects = new HashSet<T>();
			internal Node[] children;
			internal readonly int depth;
			internal readonly Func<T, Rect> getRectFunc;
			internal readonly QuadTree<T> parent;

			public Node(Rect boundary, int depth, Func<T, Rect> getRectFunc, QuadTree<T> parent)
			{
				this.boundary = boundary;
				this.depth = depth;
				this.getRectFunc = getRectFunc;
				this.parent = parent;
				this.children = null;
			}

			/// <summary>
			/// 插入元素
			/// </summary>
			public bool Insert(T item)
			{
				Rect itemRect = getRectFunc(item);

				// 如果元素与当前节点不重叠，无法插入
				if (!IsOverlapping(boundary, itemRect))
					return false;

				// 如果是叶子节点且未达容量上限
				if (objects != null && (objects.Count < parent.maxObjectsPerNode || depth >= parent.maxDepth))
				{
					objects.Add(item);
					parent.Count++;
					return true;
				}

				// 分裂节点
				if (children == null)
					Split();

				// 插入到子节点
				bool inserted = false;
				foreach (var child in children)
				{
					if (child.Insert(item))
						inserted = true;
				}

				return inserted;
			}

			/// <summary>
			/// 分裂为四个子节点
			/// </summary>
			private void Split()
			{
				float halfWidth = boundary.width / 2;
				float halfHeight = boundary.height / 2;
				float midX = boundary.x + halfWidth;
				float midY = boundary.y + halfHeight;

				children = new Node[4];
				children[0] = new Node(new Rect(boundary.x, boundary.y, halfWidth, halfHeight), depth + 1, getRectFunc, parent);          // 左上
				children[1] = new Node(new Rect(midX, boundary.y, halfWidth, halfHeight), depth + 1, getRectFunc, parent);               // 右上
				children[2] = new Node(new Rect(boundary.x, midY, halfWidth, halfHeight), depth + 1, getRectFunc, parent);               // 左下
				children[3] = new Node(new Rect(midX, midY, halfWidth, halfHeight), depth + 1, getRectFunc, parent);                    // 右下

				// 移动当前节点的对象到子节点
				var tempObjects = new List<T>(objects);
				objects.Clear();
				objects = null; // 标记为非叶子节点

				foreach (var obj in tempObjects)
				{
					Insert(obj);
				}
			}

			/// <summary>
			/// 查找最近的元素
			/// </summary>
			public void FindNearest(Vector2 target, ref T nearest, ref float minDistanceSquared)
			{
				// 如果当前节点距离大于已知最小距离，直接返回
				float distanceToBoundary = GetDistanceToRectSquared(target, boundary);
				if (distanceToBoundary >= minDistanceSquared)
					return;

				// 检查当前节点中的对象
				if (objects != null)
				{
					foreach (var obj in objects)
					{
						Rect objRect = getRectFunc(obj);
						float distanceSquared = GetDistanceToRectSquared(target, objRect);

						if (distanceSquared < minDistanceSquared)
						{
							minDistanceSquared = distanceSquared;
							nearest = obj;
						}
					}
				}

				// 递归检查子节点
				if (children != null)
				{
					// 按距离排序子节点，优先检查更近的
					var childDistances = new List<Tuple<Node, float>>();
					foreach (var child in children)
					{
						float dist = child.GetDistanceToRectSquared(target, child.boundary);
						childDistances.Add(Tuple.Create(child, dist));
					}

					// 按距离升序排序
					childDistances.Sort((a, b) => a.Item2.CompareTo(b.Item2));

					foreach (var tuple in childDistances)
					{
						if (tuple.Item2 < minDistanceSquared)
						{
							tuple.Item1.FindNearest(target, ref nearest, ref minDistanceSquared);
						}
					}
				}
			}

			/// <summary>
			/// 查找范围内的元素
			/// </summary>
			public HashSet<T> Retrieve(Rect area, HashSet<T> result)
			{
				if (result == null)
					result = new HashSet<T>();

				// 如果与当前节点不重叠，返回空集
				if (!IsOverlapping(boundary, area))
					return result;

				// 检查当前节点中的对象
				if (objects != null)
				{
					foreach (var obj in objects)
					{
						if (IsOverlapping(getRectFunc(obj), area))
						{
							result.Add(obj);
						}
					}
				}

				// 递归查询子节点
				if (children != null)
				{
					foreach (var child in children)
					{
						var childResults = child.Retrieve(area, result);
						foreach (var item in childResults)
						{
							result.Add(item);
						}
					}
				}

				return result;
			}

			/// <summary>
			/// 绘制节点网格
			/// </summary>
			public void DrawGrid(Color color)
			{
				Debug.DrawLine(new Vector2(boundary.x, boundary.y), new Vector2(boundary.xMax, boundary.y), color);
				Debug.DrawLine(new Vector2(boundary.x, boundary.y), new Vector2(boundary.x, boundary.yMax), color);
				Debug.DrawLine(new Vector2(boundary.xMax, boundary.y), new Vector2(boundary.xMax, boundary.yMax), color);
				Debug.DrawLine(new Vector2(boundary.x, boundary.yMax), new Vector2(boundary.xMax, boundary.yMax), color);

				if (children != null)
				{
					foreach (var child in children)
					{
						child.DrawGrid(color);
					}
				}
			}

			#region 辅助方法

			/// <summary>
			/// 判断两个矩形是否重叠
			/// </summary>
			private bool IsOverlapping(Rect a, Rect b)
			{
				return a.x < b.xMax && a.xMax > b.x &&
					   a.y < b.yMax && a.yMax > b.y;
			}

			/// <summary>
			/// 计算点到矩形的最小距离平方
			/// </summary>
			private float GetDistanceToRectSquared(Vector2 point, Rect rect)
			{
				float dx = Mathf.Max(rect.x - point.x, point.x - rect.xMax, 0);
				float dy = Mathf.Max(rect.y - point.y, point.y - rect.yMax, 0);
				return dx * dx + dy * dy;
			}

			/// <summary>
			/// 递归清空所有节点
			/// </summary>
			public void ClearAll()
			{
				if (objects != null)
				{
					objects.Clear();
				}

				if (children != null)
				{
					for (int i = 0; i < children.Length; i++)
					{
						children[i]?.ClearAll();
					}
					children = null;
				}

				objects = new HashSet<T>();
			}

			/// <summary>
			/// 递归收集所有元素
			/// </summary>
			public void CollectAllItems(HashSet<T> result)
			{
				if (objects != null)
				{
					foreach (var obj in objects)
					{
						result.Add(obj);
					}
					return;
				}

				if (children != null)
				{
					for (int i = 0; i < children.Length; i++)
					{
						children[i]?.CollectAllItems(result);
					}
				}
			}

			public IEnumerator<Node> GetEnumerator()
			{
				if (objects != null)
						yield return this;
				else
					foreach (var child in children)
						foreach (var node in child)
							yield return node;
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			#endregion
		}

		#endregion
	}

	/// <summary>
	/// 四叉树扩展方法，提供不同类型的矩形转换
	/// </summary>
	public static class QuadTreeExtensions
	{
		// Vector2 扩展 - 视为点
		public static Rect ToQuadTreeRect(this Vector2 vector)
		{
			return new Rect(vector, Vector2.zero);
		}

		// Vector3 扩展 - 使用X和Y坐标，视为点
		public static Rect ToQuadTreeRect(this Vector3 vector)
		{
			return new Rect(vector.x, vector.y, 0, 0);
		}

		// Rect 扩展 - 直接使用自身作为边界
		public static Rect ToQuadTreeRect(this Rect rect)
		{
			return rect;
		}

		// RectTransform 扩展 - 使用Rect范围
		public static Rect ToQuadTreeRect(this RectTransform transform)
		{
			return transform.rect;
		}

		// Transform 扩展 - 使用位置的X和Y坐标，视为点
		public static Rect ToQuadTreeRect(this Transform transform)
		{
			Vector3 pos = transform.position;
			return new Rect(pos.x, pos.y, 0, 0);
		}

		// 简化四叉树创建的扩展方法
		public static QuadTree<T> CreateQuadTree<T>(this IEnumerable<T> collection, Rect boundary, Func<T, Rect> getRectFunc)
		{
			var quadTree = new QuadTree<T>(boundary, getRectFunc);
			foreach (var item in collection)
			{
				quadTree.Insert(item);
			}
			return quadTree;
		}
	}

}
#endif