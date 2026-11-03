using UnityEditor;
using UnityEditor.UI;

using UnityEngine;
using UnityEngine.UI;

using XericUI.OverrideUI;
using XericUI.Core;

namespace XericUIEditor.OverrideUI
{
	/// <summary>
	/// XericImage 的 Inspector 与场景编辑手柄。
	/// 场景手柄布局（每条边一套）：
	/// <code>
	///   圆点 --------- 圆点 - 横 - 圆点 --------- 圆点
	///   顶点          旋转   滑移  旋转          顶点
	/// </code>
	/// 顶点=自由拖拽该角点；横=沿这条边的方向平移两端点；圆点=绕这条边的中点旋转两端点（保持边长）。
	/// </summary>
	[CustomEditor(typeof(XericImage), true)]
	[CanEditMultipleObjects]
	public class XericImageEditor : ImageEditor
	{
		#region 常量与静态状态

		private const string SceneEditKey = "XericImage.SceneEdit";
		private const string SceneEditMenu = XericRegistrationPaths.UIBar + "/Image/场景变形手柄";

		private static bool s_SceneEdit;
		private static bool s_SceneEditLoaded;

		/// <summary>场景手柄总开关（默认关闭，避免选中任何 Image 都冒出手柄）</summary>
		private static bool SceneEditEnabled
		{
			get
			{
				if (!s_SceneEditLoaded)
				{
					s_SceneEdit = EditorPrefs.GetBool(SceneEditKey, false);
					s_SceneEditLoaded = true;
				}
				return s_SceneEdit;
			}
			set
			{
				s_SceneEdit = value;
				s_SceneEditLoaded = true;
				EditorPrefs.SetBool(SceneEditKey, value);
				SceneView.RepaintAll();
			}
		}

		[MenuItem(SceneEditMenu, false, 300)]
		private static void ToggleSceneEdit() => SceneEditEnabled = !SceneEditEnabled;

		[MenuItem(SceneEditMenu, true)]
		private static bool ToggleSceneEditValidate()
		{
			Menu.SetChecked(SceneEditMenu, SceneEditEnabled);
			return true;
		}

		#endregion

		#region 字段

		private SerializedProperty m_OffsetBL;
		private SerializedProperty m_OffsetTL;
		private SerializedProperty m_OffsetTR;
		private SerializedProperty m_OffsetBR;

		// 生成器参数（编辑器侧状态，不进序列化）
		private bool m_ShowGenerator;
		private bool m_KeepParallelogram;
		private Matrix4x4 m_Matrix = Matrix4x4.identity;
		private Vector3 m_PlaneNormal = Vector3.back;
		private float m_PlaneRoll;

		// 一轮操作只登记一条撤销：登记过就按住，等这一轮操作结束再放开
		private bool m_UndoLatched;

		#endregion

		#region 生命周期

		protected override void OnEnable()
		{
			base.OnEnable();

			m_OffsetBL = serializedObject.FindProperty("m_CornerOffsetBL");
			m_OffsetTL = serializedObject.FindProperty("m_CornerOffsetTL");
			m_OffsetTR = serializedObject.FindProperty("m_CornerOffsetTR");
			m_OffsetBR = serializedObject.FindProperty("m_CornerOffsetBR");
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			m_UndoLatched = false;
		}

		#endregion

		#region Inspector

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			DrawDeformSection();

			// 拖动数值框时每帧都会改值，鼠标松开才算这一轮操作结束
			if (Event.current.type == EventType.MouseUp)
				m_UndoLatched = false;
		}

		private void DrawDeformSection()
		{
			serializedObject.Update();

			EditorGUILayout.Space(8);
			EditorGUILayout.LabelField("变形（四顶点）", EditorStyles.boldLabel);

			SceneEditEnabled = EditorGUILayout.ToggleLeft(
				"场景手柄编辑（选中时在 Scene 视图拖拽顶点与边）", SceneEditEnabled);

			m_KeepParallelogram = EditorGUILayout.ToggleLeft(
				"拖拽顶点时保持平行四边形", m_KeepParallelogram);

			EditorGUILayout.Space(2);
			DrawCornerFields();

			EditorGUILayout.Space(4);
			m_ShowGenerator = EditorGUILayout.Foldout(m_ShowGenerator, "生成器（矩阵 / 虚拟法线）", true);
			if (m_ShowGenerator)
			{
				EditorGUI.indentLevel++;
				DrawMatrixGenerator();
				DrawPlaneGenerator();
				EditorGUI.indentLevel--;
			}

			EditorGUILayout.Space(4);
			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("复制角点"))
					CopyCorners();
				if (GUILayout.Button("粘贴角点"))
					PasteCorners();
				if (GUILayout.Button("重置变形"))
					ApplyToTargets(img => img.ResetDeform(), "重置 Image 变形", recordSerialized: true);
			}

			serializedObject.ApplyModifiedProperties();
		}

		private void DrawCornerFields()
		{
			var image = target as XericImage;
			if (image == null) return;

			EditorGUI.BeginChangeCheck();
			Vector2 bl = EditorGUILayout.Vector2Field("左下 (0,0)", image.GetCorner(0));
			Vector2 tl = EditorGUILayout.Vector2Field("左上 (0,1)", image.GetCorner(1));
			Vector2 tr = EditorGUILayout.Vector2Field("右上 (1,1)", image.GetCorner(2));
			Vector2 br = EditorGUILayout.Vector2Field("右下 (1,0)", image.GetCorner(3));
			if (EditorGUI.EndChangeCheck())
			{
				RecordTargetsUndo("编辑 Image 变形");
				foreach (Object t in targets)
				{
					if (t is XericImage img)
						img.SetCorners(bl, tl, tr, br);
				}
				// 上面走的是字段直改，SerializedObject 的缓存要重新读一遍
				serializedObject.Update();
			}
		}

		private void DrawMatrixGenerator()
		{
			for (int r = 0; r < 4; r++)
			{
				EditorGUI.BeginChangeCheck();
				Vector4 row = EditorGUILayout.Vector4Field(r == 0 ? "矩阵 (4x4)" : " ", m_Matrix.GetRow(r));
				if (EditorGUI.EndChangeCheck())
					m_Matrix.SetRow(r, row);
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("应用矩阵（2D 仿射）"))
					ApplyToTargets(img => img.ApplyAffine(m_Matrix), "应用仿射矩阵");
				if (GUILayout.Button("应用矩阵（伪投影）"))
					ApplyToTargets(img => img.ApplyProjected(m_Matrix), "应用伪投影矩阵");
			}
		}

		private void DrawPlaneGenerator()
		{
			m_PlaneNormal = EditorGUILayout.Vector3Field("虚拟法线", m_PlaneNormal);
			m_PlaneRoll = EditorGUILayout.Slider("绕法线旋转", m_PlaneRoll, -180f, 180f);

			if (GUILayout.Button("应用法线（拟合平面，仿射）"))
				ApplyToTargets(img => img.ApplyPlane(m_PlaneNormal, m_PlaneRoll), "应用平面法线");
		}

		#endregion

		#region Inspector 辅助

		private void RecordTargetsUndo(string undoName)
			=> Undo.RecordObjects(targets, undoName);

		private void ApplyToTargets(System.Action<XericImage> action, string undoName, bool recordSerialized = false)
		{
			if (recordSerialized)
				RecordTargetsUndo(undoName + "（含 Inspector 值）");
			else
				RecordTargetsUndo(undoName);

			foreach (Object t in targets)
			{
				if (t is XericImage img)
					action(img);
			}

			serializedObject.Update();
			SceneView.RepaintAll();
		}

		private const string CornersClipboardKey = "XericImage.Corners";

		private void CopyCorners()
		{
			var image = target as XericImage;
			if (image == null) return;

			Vector2[] c = image.GetCorners();
			EditorPrefs.SetString(CornersClipboardKey, string.Format(
				"{0:F6}|{1:F6}|{2:F6}|{3:F6}|{4:F6}|{5:F6}|{6:F6}|{7:F6}",
				c[0].x, c[0].y, c[1].x, c[1].y, c[2].x, c[2].y, c[3].x, c[3].y));
		}

		private void PasteCorners()
		{
			string raw = EditorPrefs.GetString(CornersClipboardKey, string.Empty);
			if (string.IsNullOrEmpty(raw)) return;

			string[] parts = raw.Split('|');
			if (parts.Length != 8) return;

			var values = new float[8];
			for (int i = 0; i < 8; i++)
			{
				float v;
				if (!float.TryParse(parts[i], System.Globalization.NumberStyles.Float,
						System.Globalization.CultureInfo.InvariantCulture, out v))
					return;
				values[i] = v;
			}

			ApplyToTargets(img => img.SetCorners(
				new Vector2(values[0], values[1]),
				new Vector2(values[2], values[3]),
				new Vector2(values[4], values[5]),
				new Vector2(values[6], values[7])), "粘贴 Image 变形");
		}

		#endregion

		#region 场景手柄

		/// <summary>九宫格线框的格数（3 = 横竖各两条内线）</summary>
		private const int GridDivisions = 3;

		/// <summary>滑移手柄横杠的朝向（供给 DashCap，绘制时逐边设置）</summary>
		private static Vector3 s_DashDirection = Vector3.right;

		private void OnSceneGUI()
		{
			if (!SceneEditEnabled) return;

			var image = target as XericImage;
			if (image == null) return;

			Rect rect = image.GetPixelAdjustedRect();
			if (rect.width <= 0f || rect.height <= 0f) return;

			Transform tr = image.transform;
			Vector3 planeNormal = tr.forward;

			var world = new Vector3[4];
			var normalized = new Vector2[4];
			for (int i = 0; i < 4; i++)
			{
				normalized[i] = image.GetCorner(i);
				world[i] = tr.TransformPoint(image.NormalizedToLocal(normalized[i]));
			}

			Handles.color = new Color(0.35f, 0.75f, 1f, 0.85f);
			Handles.DrawAAPolyLine(2f, world[0], world[1], world[2], world[3], world[0]);

			DrawDeformGrid(image, tr);
			DrawEdgeHandles(image, rect, tr, world, normalized);
			DrawVertexHandles(image, tr, world, planeNormal);

			// 一轮拖动结束才放开撤销闩锁
			if (Event.current.type == EventType.MouseUp || Event.current.type == EventType.Ignore)
				m_UndoLatched = false;
		}

		/// <summary>
		/// 九宫格线框：画的是单位正方形网格线在变形映射下的像。
		/// 单应把直线映成直线，所以每条线取两个端点即可精确；半透明的面上靠它看形变。
		/// </summary>
		private void DrawDeformGrid(XericImage image, Transform tr)
		{
			// 与 OnPopulateMesh 走同一个 XericQuadMapper：线框和网格必须在同一套映射下
			if (!image.TryGetDeformMapper(out XericQuadMapper mapper))
				return;

			Handles.color = new Color(0.35f, 0.75f, 1f, 0.32f);

			for (int i = 1; i < GridDivisions; i++)
			{
				float t = (float)i / GridDivisions;

				Handles.DrawAAPolyLine(1f,
					tr.TransformPoint(mapper.MapNormalized(new Vector2(t, 0f))),
					tr.TransformPoint(mapper.MapNormalized(new Vector2(t, 1f))));
				Handles.DrawAAPolyLine(1f,
					tr.TransformPoint(mapper.MapNormalized(new Vector2(0f, t))),
					tr.TransformPoint(mapper.MapNormalized(new Vector2(1f, t))));
			}
		}

		/// <summary>逐边绘制：滑移手柄（沿边方向平移两端点）与两个旋转手柄（绕边中点保持边长旋转）</summary>
		private void DrawEdgeHandles(XericImage image, Rect rect, Transform tr, Vector3[] world, Vector2[] normalized)
		{
			for (int e = 0; e < 4; e++)
			{
				int ia = e;
				int ib = (e + 1) % 4;

				Vector3 a = world[ia];
				Vector3 b = world[ib];
				Vector3 mid = (a + b) * 0.5f;

				Vector3 dir = b - a;
				float length = dir.magnitude;
				if (length < 1e-6f) continue;
				dir /= length;

				float handleScale = HandleUtility.GetHandleSize(mid);

				DrawSlideHandle(image, rect, tr, ia, ib, mid, dir, handleScale);
				DrawRotateHandles(image, tr, normalized, ia, ib, mid, dir, handleScale);
			}
		}

		private void DrawSlideHandle(XericImage image, Rect rect, Transform tr, int ia, int ib,
			Vector3 mid, Vector3 dir, float handleScale)
		{
			s_DashDirection = dir;
			Handles.color = new Color(1f, 0.85f, 0.25f, 0.95f);

			EditorGUI.BeginChangeCheck();
			Vector3 moved = Handles.Slider(mid, dir, handleScale * 0.11f, DashCap, 0f);
			if (!EditorGUI.EndChangeCheck()) return;

			// 沿边方向平移：两端点吃同一个位移
			Vector3 worldDelta = moved - mid;
			Vector2 localDelta = tr.InverseTransformVector(worldDelta);
			var normalizedDelta = new Vector2(localDelta.x / rect.width, localDelta.y / rect.height);
			if (normalizedDelta == Vector2.zero) return;

			Vector2[] corners = image.GetCorners();
			corners[ia] += normalizedDelta;
			corners[ib] += normalizedDelta;
			PushCorners(corners);
		}

		private void DrawRotateHandles(XericImage image, Transform tr, Vector2[] normalized,
			int ia, int ib, Vector3 mid, Vector3 dir, float handleScale)
		{
			// 旋转在局部坐标里算：归一化空间两轴比例不同，直接在那里面旋转会把形状拧歪
			Vector2 localA = image.NormalizedToLocal(normalized[ia]);
			Vector2 localB = image.NormalizedToLocal(normalized[ib]);
			Vector2 midLocal = (localA + localB) * 0.5f;
			float halfLengthLocal = Vector2.Distance(localA, localB) * 0.5f;
			if (halfLengthLocal < 1e-6f) return;

			Handles.color = new Color(0.4f, 1f, 0.6f, 0.95f);
			float offset = handleScale * 0.30f;

			for (int side = -1; side <= 1; side += 2)
			{
				Vector3 handlePos = mid + dir * (offset * side);

				EditorGUI.BeginChangeCheck();
				// 用"五个参数 + 显式旋转"那一档：它在 2021.1 … 6.x 都存在（四参数那档在旧版本里没有，
				// 他 2026-10-26 的编辑器报过 CS1501 "FreeMoveHandle 没有 4 参数的重载"）。
				// 2022.1 起这一档被标了 Obsolete（"Rotation parameter is obsolete"），但同名的四参数版
				// 在更老的版本里不存在 ⇒ 为了让同一个包在他所有工程都编得过，这里显式压掉 618，
				// 而不是二选一（选四参数＝旧工程编不过，选五参数不压警告＝门把警告记成诊断）。
#pragma warning disable 618
				Vector3 dragged = Handles.FreeMoveHandle(handlePos, Quaternion.identity,
					handleScale * 0.07f, Vector3.zero, Handles.DotHandleCap);
#pragma warning restore 618
				if (!EditorGUI.EndChangeCheck()) continue;
				if (dragged == handlePos) continue;

				Vector3 dragLocal3 = tr.InverseTransformPoint(dragged);
				var dragLocal = new Vector2(dragLocal3.x, dragLocal3.y) - midLocal;
				if (dragLocal.sqrMagnitude < 1e-12f) continue;
				dragLocal.Normalize();

				// 以当前边的长度和中点，把这条边旋到指向拖拽点的方向
				Vector2[] corners = image.GetCorners();
				corners[ia] = image.LocalToNormalized(midLocal - dragLocal * halfLengthLocal);
				corners[ib] = image.LocalToNormalized(midLocal + dragLocal * halfLengthLocal);
				PushCorners(corners);
			}
		}

		/// <summary>逐顶点绘制自由拖拽手柄（限制在组件自身的 XY 平面内）</summary>
		private void DrawVertexHandles(XericImage image, Transform tr, Vector3[] world, Vector3 planeNormal)
		{
			Handles.color = new Color(1f, 1f, 1f, 0.95f);

			for (int i = 0; i < 4; i++)
			{
				Vector3 pos = world[i];
				float size = HandleUtility.GetHandleSize(pos) * 0.075f;

				EditorGUI.BeginChangeCheck();
				Vector3 moved = Handles.Slider2D(pos, planeNormal, tr.right, tr.up, size,
					Handles.SphereHandleCap, Vector2.zero);
				if (!EditorGUI.EndChangeCheck()) continue;

				Vector2 local = tr.InverseTransformPoint(moved);
				Vector2 target = image.LocalToNormalized(local);
				if (target == image.GetCorner(i)) continue;

				Vector2[] corners = image.GetCorners();
				if (m_KeepParallelogram && IsParallelogram(corners))
				{
					// 平行四边形：对角点的位移必须与当前角点一致
					corners[(i + 2) % 4] += target - corners[i];
				}
				corners[i] = target;
				PushCorners(corners);
			}
		}

		/// <summary>平行四边形判定：对角点连线的中点重合</summary>
		private static bool IsParallelogram(Vector2[] corners)
		{
			Vector2 d = corners[0] + corners[2] - corners[1] - corners[3];
			return d.sqrMagnitude < 1e-8f;
		}

		/// <summary>
		/// 场景手柄写回。只动 <see cref="Editor.target"/>：
		/// OnSceneGUI 里读 targets 数组是非法的（Unity 会直接报错），
		/// 而多选时每个选中对象的 Editor 都会各自收到一次 OnSceneGUI，天然就是各改各的。
		/// </summary>
		private void PushCorners(Vector2[] corners)
		{
			var image = target as XericImage;
			if (image == null) return;

			// 一轮操作只登记一条撤销，否则拖动时一帧一条会把撤销栈刷满
			if (!m_UndoLatched)
			{
				Undo.RecordObject(image, "变形 Image 顶点");
				m_UndoLatched = true;
			}

			image.SetCorners(corners[0], corners[1], corners[2], corners[3]);
			EditorUtility.SetDirty(image);
			SceneView.RepaintAll();
		}

		/// <summary>滑移手柄的横杠：沿当前边的方向画一段粗线</summary>
		private static void DashCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
		{
			if (eventType != EventType.Repaint) return;
			Vector3 half = s_DashDirection * size;
			Handles.DrawAAPolyLine(4f, position - half, position + half);
		}

		#endregion
	}
}