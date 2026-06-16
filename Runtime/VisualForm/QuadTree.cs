using System;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.VisualForm
{
    /// <summary>
    /// 轻量泛型四叉树，用于空间加速碰撞检测。
    /// 通过 Func{T, Rect} 委托获取任意类型对象的边界矩形。
    /// 委托在每次查询时实时调用，因此对象可在树重建后移动位置，查询仍能获得准确结果。
    /// 
    /// 典型用法：
    ///   1. 构造 QuadTree{T}(bounds, getRectFunc)
    ///   2. Rebuild(items) — 清空并重新插入
    ///   3. Retrieve(area, result) — 查询与area重叠的所有对象
    /// </summary>
    public class QuadTree<T>
    {
        private readonly int _maxPerNode;
        private readonly int _maxDepth;
        private readonly Func<T, Rect> _getRectFunc;

        private Node _root;

        public int Count { get; private set; }

        /// <summary>
        /// 初始化四叉树。
        /// </summary>
        /// <param name="boundary">根节点边界矩形</param>
        /// <param name="getRectFunc">获取对象边界的委托（每次查询实时调用）</param>
        /// <param name="maxPerNode">每节点最大容纳对象数</param>
        /// <param name="maxDepth">最大递归深度</param>
        public QuadTree(Rect boundary, Func<T, Rect> getRectFunc, int maxPerNode = 5, int maxDepth = 4)
        {
            _maxPerNode = Math.Max(1, maxPerNode);
            _maxDepth = Math.Max(1, maxDepth);
            _getRectFunc = getRectFunc ?? throw new ArgumentNullException(nameof(getRectFunc));
            _root = new Node(boundary, 0, this);
        }

        /// <summary>
        /// 清空四叉树，保留边界和配置以便重用。
        /// </summary>
        public void Clear()
        {
            _root.ClearAll();
            Count = 0;
        }

        /// <summary>
        /// 清空并重新插入所有对象。
        /// </summary>
        public void Rebuild(IEnumerable<T> items)
        {
            Clear();
            foreach (var item in items)
                Insert(item);
        }

        /// <summary>
        /// 插入一个对象。
        /// </summary>
        public bool Insert(T item)
        {
            return _root.Insert(item);
        }

        /// <summary>
        /// 查询与指定区域重叠的所有对象。
        /// </summary>
        /// <param name="area">查询区域</param>
        /// <param name="result">结果容器（会先清空再填入）</param>
        public void Retrieve(Rect area, HashSet<T> result)
        {
            if (result == null) return;
            result.Clear();
            _root.Retrieve(area, result);
        }

        #region 内部节点

        private class Node
        {
            private readonly Rect _boundary;
            private readonly int _depth;
            private readonly QuadTree<T> _owner;
            private HashSet<T> _objects;
            private Node[] _children;

            public Node(Rect boundary, int depth, QuadTree<T> owner)
            {
                _boundary = boundary;
                _depth = depth;
                _owner = owner;
                _objects = new HashSet<T>();
            }

            public bool Insert(T item)
            {
                Rect itemRect = _owner._getRectFunc(item);
                if (!Overlaps(_boundary, itemRect))
                    return false;

                if (_objects != null && (_objects.Count < _owner._maxPerNode || _depth >= _owner._maxDepth))
                {
                    _objects.Add(item);
                    _owner.Count++;
                    return true;
                }

                if (_children == null)
                    Split();

                bool inserted = false;
                for (int i = 0; i < 4; i++)
                {
                    if (_children[i].Insert(item))
                        inserted = true;
                }

                return inserted;
            }

            private void Split()
            {
                float hw = _boundary.width * 0.5f;
                float hh = _boundary.height * 0.5f;
                float mx = _boundary.x + hw;
                float my = _boundary.y + hh;

                _children = new Node[4];
                _children[0] = new Node(new Rect(_boundary.x, _boundary.y, hw, hh), _depth + 1, _owner);
                _children[1] = new Node(new Rect(mx, _boundary.y, hw, hh), _depth + 1, _owner);
                _children[2] = new Node(new Rect(_boundary.x, my, hw, hh), _depth + 1, _owner);
                _children[3] = new Node(new Rect(mx, my, hw, hh), _depth + 1, _owner);

                var temp = new List<T>(_objects);
                _objects.Clear();
                _objects = null;

                for (int i = 0; i < temp.Count; i++)
                    Insert(temp[i]);
            }

            public void Retrieve(Rect area, HashSet<T> result)
            {
                if (!Overlaps(_boundary, area))
                    return;

                if (_objects != null)
                {
                    foreach (var obj in _objects)
                    {
                        if (Overlaps(_owner._getRectFunc(obj), area))
                            result.Add(obj);
                    }
                    return;
                }

                if (_children != null)
                {
                    for (int i = 0; i < 4; i++)
                        _children[i].Retrieve(area, result);
                }
            }

            public void ClearAll()
            {
                if (_objects != null)
                {
                    _objects.Clear();
                }

                if (_children != null)
                {
                    for (int i = 0; i < _children.Length; i++)
                        _children[i]?.ClearAll();
                    _children = null;
                }

                _objects = new HashSet<T>();
            }

            private static bool Overlaps(Rect a, Rect b)
            {
                return a.xMin < b.xMax && a.xMax > b.xMin &&
                       a.yMin < b.yMax && a.yMax > b.yMin;
            }
        }

        #endregion
    }
}
