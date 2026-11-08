#if UNITY_2022_1_OR_NEWER
using BaseFloatField = UnityEngine.UIElements.FloatField;
using BaseIntegerField = UnityEngine.UIElements.IntegerField;
using BaseEnumField = UnityEngine.UIElements.EnumField;
#else
using BaseFloatField = UnityEditor.UIElements.FloatField;
using BaseIntegerField = UnityEditor.UIElements.IntegerField;
using BaseEnumField = UnityEditor.UIElements.EnumField;
#endif
using UnityEngine.UIElements;

namespace XericUI.ShaderBake.Editor
{
	/// <summary>
	/// 这三个数字/枚举输入框在 2021.3 与 2022.1 分属两个命名空间：
	/// 2021.3 在 <c>UnityEditor.UIElements</c>，2022.1 挪到了 <c>UnityEngine.UIElements</c>。
	///
	/// <para><b>为什么还要包一层</b>：UXML 的标签前缀只能指向一个固定命名空间，写
	/// <c>ui:FloatField</c> 在 2021.3 里会报 "has no registered factory method"，写
	/// <c>x:FloatField</c> 又在 2022.1 里失效。包一个自己命名空间下的同名子类，
	/// 两份 UXML 就能共用同一个 <c>xer:FloatField</c> 标签，版本差异只留在 C# 的 using 别名里。</para>
	///
	/// <para>子类不做任何事，只负责把基类的 <c>UxmlTraits</c>（label/value 等）和工厂带过来。</para>
	/// </summary>
	public class FloatField : BaseFloatField
	{
		public new class UxmlFactory : UxmlFactory<FloatField, UxmlTraits> { }
	}

	/// <inheritdoc cref="FloatField"/>
	public class IntegerField : BaseIntegerField
	{
		public new class UxmlFactory : UxmlFactory<IntegerField, UxmlTraits> { }
	}

	/// <inheritdoc cref="FloatField"/>
	public class EnumField : BaseEnumField
	{
		public new class UxmlFactory : UxmlFactory<EnumField, UxmlTraits> { }
	}
}
