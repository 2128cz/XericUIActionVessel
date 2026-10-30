using UnityEngine;
using UnityEngine.UI;
using XericUI.Core;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 向量值组件 — 处理 Vector2 / Vector3 / Vector4 类型的多字段输入
    /// 子节点按 BindingSlots 绑定各分量（如 "x", "y", "z", "w"）
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.UI + "/Values/XUIAvrgMDVectorValue")]
    public class XUIAvrgMDVectorValue : XUIAvrgMDValue
    {
        [SerializeField, Tooltip("X 分量 InputField（可选，也可通过 BindingSlots 的子节点绑定）")]
        private InputField _inputX;

        [SerializeField, Tooltip("Y 分量 InputField")]
        private InputField _inputY;

        [SerializeField, Tooltip("Z 分量 InputField（Vector3/Vector4 时使用）")]
        private InputField _inputZ;

        [SerializeField, Tooltip("W 分量 InputField（Vector4 时使用）")]
        private InputField _inputW;

        // ============ 对象池生命周期 ============

        public override void OnPoolGet(AvrgMemberMetaData metaData)
        {
            base.OnPoolGet(metaData);

            int componentCount = GetComponentCount(metaData?.ValueType);
            SetInputActive(_inputX, 0, componentCount);
            SetInputActive(_inputY, 1, componentCount);
            SetInputActive(_inputZ, 2, componentCount);
            SetInputActive(_inputW, 3, componentCount);
        }

        public override void OnPoolRelease()
        {
            if (_inputX != null) _inputX.onEndEdit.RemoveAllListeners();
            if (_inputY != null) _inputY.onEndEdit.RemoveAllListeners();
            if (_inputZ != null) _inputZ.onEndEdit.RemoveAllListeners();
            if (_inputW != null) _inputW.onEndEdit.RemoveAllListeners();

            base.OnPoolRelease();
        }

        // ============ 核心绑定 ============

        public override void BindToAccessor(
            IAvrgValueAccessor accessor,
            AvrgMvvmBinding binding,
            AvrgMvvmScheduler scheduler)
        {
            CurrentAccessor = accessor;
            CurrentBinding = binding;

            if (binding == null || accessor == null) return;

            // 为每个输入注册事件
            BindInputField(_inputX, 0, accessor, binding, scheduler);
            BindInputField(_inputY, 1, accessor, binding, scheduler);
            BindInputField(_inputZ, 2, accessor, binding, scheduler);
            BindInputField(_inputW, 3, accessor, binding, scheduler);

            // 数据 → UI
            binding.OnUIDataChanged += (value, state) =>
            {
                UpdateAllInputs(value);
                base.UpdateValueDisplay(value, state);
            };

            binding.OnValidationStateChanged += OnValidationStateChanged;

            // 首次同步
            var initialValue = accessor.GetValue();
            UpdateAllInputs(initialValue);
            UpdateValueDisplay(initialValue, ValidationState.Normal);
        }

        // ============ 输入绑定 ============

        private void BindInputField(InputField input, int componentIndex,
            IAvrgValueAccessor accessor, AvrgMvvmBinding binding, AvrgMvvmScheduler scheduler)
        {
            if (input == null) return;

            input.onEndEdit.AddListener(val =>
            {
                if (float.TryParse(val, out float num))
                {
                    var currentVal = accessor.GetValue();
                    var modified = SetVectorComponent(currentVal, componentIndex, num);
                    binding.WriteFromUI(modified);
                    scheduler?.MarkUIDirty(binding);
                }
            });
        }

        private void UpdateAllInputs(object value)
        {
            if (value == null) return;

            if (_inputX != null && _inputX.gameObject.activeSelf)
                _inputX.SetTextWithoutNotify(GetVectorComponent(value, 0).ToString("F2"));

            if (_inputY != null && _inputY.gameObject.activeSelf)
                _inputY.SetTextWithoutNotify(GetVectorComponent(value, 1).ToString("F2"));

            if (_inputZ != null && _inputZ.gameObject.activeSelf)
                _inputZ.SetTextWithoutNotify(GetVectorComponent(value, 2).ToString("F2"));

            if (_inputW != null && _inputW.gameObject.activeSelf)
                _inputW.SetTextWithoutNotify(GetVectorComponent(value, 3).ToString("F2"));
        }

        private static int GetComponentCount(System.Type type)
        {
            if (type == typeof(Vector2)) return 2;
            if (type == typeof(Vector3)) return 3;
            if (type == typeof(Vector4)) return 4;
            return 0;
        }

        private static float GetVectorComponent(object vec, int index)
        {
            return vec switch
            {
                Vector2 v2 => index == 0 ? v2.x : v2.y,
                Vector3 v3 => index == 0 ? v3.x : index == 1 ? v3.y : v3.z,
                Vector4 v4 => index == 0 ? v4.x : index == 1 ? v4.y : index == 2 ? v4.z : v4.w,
                _ => 0f
            };
        }

        private static object SetVectorComponent(object vec, int index, float value)
        {
            return vec switch
            {
                Vector2 v2 => index == 0 ? new Vector2(value, v2.y) : new Vector2(v2.x, value),
                Vector3 v3 => index == 0 ? new Vector3(value, v3.y, v3.z)
                    : index == 1 ? new Vector3(v3.x, value, v3.z) : new Vector3(v3.x, v3.y, value),
                Vector4 v4 => index == 0 ? new Vector4(value, v4.y, v4.z, v4.w)
                    : index == 1 ? new Vector4(v4.x, value, v4.z, v4.w)
                    : index == 2 ? new Vector4(v4.x, v4.y, value, v4.w)
                    : new Vector4(v4.x, v4.y, v4.z, value),
                _ => vec
            };
        }

        private static void SetInputActive(InputField input, int index, int componentCount)
        {
            if (input != null)
                input.gameObject.SetActive(index < componentCount);
        }
    }
}
