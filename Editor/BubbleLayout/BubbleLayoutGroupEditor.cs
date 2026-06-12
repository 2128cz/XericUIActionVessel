using UnityEditor;
using UnityEngine;
using XericUI.BubbleLayout;

namespace XericUIEditor.BubbleLayout
{
	[CustomEditor(typeof(BubbleLayoutGroup), true)]
	[CanEditMultipleObjects]
	internal class BubbleLayoutGroupEditor : Editor
	{
		// ── LayoutGroup 基类序列化属性 ──
		private SerializedProperty m_Padding;
		private SerializedProperty m_ChildAlignment;

		// ── BubbleLayoutGroup 序列化属性 ──
		private SerializedProperty m_EnablePlugins;
		private SerializedProperty m_PhysicsDamping;
		private SerializedProperty m_PhysicsMass;
		private SerializedProperty m_MassScaleWithArea;
		private SerializedProperty m_PhysicsDeltaTime;
		private SerializedProperty m_MaxIterations;
		private SerializedProperty m_Stiffness;
		private SerializedProperty m_MinSeparation;
		private SerializedProperty m_IterationDamping;
		private SerializedProperty m_LayoutIgnoreInactive;

		// ── 样式 ──
		private static GUIStyle s_PerfHeaderStyle;
		private static GUIStyle s_PerfLabelStyle;
		private static GUIStyle s_PerfValueStyle;
		private static bool s_StylesInitialized;

		private static void EnsureStyles()
		{
			if (s_StylesInitialized)
				return;

			s_PerfHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
			{
				fontSize = 11,
				normal = { textColor = new Color(0.75f, 0.75f, 0.75f) },
			};

			s_PerfLabelStyle = new GUIStyle(EditorStyles.label)
			{
				fontSize = 10,
				normal = { textColor = new Color(0.55f, 0.55f, 0.55f) },
				fixedWidth = 110,
			};

			s_PerfValueStyle = new GUIStyle(EditorStyles.label)
			{
				fontSize = 10,
				normal = { textColor = new Color(0.9f, 0.9f, 0.9f) },
				fixedWidth = 80,
				alignment = TextAnchor.MiddleRight,
			};

			s_StylesInitialized = true;
		}

		private void OnEnable()
		{
			m_Padding = serializedObject.FindProperty("m_Padding");
			m_ChildAlignment = serializedObject.FindProperty("m_ChildAlignment");

			m_EnablePlugins = serializedObject.FindProperty("m_EnablePlugins");
			m_PhysicsDamping = serializedObject.FindProperty("m_PhysicsDamping");
			m_PhysicsMass = serializedObject.FindProperty("m_PhysicsMass");
			m_MassScaleWithArea = serializedObject.FindProperty("m_MassScaleWithArea");
			m_PhysicsDeltaTime = serializedObject.FindProperty("m_PhysicsDeltaTime");
			m_MaxIterations = serializedObject.FindProperty("m_MaxIterations");
			m_Stiffness = serializedObject.FindProperty("m_Stiffness");
			m_MinSeparation = serializedObject.FindProperty("m_MinSeparation");
			m_IterationDamping = serializedObject.FindProperty("m_IterationDamping");
			m_LayoutIgnoreInactive = serializedObject.FindProperty("m_LayoutIgnoreInactive");
		}

		public override void OnInspectorGUI()
		{
			EnsureStyles();

			// ── 白色圆角边框 + 深色底 ──
			EditorGUILayout.BeginVertical(BubbleLayoutEditorStyles.BoxStyle);

			serializedObject.Update();

			// LayoutGroup 基类属性
			EditorGUILayout.LabelField("布局设置", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(m_Padding, true);
			EditorGUILayout.PropertyField(m_ChildAlignment);

			EditorGUILayout.Space(4);
			EditorGUILayout.LabelField("─ 气泡布局专属 ─", EditorStyles.boldLabel);

			// 插件
			EditorGUILayout.PropertyField(m_EnablePlugins);

			// 物理
			EditorGUILayout.LabelField("物理参数", EditorStyles.miniBoldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(m_PhysicsDamping);
			EditorGUILayout.PropertyField(m_PhysicsMass);
			EditorGUILayout.PropertyField(m_MassScaleWithArea);
			EditorGUILayout.PropertyField(m_PhysicsDeltaTime);
			EditorGUI.indentLevel--;

			// 碰撞排挤
			EditorGUILayout.LabelField("碰撞排挤", EditorStyles.miniBoldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(m_MaxIterations);
			EditorGUILayout.PropertyField(m_Stiffness);
			EditorGUILayout.PropertyField(m_MinSeparation);
			EditorGUILayout.PropertyField(m_IterationDamping);
			EditorGUI.indentLevel--;

			// 布局
			EditorGUILayout.PropertyField(m_LayoutIgnoreInactive);

			serializedObject.ApplyModifiedProperties();

			EditorGUILayout.EndVertical();

			// ── 性能指标 ──
			DrawPerformanceMetrics();
		}

		private void DrawPerformanceMetrics()
		{
			var layout = target as BubbleLayoutGroup;
			if (layout == null)
				return;

			var perf = layout.LastProfilerData;

			EditorGUILayout.Space(6);

			EditorGUILayout.BeginVertical(BubbleLayoutEditorStyles.BoxStyle);

			EditorGUILayout.LabelField("性能指标", s_PerfHeaderStyle);
			EditorGUILayout.Space(2);

			DrawPerfRow("元素数量", perf.ItemCount.ToString());
			DrawPerfRow("动画插件数", perf.AnimationPluginCount.ToString());
			DrawPerfRow("约束插件数", perf.ConstraintPluginCount.ToString());
			DrawPerfRow("碰撞迭代次数", perf.CollisionIterations.ToString());

			EditorGUILayout.Space(2);

			EditorGUILayout.LabelField("耗时明细 (ms)", EditorStyles.miniBoldLabel);

			DrawPerfRow("同步子元素", $"{perf.SyncTimeMs:F3}");
			DrawPerfRow("动画阶段", $"{perf.AnimationTimeMs:F3}");
			DrawPerfRow("物理积分", $"{perf.PhysicsTimeMs:F3}");
			DrawPerfRow("约束阶段", $"{perf.ConstraintTimeMs:F3}");
			DrawPerfRow("碰撞排挤", $"{perf.CollisionTimeMs:F3}");
			DrawPerfRow("位置应用", $"{perf.ApplyTimeMs:F3}");

			EditorGUILayout.Space(2);

			var totalStyle = new GUIStyle(s_PerfValueStyle)
			{
				fontStyle = FontStyle.Bold,
				normal = { textColor = new Color(0.3f, 1f, 0.5f) },
			};
			var totalLabelStyle = new GUIStyle(s_PerfLabelStyle) { fontStyle = FontStyle.Bold };

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("总耗时", totalLabelStyle);
			EditorGUILayout.LabelField($"{perf.TotalTimeMs:F3}", totalStyle);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.EndVertical();
		}

		private void DrawPerfRow(string label, string value)
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(label, s_PerfLabelStyle);
			EditorGUILayout.LabelField(value, s_PerfValueStyle);
			EditorGUILayout.EndHorizontal();
		}
	}
}
