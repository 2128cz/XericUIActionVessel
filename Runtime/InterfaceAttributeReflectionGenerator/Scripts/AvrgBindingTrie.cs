using System.Collections.Generic;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 绑定名称前缀树（Trie），支持通过完整路径或成员名后缀快速查找 AvrgMvvmBinding。
    /// 
    /// 使用场景：
    /// - Find("XericUI.MyData.speed")  → 完整路径精确命中
    /// - FindByMemberName("speed")       → 沿 Trie 叶节点查找末段匹配
    /// 
    /// 在 BuildMDVM 时构建，绑定到 AvrgMvvmBindingCollection。
    /// </summary>
    internal class AvrgBindingTrie
    {
        private class Node
        {
            public AvrgMvvmBinding Binding;
            public readonly Dictionary<string, Node> Children = new();
        }

        private readonly Node _root = new();
        private readonly Dictionary<string, AvrgMvvmBinding> _memberNameIndex = new();

        public void Insert(string fullPath, AvrgMvvmBinding binding)
        {
            if (string.IsNullOrEmpty(fullPath) || binding == null) return;

            var parts = fullPath.Split('.');
            var current = _root;
            foreach (var part in parts)
            {
                if (!current.Children.TryGetValue(part, out var child))
                {
                    child = new Node();
                    current.Children[part] = child;
                }
                current = child;
            }
            current.Binding = binding;

            // 同时建成员名索引（末段）
            string memberName = parts[parts.Length - 1];
            if (!_memberNameIndex.ContainsKey(memberName))
                _memberNameIndex[memberName] = binding;
        }

        /// <summary>通过完整路径精确查找（如 "XericUI.MyData.speed"）</summary>
        public AvrgMvvmBinding Find(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return null;

            var parts = fullPath.Split('.');
            var current = _root;
            foreach (var part in parts)
            {
                if (!current.Children.TryGetValue(part, out current))
                    return null;
            }
            return current?.Binding;
        }

        /// <summary>通过成员名查找（如 "speed"）— O(1) 直接索引</summary>
        public AvrgMvvmBinding FindByMemberName(string memberName)
        {
            _memberNameIndex.TryGetValue(memberName, out var binding);
            return binding;
        }

        /// <summary>清除所有索引</summary>
        public void Clear()
        {
            _root.Children.Clear();
            _memberNameIndex.Clear();
        }
    }
}
