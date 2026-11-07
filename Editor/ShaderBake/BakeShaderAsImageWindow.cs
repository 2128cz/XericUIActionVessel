using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XericUI.Core;

namespace XericUI.ShaderBake.Editor
{
	/// <summary>
	/// Bake Shader As Image 窗口：把 shader / 材质渲成图，右侧调参数与材质属性，左侧实时看结果，
	/// 顶部导出成文件或 Unity 资产。
	///
	/// <para><b>界面结构在 UXML，这里只做接线</b>：控件按名字取，每个绑定同时登记一个
	/// "把设置里的值写回这个控件"的同步动作（<see cref="SyncUiFromSettings"/>）。
	/// 参数每次提交后都走一遍 Clamp 再整体回写，所以界面显示的永远是真正生效的那个值，
	/// 而不是用户敲进去的那个数——这条性质只在"同步动作和绑定一起登记"时保得住。</para>
	///
	/// <para><b>材质永远是一张临时副本</b>（见 <see cref="ShaderBakeMaterialSource"/>）：
	/// 调滑块不该把项目里的 .mat 改脏，想留下改动请按"写回原材质"。</para>
	/// </summary>
	public sealed class BakeShaderAsImageWindow : EditorWindow
	{
		private const string kUxmlPath = "Packages/com.lrss3.xericuiactionvessel/Editor/ShaderBake/BakeShaderWindow.uxml";
		private const string kUssPath = "Packages/com.lrss3.xericuiactionvessel/Editor/ShaderBake/BakeShaderWindow.uss";
		private const string kRowUxmlPath = "Packages/com.lrss3.xericuiactionvessel/Editor/ShaderBake/BakeShaderPropertyRow.uxml";
		private const string kGlobalRowUxmlPath = "Packages/com.lrss3.xericuiactionvessel/Editor/ShaderBake/BakeShaderGlobalRow.uxml";
		private const string kDebugViewShaderPath = "Packages/com.lrss3.xericuiactionvessel/Editor/ShaderBake/ShaderBakeDebugView.shader";

		private const string kPrefSettings = "XericUI.ShaderBake.Settings";
		private const string kPrefAutoRefresh = "XericUI.ShaderBake.AutoRefresh";
		private const string kPrefChecker = "XericUI.ShaderBake.Checker";
		private const string kPrefDebugView = "XericUI.ShaderBake.DebugView";
		private const string kPrefDetailWidth = "XericUI.ShaderBake.DetailWidth";
		private const string kPrefSource = "XericUI.ShaderBake.SourceAsset";

		/// <summary> 测试视图用的那张 shader（Editor 目录下，见 ShaderBakeDebugView.shader） </summary>
		private const string kDebugViewShader = "Hidden/XericUI/ShaderBake/Debug View";

		/// <summary>
		/// 实时预览的节拍上限：有焦点时按 60fps 排，失焦降到 20fps。
		///
		/// <para>这只是"最早什么时候重画"，真正的帧率还受渲染耗时和编辑器自己的刷新率限制——
		/// 所以别处只管少做无用功，别指望靠调这个数字把卡顿调掉。</para>
		/// </summary>
		private const double kLiveIntervalFocused = 1.0 / 60.0;
		private const double kLiveIntervalBlurred = 1.0 / 20.0;

		/// <summary> 覆盖率读数降频：它要 ReadPixels，会同步一次显卡，实时预览里做多了就是顿感来源 </summary>
		private const double kCoverageInterval = 2.0;

		private const float kDefaultDetailWidth = 340f;
		private static readonly string[] kAntiAliasingChoices = { "1", "2", "4", "8" };
		private const string kCustomSize = "自定义";

		/// <summary> 常用导出尺寸。最后一项只是让当前值不在表里时下拉框有个落点 </summary>
		private static readonly string[] kSizePresets =
		{
			"256 × 256", "512 × 512", "1024 × 1024", "2048 × 2048", "4096 × 4096",
			"512 × 256", "1024 × 512", "2048 × 1024", kCustomSize
		};

		private ShaderBakeMaterialSource m_Source;
		private ShaderBakeRenderer m_Renderer;
		private ShaderBakePreviewPane m_Preview;
		private ShaderBakePropertyTable m_PropertyTable;
		private ShaderBakeGlobalTable m_Globals;
		private ShaderBakeExporter m_Exporter;

		private ObjectField m_ShaderField;
		private ObjectField m_MaterialField;
		private Label m_SourceNote;
		private Button m_ExportButton;
		private ToolbarButton m_RenderButton;
		private ToolbarToggle m_AutoRefreshToggle;
		private ToolbarToggle m_CheckerToggle;
		private ToolbarButton m_DebugButton;
		private Label m_StatusMessage;
		private Label m_StatusSize;
		private Label m_StatusTime;
		private Label m_StatusSource;
		private VisualElement m_DetailPane;
		private VisualElement m_DetailSplit;

		private readonly List<Action> m_Syncers = new List<Action>();

		private bool m_RenderPending;
		/// <summary> 实时预览关着时攒下的改动：还没重画，「重新渲染」按钮上会写着这件事 </summary>
		private bool m_PendingRefresh;
		private double m_LastRenderMs = -1;
		private double m_NextLiveRender;
		/// <summary> 上一次心跳的时刻；用来量两次心跳之间实际隔了多久 </summary>
		private double m_LastHeartbeatTime;
		/// <summary> 实测心跳间隔（毫秒），-1 = 还没量到 </summary>
		private float m_LiveFrameMs = -1f;
		private double m_NextStatusUpdate;
		private double m_NextCoverageCheck;
		/// <summary> 上一次结果的不透明像素占比，-1 = 没测出来 </summary>
		private float m_LastCoverage = -1f;
		/// <summary> 探针（自检 / 测试视图）活着才能让那张图继续显示；切回真实结果时才收掉 </summary>
		private readonly ShaderBakeOverrideRenderer m_Override = new ShaderBakeOverrideRenderer();
		private DebugView m_DebugView = DebugView.None;
		/// <summary> 测试视图那张材质：_Mode 决定画哪种图，建一次反复用 </summary>
		private Material m_DebugMaterial;
		private int m_SelectedFace;
		/// <summary> 给预览面板的分格数据：每帧重填，避免按格新建列表 </summary>
		private readonly List<ShaderBakePreviewPane.FaceCellRect> m_GridCells = new List<ShaderBakePreviewPane.FaceCellRect>();

		/// <summary> 预览换成"量几何与 UV"的测试图。都不吃贴图、不吃光照，画不出来的原因只剩一个。
		/// Uv/Gradient/Checker 的取值与 shader 的 _Mode 一一对应 </summary>
		private enum DebugView
		{
			None = -1,
			/// <summary> 红=U、绿=V，一格四角即 00/10/01/11 </summary>
			Uv = 0,
			/// <summary> 灰度尺：主体沿 U、顶条沿 V，每 0.1 一档，量这一格跨了多少像素 </summary>
			Gradient = 1,
			/// <summary> 半 UV 棋盘 + 每格色相 + 左下角红/右上角蓝，看拉伸、斜切与镜像 </summary>
			Checker = 2,
			/// <summary>
			/// 内置 Unlit/Color 的一张绿面片：不吃贴图、不吃光照、不吃 UV，只回答"这套矩阵画得出东西吗"。
			/// 取值故意离开 shader 的 _Mode 范围——它用的是另一张 shader
			/// </summary>
			SolidColor = 20
		}

		[MenuItem(XericRegistrationPaths.ToolBar + "/Bake Shader As Image", false, 20)]
		private static void OpenFromMenu()
		{
			BakeShaderAsImageWindow window = GetWindow<BakeShaderAsImageWindow>();
			window.titleContent = new GUIContent("Bake Shader");
			window.minSize = new Vector2(640f, 380f);
		}

		private void CreateGUI()
		{
			VisualTreeAsset tree = LoadAsset<VisualTreeAsset>(kUxmlPath, "BakeShaderWindow");
			if (tree == null)
			{
				Debug.LogError("[ShaderBake] 找不到界面文件 " + kUxmlPath + "，窗口无法构建");
				return;
			}

			rootVisualElement.Clear();
			tree.CloneTree(rootVisualElement);

			StyleSheet sheet = LoadAsset<StyleSheet>(kUssPath, "BakeShaderWindow");
			if (sheet != null)
				rootVisualElement.styleSheets.Add(sheet);

			m_Source = new ShaderBakeMaterialSource();
			m_Renderer = new ShaderBakeRenderer();
			m_Exporter = new ShaderBakeExporter(m_Renderer, SetStatus);
			m_Exporter.settingsReplaced += OnSettingsReplaced;

			try
			{
				m_Preview = new ShaderBakePreviewPane(rootVisualElement);
			}
			catch (InvalidOperationException e)
			{
				Debug.LogError("[ShaderBake] " + e.Message);
				return;
			}

			m_Preview.faceClicked += OnFaceClicked;

			CollectReferences();
			BuildPropertyTable();
			BuildGlobalTable();
			BindFaceGrid();
			BindToolbar();
			BindSections();
			BindSettings();
			BindDetailSplit();

			RestorePrefs();
			SyncUiFromSettings();
			AdoptSelectionIfEmpty();
			AssignSource(m_Source.sourceShader, m_Source.sourceMaterial, true);
			DoRender();

			// 实时预览的节拍由这里驱动，域重载/关窗口时在 OnDisable 里摘掉
			EditorApplication.update -= OnEditorUpdate;
			EditorApplication.update += OnEditorUpdate;
		}

		/// <summary>
		/// 编辑器心跳：实时预览开着就按固定节拍重画，不管有没有"改动事件"。
		///
		/// <para>之所以不能只等控件回调：数字框是延迟提交的，拖标签刻度、在别处改材质、
		/// shader 里用 <c>_Time</c> 挪顶点，这些都不会发 ChangeEvent——表现就是"界面不跟着刷新"。
		/// 关掉实时预览时这里什么都不做，改动只标记成待刷新。</para>
		/// </summary>
		private void OnEditorUpdate()
		{
			if (m_Renderer == null || m_Preview == null || m_AutoRefreshToggle == null || !m_AutoRefreshToggle.value)
				return;
			double now = EditorApplication.timeSinceStartup;
			if (now < m_NextLiveRender)
				return;

			// 实测两次心跳之间的间隔。它是"看到的帧率"，和渲染本身花了多少毫秒不是一回事：
			// 卡在别处时两个数会差很远，只有摆出来才分得清是谁的问题
			if (m_LastHeartbeatTime > 0.0)
			{
				float delta = (float)((now - m_LastHeartbeatTime) * 1000.0);
				m_LiveFrameMs = m_LiveFrameMs < 0f ? delta : Mathf.Lerp(m_LiveFrameMs, delta, 0.3f);
			}
			m_LastHeartbeatTime = now;

			m_NextLiveRender = now + (hasFocus ? kLiveIntervalFocused : kLiveIntervalBlurred);
			DoRender(true);
		}

		private void OnDisable()
		{
			EditorApplication.update -= OnEditorUpdate;
			SavePrefs();
			if (m_Exporter != null)
				m_Exporter.settingsReplaced -= OnSettingsReplaced;
			if (m_PropertyTable != null)
				m_PropertyTable.Dispose();
			if (m_Preview != null)
				m_Preview.Dispose();
			// 探针那张 RT 归探针所有，先收探针；测试视图材质挂着 shader，不销毁就是每次开关窗口漏一份
			m_Override.Release();
			ReleaseDebugMaterial();
			if (m_Source != null) m_Source.Dispose(true);
			if (m_Renderer != null) m_Renderer.Dispose(true);
			m_Source = null;
			m_Renderer = null;
			m_Preview = null;
			m_PropertyTable = null;
			m_Globals = null;
			m_Syncers.Clear();
		}

		private void OnFocus()
		{
			AdoptSelectionIfEmpty();
		}

		/// <summary> 参数被整块换掉（粘贴 JSON）后：回写界面并重画，否则预览和右侧数字对不上 </summary>
		private void OnSettingsReplaced()
		{
			SyncUiFromSettings();
			RefreshGlobals();
			RequestRender();
		}

		// ── 引用收集 ────────────────────────────────────────────

		private void CollectReferences()
		{
			m_ShaderField = Require<ObjectField>("src-shader");
			m_MaterialField = Require<ObjectField>("src-material");
			m_SourceNote = Require<Label>("src-note");
			m_ExportButton = Require<ToolbarButton>("export-menu");
			m_AutoRefreshToggle = Require<ToolbarToggle>("auto-refresh");
			m_CheckerToggle = Require<ToolbarToggle>("show-checker");
			m_DebugButton = Require<ToolbarButton>("debug-view");
			m_StatusMessage = Require<Label>("status-message");
			m_StatusSize = Require<Label>("status-size");
			m_StatusTime = Require<Label>("status-time");
			m_StatusSource = Require<Label>("status-source");
			m_DetailPane = Require<VisualElement>("detail-pane");
			m_DetailSplit = Require<VisualElement>("detail-split");

			// 侧栏只允许纵向滚动。UXML 的 mode="Vertical" 在 2021.3 里仍可能带出横向滚动条，
			// 这里显式把横向滚动器隐藏掉，宽度不足交给内部的 min-width/flex 规则去收敛。
			var detailScroll = m_DetailPane as ScrollView;
			if (detailScroll != null)
			{
				detailScroll.mode = ScrollViewMode.Vertical;
				detailScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
				detailScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
			}
		}

		private T Require<T>(string name) where T : VisualElement
		{
			T element = rootVisualElement.Q<T>(name);
			if (element == null)
				Debug.LogError("[ShaderBake] UXML 里缺节点 " + name + "，对应功能不可用");
			return element;
		}

		private void BuildGlobalTable()
		{
			VisualTreeAsset rowTemplate = LoadAsset<VisualTreeAsset>(kGlobalRowUxmlPath, "BakeShaderGlobalRow");
			if (rowTemplate == null)
			{
				Debug.LogError("[ShaderBake] 找不到全局参数行模板 " + kGlobalRowUxmlPath);
				return;
			}
			m_Globals = new ShaderBakeGlobalTable(rootVisualElement, rowTemplate);
			m_Globals.changed += RequestRender;
		}

		private void RefreshGlobals()
		{
			if (m_Globals == null)
				return;
			m_Globals.SetTarget(m_Renderer.settings.globals);
			Label note = rootVisualElement.Q<Label>("globals-note");
			if (note != null)
				note.text = m_Globals.rowCount == 0
					? "还没有全局参数：可以扫描 shader，或直接加入常用光照/环境预设"
					: m_Globals.rowCount + " 条（勾选才生效，渲染完立即还原）";
		}

		private void BuildPropertyTable()
		{
			VisualTreeAsset rowTemplate = LoadAsset<VisualTreeAsset>(kRowUxmlPath, "BakeShaderPropertyRow");
			if (rowTemplate == null)
			{
				Debug.LogError("[ShaderBake] 找不到属性行模板 " + kRowUxmlPath);
				return;
			}
			m_PropertyTable = new ShaderBakePropertyTable(rootVisualElement, rowTemplate);
			m_PropertyTable.changed += OnMaterialChanged;
		}

		// ── 工具栏 ──────────────────────────────────────────────

		private void BindToolbar()
		{
			if (m_ExportButton != null)
				m_ExportButton.clicked += ShowExportMenu;

			ToolbarButton render = rootVisualElement.Q<ToolbarButton>("render-now");
			if (render != null)
			{
				m_RenderButton = render;
				render.clicked += DoRender;
			}

			if (m_AutoRefreshToggle != null)
			{
				m_AutoRefreshToggle.RegisterValueChangedCallback(evt =>
				{
					// 这个勾就是"要不要一直重画"的总开关：打开时把攒下的待刷新立刻补上，
					// 关掉时不再重画，只把它记成待刷新显示在「重新渲染」上
					m_LastHeartbeatTime = 0.0;
					m_LiveFrameMs = -1f;
					UpdateRenderButton();
					if (evt.newValue && m_PendingRefresh)
						DoRender();
					SavePrefs();
				});
			}

			if (m_CheckerToggle != null)
				m_CheckerToggle.RegisterValueChangedCallback(evt =>
				{
					if (m_Preview != null) m_Preview.SetCheckerVisible(evt.newValue);
					SavePrefs();
				});

			if (m_DebugButton != null)
				m_DebugButton.clicked += ShowDebugMenu;

			Click("zoom-in", () => { if (m_Preview != null) { m_Preview.autoFit = false; m_Preview.ZoomIn(); } });
			Click("zoom-out", () => { if (m_Preview != null) { m_Preview.autoFit = false; m_Preview.ZoomOut(); } });
			Click("zoom-fit", () => { if (m_Preview != null) m_Preview.FitToView(); });
			Click("zoom-actual", () => { if (m_Preview != null) m_Preview.ActualSize(); });
			Click("reset-settings", ResetSettings);
		}

		// 普通按钮和工具栏按钮都走这里：Q<Button> 取不到 ToolbarButton，Q<ToolbarButton> 也取不到普通按钮
		private void Click(string name, Action action)
		{
			Button button = rootVisualElement.Q<Button>(name);
			if (button == null)
			{
				Debug.LogError("[ShaderBake] UXML 里缺按钮 " + name);
				return;
			}
			button.clicked += action;
		}

		private void ShowExportMenu()
		{
			GenericMenu menu = new GenericMenu();
			if (m_DebugView != DebugView.None)
			{
				// 免得有人对着一张测试色块图等它导出成那张图——导出永远走真实材质
				menu.AddDisabledItem(new GUIContent("当前显示的是测试视图，导出仍按真实材质"), false);
			}
			m_Exporter.BuildMenu(menu, SuggestedExportName());
			if (m_ExportButton != null)
				menu.DropDown(m_ExportButton.worldBound);
			else
				menu.ShowAsContext();
		}

		private string SuggestedExportName()
		{
			if (m_Source.sourceMaterial != null)
				return m_Source.sourceMaterial.name;
			if (m_Source.sourceShader != null)
			{
				string[] parts = m_Source.sourceShader.name.Split('/');
				return parts[parts.Length - 1];
			}
			return "BakedImage";
		}

		// ── 分组折叠 ────────────────────────────────────────────

		private void BindSections()
		{
			// 头部是 Toggle（名字带 -head），外层同名容器只是分组壳，别拿它去 Q<Toggle>
			Collapsible("sec-source-head", "sec-source-body");
			Collapsible("sec-output-head", "sec-output-body");
			Collapsible("sec-surface-head", "sec-surface-body");
			Collapsible("sec-camera-head", "sec-camera-body");
			Collapsible("sec-globals-head", "sec-globals-body");
			Collapsible("sec-props-head", "sec-props-body");
		}

		private void Collapsible(string headName, string bodyName)
		{
			Toggle head = rootVisualElement.Q<Toggle>(headName);
			VisualElement body = rootVisualElement.Q<VisualElement>(bodyName);
			if (head == null || body == null)
			{
				Debug.LogError("[ShaderBake] 分组节点不成对：" + headName + " / " + bodyName);
				return;
			}
			body.style.display = head.value ? DisplayStyle.Flex : DisplayStyle.None;
			head.RegisterValueChangedCallback(evt =>
				body.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None);
		}

		// ── 输入源 ──────────────────────────────────────────────

		private void BindSettings()
		{
			// 两个入口互斥：填了一个另一个必须清空，否则界面会显示"两个来源"而实际用其中一张
			if (m_ShaderField != null)
			{
				m_ShaderField.objectType = typeof(Shader);
				m_ShaderField.allowSceneObjects = false;
				m_ShaderField.RegisterValueChangedCallback(evt =>
				{
					var shader = evt.newValue as Shader;
					if (shader != null)
						AssignSource(shader, null, false);
					else if (m_MaterialField != null && m_MaterialField.value == null)
						AssignSource(null, null, false);
				});
			}
			if (m_MaterialField != null)
			{
				m_MaterialField.objectType = typeof(Material);
				m_MaterialField.allowSceneObjects = true;
				m_MaterialField.RegisterValueChangedCallback(evt =>
				{
					var material = evt.newValue as Material;
					if (material != null)
						AssignSource(null, material, false);
					else if (m_ShaderField != null && m_ShaderField.value == null)
						AssignSource(null, null, false);
				});
			}

			Button apply = rootVisualElement.Q<Button>("src-apply");
			if (apply != null)
				apply.clicked += ApplyToSource;
			Button revert = rootVisualElement.Q<Button>("src-revert");
			if (revert != null)
				revert.clicked += RevertToSource;

			Click("globals-detect", ScanGlobals);
			Click("globals-presets", AddGlobalPresets);

			BindOutput();
			BindSurface();
			BindCamera();
		}

		/// <summary>
		/// 指定来源。shader 与 material 只能有一个非空，另一个会被清掉。
		/// <paramref name="keepFields"/> 为真时不改对象字段（从存档恢复时用，字段已经在别处填好）。
		/// </summary>
		private void AssignSource(Shader shader, Material material, bool keepFields)
		{
			if (shader != null)
				m_Source.Assign(shader);
			else if (material != null)
				m_Source.Assign(material);
			else
				m_Source.Clear();

			m_Renderer.material = m_Source.material;

			if (!keepFields)
			{
				if (m_ShaderField != null) m_ShaderField.SetValueWithoutNotify(shader);
				if (m_MaterialField != null) m_MaterialField.SetValueWithoutNotify(material);
			}

			SetSourceNote();
			RefreshPropertyTable();
			RefreshGlobals();
			DoRender();
		}

		private void SetSourceNote()
		{
			if (m_SourceNote == null)
				return;
			m_SourceNote.text = m_Source.description;
			if (m_StatusSource != null)
				m_StatusSource.text = m_Source.description;
		}

		private void RefreshPropertyTable()
		{
			if (m_PropertyTable == null)
				return;
			m_PropertyTable.SetTarget(m_Source.shader, m_Source.material);
			string textureHint = ShaderBakePropertyTable.RepairTextureScale(m_Source.shader, m_Source.material);
			Label note = rootVisualElement.Q<Label>("props-note");
			if (note != null)
			{
				note.text = m_Source.shader == null
					? "未指定 shader"
					: (m_PropertyTable.rowCount + " 项可编辑属性（隐藏属性不列出）");
				if (!string.IsNullOrEmpty(textureHint))
					note.text += "\n" + textureHint;
			}
			// 只有"来源是材质"才有可写回/可回读的原资产
			SetApplyRevertEnabled(m_Source.sourceMaterial != null);
		}

		private void SetApplyRevertEnabled(bool enabled)
		{
			Button apply = rootVisualElement.Q<Button>("src-apply");
			Button revert = rootVisualElement.Q<Button>("src-revert");
			if (apply != null) apply.SetEnabled(enabled);
			if (revert != null) revert.SetEnabled(enabled);
		}

		private void ApplyToSource()
		{
			if (!m_Source.ApplyToSource())
			{
				SetStatus("没有原材质可写（来源是 Shader）");
				return;
			}
			EditorUtility.SetDirty(m_Source.sourceMaterial);
			SetStatus("已把参数写回 " + m_Source.sourceMaterial.name);
			DoRender();
		}

		private void RevertToSource()
		{
			if (!m_Source.RevertToSource())
			{
				SetStatus("没有原材质可回读");
				return;
			}
			m_Renderer.material = m_Source.material;
			RefreshPropertyTable();
			SetStatus("已丢弃改动，副本回到原材质参数");
			DoRender();
		}

		private void OnMaterialChanged()
		{
			RequestRender();
		}

		// ── 全局参数 ────────────────────────────────────────────

		private void ScanGlobals()
		{
			string note;
			ShaderBakeGlobalScanner.Scan(m_Source.shader, m_Renderer.settings.globals, out note);
			RefreshGlobals();
			SetStatus(note);
		}

		private void AddGlobalPresets()
		{
			m_Renderer.settings.globals.AddEnvironmentPresets();
			RefreshGlobals();
			SetStatus("已加入常用光照/环境预设（勾选才生效）");
		}

		// ── 输出 ────────────────────────────────────────────────

		private void BindOutput()
		{
			DropdownField preset = rootVisualElement.Q<DropdownField>("out-preset");
			if (preset != null)
			{
				preset.choices = new List<string>(kSizePresets);
				m_Syncers.Add(() => preset.SetValueWithoutNotify(CurrentSizePreset()));
				preset.RegisterValueChangedCallback(evt => ApplySizePreset(evt.newValue));
			}
			Click("out-fit-aspect", FitOutputAspect);

			BindInt("out-width", () => m_Renderer.settings.output.width, v => m_Renderer.settings.output.width = v);
			BindInt("out-height", () => m_Renderer.settings.output.height, v => m_Renderer.settings.output.height = v);
			BindInt("out-depth", () => m_Renderer.settings.output.depthBuffer, v => m_Renderer.settings.output.depthBuffer = v);
			BindInt("out-pass", () => m_Renderer.settings.output.shaderPass, v => m_Renderer.settings.output.shaderPass = v);
			BindBool("out-hdr", () => m_Renderer.settings.output.hdr, v => m_Renderer.settings.output.hdr = v);
			BindBool("out-srgb", () => m_Renderer.settings.output.sRGB, v => m_Renderer.settings.output.sRGB = v);
			BindBool("out-flip", () => m_Renderer.settings.output.flipY, v => m_Renderer.settings.output.flipY = v);
			BindColor("out-clear", () => m_Renderer.settings.output.clearColor, v => m_Renderer.settings.output.clearColor = v);

			DropdownField msaa = rootVisualElement.Q<DropdownField>("out-msaa");
			if (msaa != null)
			{
				msaa.choices = new List<string>(kAntiAliasingChoices);
				m_Syncers.Add(() => msaa.SetValueWithoutNotify(
					ShaderBakeOutputSettings.SnapAntiAliasing(m_Renderer.settings.output.antiAliasing).ToString()));
				msaa.RegisterValueChangedCallback(evt =>
				{
					m_Renderer.settings.output.antiAliasing = int.Parse(evt.newValue);
					Commit();
				});
			}
		}

		private string CurrentSizePreset()
		{
			Vector2Int size = m_Renderer.settings.output.size;
			string label = size.x + " × " + size.y;
			for (int i = 0; i < kSizePresets.Length - 1; i++)
				if (kSizePresets[i] == label)
					return label;
			return kCustomSize;
		}

		private void ApplySizePreset(string label)
		{
			if (label == kCustomSize)
				return;
			string[] parts = label.Split('×');
			if (parts.Length != 2)
				return;
			if (!int.TryParse(parts[0].Trim(), out int width) || !int.TryParse(parts[1].Trim(), out int height))
				return;
			m_Renderer.settings.output.width = width;
			m_Renderer.settings.output.height = height;
			Commit();
		}

		/// <summary> 按面片网格的整体宽高比把高度对上，省得手算 </summary>
		private void FitOutputAspect()
		{
			ShaderBakeSettings settings = m_Renderer.settings;
			float totalWidth = settings.surface.size.x * settings.surface.gridColumns;
			float totalHeight = settings.surface.size.y * settings.surface.gridRows;
			if (totalWidth <= 0.0001f || totalHeight <= 0.0001f)
				return;
			settings.output.height = Mathf.Clamp(
				Mathf.RoundToInt(settings.output.width * totalHeight / totalWidth),
				ShaderBakeOutputSettings.kMinDimension, ShaderBakeOutputSettings.kMaxDimension);
			Commit();
			SetStatus("高度已按面片比例对齐");
		}

		// ── 面片网格与选面 ──────────────────────────────────────

		private void BindFaceGrid()
		{
			Click("grid-left", () => ResizeGrid(true, true));
			Click("grid-right", () => ResizeGrid(false, true));
			Click("grid-down", () => ResizeGrid(true, false));
			Click("grid-up", () => ResizeGrid(false, false));
			Click("grid-reset", () =>
			{
				m_Renderer.settings.surface.ResetGrid();
				m_SelectedFace = 0;
				Commit();
			});
			Click("face-zero", () =>
			{
				ShaderBakeFaceCell cell = SelectedFaceCell();
				if (cell == null)
					return;
				cell.uvX = 0;
				cell.uvY = 0;
				Commit();
			});
			Click("face-fit", FitCameraOnSelectedFace);

			BindInt("face-uv-x",
				() => SelectedFaceCell() != null ? SelectedFaceCell().uvX : 0,
				v => { ShaderBakeFaceCell cell = SelectedFaceCell(); if (cell != null) cell.uvX = v; });
			BindInt("face-uv-y",
				() => SelectedFaceCell() != null ? SelectedFaceCell().uvY : 0,
				v => { ShaderBakeFaceCell cell = SelectedFaceCell(); if (cell != null) cell.uvY = v; });
		}

		/// <summary>
		/// 在左/右加一列，或上/下加一行。新格的 UV 编号由 <see cref="ShaderBakeSurfaceSettings"/>
		/// 按贴边那一格往外推，所以向左加出来的一定是 [-1,0] 这种负号区间。
		/// </summary>
		private void ResizeGrid(bool negativeSide, bool column)
		{
			ShaderBakeSurfaceSettings surface = m_Renderer.settings.surface;
			if (column)
				surface.AddColumn(!negativeSide);
			else
				surface.AddRow(!negativeSide);
			Commit();
			SetStatus(surface.gridColumns + " × " + surface.gridRows + " 格");
		}

		private ShaderBakeFaceCell SelectedFaceCell()
		{
			if (m_Renderer == null)
				return null;
			ShaderBakeSurfaceSettings surface = m_Renderer.settings.surface;
			if (surface.kind == ShaderBakeSurfaceKind.Mesh && surface.sourceMesh != null)
				return null;
			return surface.GetFace(m_SelectedFace);
		}

		private void OnFaceClicked(Vector2 viewport01)
		{
			int face;
			if (!m_Renderer.TryPickFace(viewport01, out face))
				return;
			m_SelectedFace = face;
			RefreshFaceUi();
			SetStatus("选中第 " + (face + 1) + " 格");
		}

		private void RefreshFaceUi()
		{
			if (m_Renderer == null)
				return;
			ShaderBakeSurfaceSettings surface = m_Renderer.settings.surface;
			int count = surface.FaceCount;
			m_SelectedFace = Mathf.Clamp(m_SelectedFace, 0, Mathf.Max(count - 1, 0));

			Label gridInfo = rootVisualElement.Q<Label>("grid-info");
			if (gridInfo != null)
				gridInfo.text = surface.gridColumns + " × " + surface.gridRows + " 格";

			bool meshMode = surface.kind == ShaderBakeSurfaceKind.Mesh && surface.sourceMesh != null;
			ShaderBakeFaceCell cell = meshMode ? null : surface.GetFace(m_SelectedFace);
			Label faceInfo = rootVisualElement.Q<Label>("face-info");
			if (faceInfo != null)
			{
				faceInfo.text = meshMode
					? "外部网格不参与面片点选"
					: "选中第 " + (m_SelectedFace + 1) + " 格：UV (" + cell.uvX + ", " + cell.uvY + ")\n" +
					  FaceCornerText(surface, cell);
			}
			SetFaceFieldsEnabled(!meshMode);
			UpdateFaceOverlay();
		}

		/// <summary>
		/// 这一格四个顶点的实际 UV 写出来。"设为 UV0" 的约定就是四角落在 (0,0)(1,0)(0,1)(1,1)，
		/// 再乘重复、加偏移；把结果摊开显示，是不是这个范围一眼就能核对。
		/// </summary>
		private static string FaceCornerText(ShaderBakeSurfaceSettings surface, ShaderBakeFaceCell cell)
		{
			Vector2 min = Vector2.Scale(new Vector2(cell.uvX, cell.uvY), surface.uvTiling) + surface.uvOffset;
			Vector2 max = Vector2.Scale(new Vector2(cell.uvX + 1, cell.uvY + 1), surface.uvTiling) + surface.uvOffset;
			return "四角 UV：(" + Corners(min.x, max.x) + ", " + Corners(min.y, max.y) + ")";
		}

		/// <summary> 一个轴的两个角值，左下在前 </summary>
		private static string Corners(float low, float high)
		{
			return low.ToString("0.##") + " / " + high.ToString("0.##");
		}

		private void SetFaceFieldsEnabled(bool enabled)
		{
			VisualElement x = rootVisualElement.Q<VisualElement>("face-uv-x");
			VisualElement y = rootVisualElement.Q<VisualElement>("face-uv-y");
			if (x != null) x.SetEnabled(enabled);
			if (y != null) y.SetEnabled(enabled);
		}

		/// <summary> 把选中格的屏幕框、分格线和每格的 UV 编号贴到浏览图上 </summary>
		private void UpdateFaceOverlay()
		{
			if (m_Preview == null || m_Renderer == null)
				return;
			ShaderBakeSurfaceSettings surface = m_Renderer.settings.surface;
			if (surface.kind == ShaderBakeSurfaceKind.Mesh && surface.sourceMesh != null)
			{
				m_Preview.SetFaceSelection(false, Vector2.zero, Vector2.zero, string.Empty);
				m_Preview.SetFaceGrid(null, 0, 0);
				return;
			}

			int columns = Mathf.Max(surface.gridColumns, 1);
			int rows = Mathf.Max(surface.gridRows, 1);
			m_GridCells.Clear();
			for (int i = 0; i < columns * rows; i++)
			{
				Vector2 min, max;
				ShaderBakeFaceCell cell = surface.GetFace(i);
				if (!m_Renderer.TryGetFaceViewportBounds(i, out min, out max))
					continue;
				// 完全在画面外的那几格（按「选中格铺满」之后就只剩一格可见）不参与，
				// 否则它们的包围盒会被夹成零面积，编号全挤在画面边上
				if (max.x - min.x < 0.001f || max.y - min.y < 0.001f)
					continue;
				m_GridCells.Add(new ShaderBakePreviewPane.FaceCellRect
				{
					min = min,
					max = max,
					label = "U" + cell.uvX + " V" + cell.uvY
				});
			}
			m_Preview.SetFaceGrid(m_GridCells, columns, rows);

			Vector2 selMin, selMax;
			if (!m_Renderer.TryGetFaceViewportBounds(m_SelectedFace, out selMin, out selMax))
			{
				m_Preview.SetFaceSelection(false, Vector2.zero, Vector2.zero, string.Empty);
				return;
			}
			// 分了格就由每格的编号说明 UV，选中框只留框，不然同一格里两行字叠着
			m_Preview.SetFaceSelection(true, selMin, selMax, columns * rows > 1 ? string.Empty : SelectedFaceLabel());
		}

		private string SelectedFaceLabel()
		{
			ShaderBakeFaceCell cell = SelectedFaceCell();
			return cell == null ? string.Empty : "UV (" + cell.uvX + ", " + cell.uvY + ")";
		}

		// ── 面片 ────────────────────────────────────────────────

		private void BindSurface()
		{
			EnumField kind = rootVisualElement.Q<EnumField>("surf-kind");
			if (kind != null)
			{
				// 2022.1 的 EnumField 只认一个默认值，挡位表由枚举本身提供
				kind.Init(ShaderBakeSurfaceKind.Quad);
				m_Syncers.Add(() => kind.SetValueWithoutNotify(m_Renderer.settings.surface.kind));
				kind.RegisterValueChangedCallback(evt =>
				{
					m_Renderer.settings.surface.kind = (ShaderBakeSurfaceKind)evt.newValue;
					Commit();
				});
			}

			BindFloat("surf-size-x", () => m_Renderer.settings.surface.size.x, v => SetSizeX(v));
			BindFloat("surf-size-y", () => m_Renderer.settings.surface.size.y, v => SetSizeY(v));
			BindInt("surf-seg-x", () => m_Renderer.settings.surface.segments.x, v => SetSegmentX(v));
			BindInt("surf-seg-y", () => m_Renderer.settings.surface.segments.y, v => SetSegmentY(v));
			BindFloat("surf-uv-off-x", () => m_Renderer.settings.surface.uvOffset.x, v => SetUvOffsetX(v));
			BindFloat("surf-uv-off-y", () => m_Renderer.settings.surface.uvOffset.y, v => SetUvOffsetY(v));
			BindFloat("surf-uv-tile-x", () => m_Renderer.settings.surface.uvTiling.x, v => SetUvTileX(v));
			BindFloat("surf-uv-tile-y", () => m_Renderer.settings.surface.uvTiling.y, v => SetUvTileY(v));
			BindFloat("surf-pos-x", () => m_Renderer.settings.surface.position.x, v => SetPos(v, m_Renderer.settings.surface.position.y, m_Renderer.settings.surface.position.z));
			BindFloat("surf-pos-y", () => m_Renderer.settings.surface.position.y, v => SetPos(m_Renderer.settings.surface.position.x, v, m_Renderer.settings.surface.position.z));
			BindFloat("surf-pos-z", () => m_Renderer.settings.surface.position.z, v => SetPos(m_Renderer.settings.surface.position.x, m_Renderer.settings.surface.position.y, v));
			BindFloat("surf-rot-x", () => m_Renderer.settings.surface.eulerAngles.x, v => SetRot(v, m_Renderer.settings.surface.eulerAngles.y, m_Renderer.settings.surface.eulerAngles.z));
			BindFloat("surf-rot-y", () => m_Renderer.settings.surface.eulerAngles.y, v => SetRot(m_Renderer.settings.surface.eulerAngles.x, v, m_Renderer.settings.surface.eulerAngles.z));
			BindFloat("surf-rot-z", () => m_Renderer.settings.surface.eulerAngles.z, v => SetRot(m_Renderer.settings.surface.eulerAngles.x, m_Renderer.settings.surface.eulerAngles.y, v));
			BindFloat("surf-scale-x", () => m_Renderer.settings.surface.scale.x, v => SetScale(v, m_Renderer.settings.surface.scale.y, m_Renderer.settings.surface.scale.z));
			BindFloat("surf-scale-y", () => m_Renderer.settings.surface.scale.y, v => SetScale(m_Renderer.settings.surface.scale.x, v, m_Renderer.settings.surface.scale.z));
			BindFloat("surf-scale-z", () => m_Renderer.settings.surface.scale.z, v => SetScale(m_Renderer.settings.surface.scale.x, m_Renderer.settings.surface.scale.y, v));
			BindInt("surf-submesh", () => m_Renderer.settings.surface.meshSubMesh, v => m_Renderer.settings.surface.meshSubMesh = v);
			BindBool("surf-normalize", () => m_Renderer.settings.surface.normalizeMeshSize, v => m_Renderer.settings.surface.normalizeMeshSize = v);
			BindBool("surf-uv-st", () => m_Renderer.settings.surface.applyUvToMainTexST, v => m_Renderer.settings.surface.applyUvToMainTexST = v);
			BindBool("surf-double-sided", () => m_Renderer.settings.surface.doubleSided, v => m_Renderer.settings.surface.doubleSided = v);

			ObjectField meshField = rootVisualElement.Q<ObjectField>("surf-mesh");
			if (meshField != null)
			{
				meshField.objectType = typeof(Mesh);
				meshField.allowSceneObjects = true;
				m_Syncers.Add(() => meshField.SetValueWithoutNotify(m_Renderer.settings.surface.sourceMesh));
				meshField.RegisterValueChangedCallback(evt =>
				{
					m_Renderer.settings.surface.sourceMesh = evt.newValue as Mesh;
					if (m_Renderer.settings.surface.sourceMesh != null && m_Renderer.settings.surface.kind != ShaderBakeSurfaceKind.Mesh)
						m_Renderer.settings.surface.kind = ShaderBakeSurfaceKind.Mesh;
					Commit();
				});
			}

			Click("surf-reset", ResetSurface);
			Click("surf-pick-scene", PickSceneMesh);
			Click("surf-rot-90", () => { m_Renderer.settings.surface.eulerAngles += new Vector3(0f, 0f, 90f); Commit(); });
			Click("surf-flip-u", () => FlipUv(true));
			Click("surf-flip-v", () => FlipUv(false));

			Button importModel = rootVisualElement.Q<Button>("surf-import-model");
			if (importModel != null)
				importModel.clicked += ImportModel;
		}

		// 面片那些"改一个分量"的写法摊成方法，绑定处只留一行，读起来不至于全是三元组
		private void SetSizeX(float v) { Vector2 s = m_Renderer.settings.surface.size; s.x = v; m_Renderer.settings.surface.size = s; }
		private void SetSizeY(float v) { Vector2 s = m_Renderer.settings.surface.size; s.y = v; m_Renderer.settings.surface.size = s; }
		private void SetSegmentX(int v) { Vector2Int s = m_Renderer.settings.surface.segments; s.x = v; m_Renderer.settings.surface.segments = s; }
		private void SetSegmentY(int v) { Vector2Int s = m_Renderer.settings.surface.segments; s.y = v; m_Renderer.settings.surface.segments = s; }
		private void SetUvOffsetX(float v) { Vector2 u = m_Renderer.settings.surface.uvOffset; u.x = v; m_Renderer.settings.surface.uvOffset = u; }
		private void SetUvOffsetY(float v) { Vector2 u = m_Renderer.settings.surface.uvOffset; u.y = v; m_Renderer.settings.surface.uvOffset = u; }
		private void SetUvTileX(float v) { Vector2 u = m_Renderer.settings.surface.uvTiling; u.x = v; m_Renderer.settings.surface.uvTiling = u; }
		private void SetUvTileY(float v) { Vector2 u = m_Renderer.settings.surface.uvTiling; u.y = v; m_Renderer.settings.surface.uvTiling = u; }
		private void SetPos(float x, float y, float z) { m_Renderer.settings.surface.position = new Vector3(x, y, z); }
		private void SetRot(float x, float y, float z) { m_Renderer.settings.surface.eulerAngles = new Vector3(x, y, z); }
		private void SetScale(float x, float y, float z) { m_Renderer.settings.surface.scale = new Vector3(x, y, z); }

		private void FlipUv(bool horizontal)
		{
			// 镜像后覆盖范围必须和原来一样，否则图会被推出画面。
			// uv = uv01 * t + o 的取值范围是 [o, o+t]；取 t' = -t、o' = o+t 后范围变成 [o+t, o]，正好还是同一段
			ShaderBakeSurfaceSettings s = m_Renderer.settings.surface;
			if (horizontal)
			{
				s.uvOffset.x += s.uvTiling.x;
				s.uvTiling.x = -s.uvTiling.x;
			}
			else
			{
				s.uvOffset.y += s.uvTiling.y;
				s.uvTiling.y = -s.uvTiling.y;
			}
			Commit();
		}

		private void ResetSurface()
		{
			m_Renderer.settings.surface = new ShaderBakeSurfaceSettings();
			Commit();
			SetStatus("面片参数已归位");
		}

		private void PickSceneMesh()
		{
			string note;
			Mesh mesh = ShaderBakeModelSource.PickFromSelection(out note);
			if (mesh == null)
			{
				SetStatus(note);
				return;
			}
			m_Renderer.settings.surface.sourceMesh = mesh;
			m_Renderer.settings.surface.kind = ShaderBakeSurfaceKind.Mesh;
			m_Renderer.settings.surface.meshSubMesh = 0;
			Commit();
			SetStatus(note);
		}

		private void ImportModel()
		{
			string note;
			Mesh mesh = ShaderBakeModelSource.ImportFromFile(out note);
			if (mesh == null)
			{
				SetStatus(note);
				return;
			}
			m_Renderer.settings.surface.sourceMesh = mesh;
			m_Renderer.settings.surface.kind = ShaderBakeSurfaceKind.Mesh;
			m_Renderer.settings.surface.meshSubMesh = 0;
			Commit();
			SetStatus(note);
		}

		// ── 相机 ────────────────────────────────────────────────

		private void BindCamera()
		{
			BindBool("cam-ortho", () => m_Renderer.settings.camera.orthographic, v => m_Renderer.settings.camera.orthographic = v);
			BindFloat("cam-ortho-size", () => m_Renderer.settings.camera.orthographicSize, v => m_Renderer.settings.camera.orthographicSize = v);
			BindFloat("cam-fov", () => m_Renderer.settings.camera.fieldOfView, v => m_Renderer.settings.camera.fieldOfView = v);
			BindFloat("cam-near", () => m_Renderer.settings.camera.nearClipPlane, v => m_Renderer.settings.camera.nearClipPlane = v);
			BindFloat("cam-far", () => m_Renderer.settings.camera.farClipPlane, v => m_Renderer.settings.camera.farClipPlane = v);
			BindFloat("cam-pos-x", () => m_Renderer.settings.camera.position.x, v => SetCamPos(v, m_Renderer.settings.camera.position.y, m_Renderer.settings.camera.position.z));
			BindFloat("cam-pos-y", () => m_Renderer.settings.camera.position.y, v => SetCamPos(m_Renderer.settings.camera.position.x, v, m_Renderer.settings.camera.position.z));
			BindFloat("cam-pos-z", () => m_Renderer.settings.camera.position.z, v => SetCamPos(m_Renderer.settings.camera.position.x, m_Renderer.settings.camera.position.y, v));
			BindFloat("cam-rot-x", () => m_Renderer.settings.camera.eulerAngles.x, v => SetCamRot(v, m_Renderer.settings.camera.eulerAngles.y, m_Renderer.settings.camera.eulerAngles.z));
			BindFloat("cam-rot-y", () => m_Renderer.settings.camera.eulerAngles.y, v => SetCamRot(m_Renderer.settings.camera.eulerAngles.x, v, m_Renderer.settings.camera.eulerAngles.z));
			BindFloat("cam-rot-z", () => m_Renderer.settings.camera.eulerAngles.z, v => SetCamRot(m_Renderer.settings.camera.eulerAngles.x, m_Renderer.settings.camera.eulerAngles.y, v));
			Click("cam-fit", FitCamera);
			Click("cam-selftest", RunPipelineSelfTest);
		}

		private void SetCamPos(float x, float y, float z) { m_Renderer.settings.camera.position = new Vector3(x, y, z); }
		private void SetCamRot(float x, float y, float z) { m_Renderer.settings.camera.eulerAngles = new Vector3(x, y, z); }

		private void FitCamera()
		{
			m_Renderer.FitCameraToGeometry();
			Commit();
			SetStatus("相机已框住面片");
		}

		/// <summary>
		/// 相机改成只框选中的那一格。「框住面片」量的是整个网格，多格时一格只占画面中间一小块，
		/// 这就是"烘出来的尺寸和场景里贴上去不一样"的来源——铺满之后一格才等于一整张图。
		/// </summary>
		private void FitCameraOnSelectedFace()
		{
			if (!m_Renderer.FitCameraOnFace(m_SelectedFace))
			{
				SetStatus("选中格铺满不可用：外部网格模式下不分格");
				return;
			}
			Commit();
			SetStatus("第 " + (m_SelectedFace + 1) + " 格已铺满画面，仍有留白就按「匹配面片比例」");
		}

		/// <summary>
		/// 用一张不吃贴图、不吃光照、不吃 UV 的内置纯色面片，按<b>同一套</b>面片 / 相机再画一次。
		/// 画得出来就说明管线是通的，画面空的原因在被测 shader 那边（贴图没指定、混合模式、需要光照）；
		/// 画不出来才是这里的问题（相机框不住、面片尺寸）。
		///
		/// <para>它就是测试视图里的一种，所以结果会跟着实时预览一直留在画面上，
		/// 不再像从前那样被下一次重画顶掉。</para>
		/// </summary>
		private void RunPipelineSelfTest()
		{
			if (m_DebugView == DebugView.SolidColor)
				DoRender();
			else
				SetDebugView(DebugView.SolidColor);

			if (m_DebugView != DebugView.SolidColor)
			{
				SetSelfTestNote("自检失败：纯色探针渲不出来 → 问题在渲染管线。" + DescribePipeline());
				return;
			}
			if (m_LastCoverage < 0f)
			{
				SetSelfTestNote("自检读不回像素：显卡或纹理格式不支持这次采样");
				return;
			}
			if (m_LastCoverage <= 0.001f)
			{
				SetSelfTestNote("自检未画出任何像素 → 问题在渲染管线。" + DescribePipeline() +
				               " 先按「框住面片」或「选中格铺满」再自检一次（探针固定 pass 0，pass 不背这个锅）。");
				SetStatus("管线自检：空画面");
				return;
			}
			SetSelfTestNote("自检通过：纯色面片盖住画面 " + Mathf.RoundToInt(m_LastCoverage * 100f) +
			               "%。管线是通的，画面空是被测 shader 自己的事（贴图、pass、混合模式、需要光照）");
			SetStatus("管线自检：正常");
		}

		/// <summary>
		/// 把决定"画不画得见"的那几个数值摊开写一行。空画面的时候最缺的就是这个——
		/// 否则只能靠猜是相机框不住、面片太小还是 pass 选错。
		/// </summary>
		private string DescribePipeline()
		{
			ShaderBakeSettings s = m_Renderer.settings;
			return "正交=" + s.camera.orthographic +
			       " 半高=" + s.camera.orthographicSize.ToString("0.###") +
			       " 近/远=" + s.camera.nearClipPlane.ToString("0.###") + "/" + s.camera.farClipPlane.ToString("0.###") +
			       " 机位=" + s.camera.position +
			       " 面片=" + s.surface.gridColumns + "×" + s.surface.gridRows + " 格 尺寸=" + s.surface.size +
			       " 双面=" + s.surface.doubleSided +
			       " pass=" + s.output.shaderPass + "（探针固定用 0）" +
			       " 翻转Y=" + s.output.flipY;
		}

		private void SetSelfTestNote(string text)
		{
			Label note = rootVisualElement.Q<Label>("selftest-note");
			if (note != null)
				note.text = text;
		}

		// ── 右侧细节栏宽度 ──────────────────────────────────────

		private void BindDetailSplit()
		{
			if (m_DetailSplit == null || m_DetailPane == null)
				return;
			float dragStartX = 0f, startWidth = 0f;
			bool dragging = false;

			m_DetailSplit.RegisterCallback<PointerDownEvent>(evt =>
			{
				dragging = true;
				dragStartX = evt.position.x;
				startWidth = m_DetailPane.resolvedStyle.width;
				m_DetailSplit.CapturePointer(evt.pointerId);
				evt.StopPropagation();
			});
			m_DetailSplit.RegisterCallback<PointerMoveEvent>(evt =>
			{
				if (!dragging)
					return;
				m_DetailPane.style.width = Mathf.Clamp(startWidth - (evt.position.x - dragStartX), 240f, 720f);
			});
			m_DetailSplit.RegisterCallback<PointerUpEvent>(evt =>
			{
				dragging = false;
				m_DetailSplit.ReleasePointer(evt.pointerId);
				SavePrefs();
			});
		}

		// ── 通用绑定 ────────────────────────────────────────────

		/*
		 * 一律按名字取元素、再按 INotifyValueChanged<T> 绑定，而不是 Q<FloatField> 之类。
		 * 同名的数字输入框在 2021.3 / 2022.1 分属 UnityEditor.UIElements 与 UnityEngine.UIElements，
		 * 按具体类型 Q 一旦落在另一侧就取不到：表现是数值永远空白，而且当时不一定有显眼报错。
		 */

		private void BindFloat(string name, Func<float> get, Action<float> set)
		{
			VisualElement field = rootVisualElement.Q<VisualElement>(name);
			var target = field as INotifyValueChanged<float>;
			if (target == null)
			{
				Debug.LogError("[ShaderBake] UXML 里缺数值控件 " + name);
				return;
			}
			// 延迟提交：一边打字一边回写会把光标和"0."这类中间输入搅乱，等回车/失焦再收
			var delayedTarget = field as TextInputBaseField<float>;
			if (delayedTarget != null)
				delayedTarget.isDelayed = true;
			m_Syncers.Add(() => target.SetValueWithoutNotify(get()));
			field.RegisterCallback<ChangeEvent<float>>(evt =>
			{
				set(evt.newValue);
				Commit();
			});
		}

		private void BindInt(string name, Func<int> get, Action<int> set)
		{
			VisualElement field = rootVisualElement.Q<VisualElement>(name);
			var target = field as INotifyValueChanged<int>;
			if (target == null)
			{
				Debug.LogError("[ShaderBake] UXML 里缺整数控件 " + name);
				return;
			}
			var delayedTarget = field as TextInputBaseField<int>;
			if (delayedTarget != null)
				delayedTarget.isDelayed = true;
			m_Syncers.Add(() => target.SetValueWithoutNotify(get()));
			field.RegisterCallback<ChangeEvent<int>>(evt =>
			{
				set(evt.newValue);
				Commit();
			});
		}

		private void BindBool(string name, Func<bool> get, Action<bool> set)
		{
			VisualElement field = rootVisualElement.Q<VisualElement>(name);
			var target = field as INotifyValueChanged<bool>;
			if (target == null)
			{
				Debug.LogError("[ShaderBake] UXML 里缺开关 " + name);
				return;
			}
			m_Syncers.Add(() => target.SetValueWithoutNotify(get()));
			field.RegisterCallback<ChangeEvent<bool>>(evt =>
			{
				set(evt.newValue);
				Commit();
			});
		}

		private void BindColor(string name, Func<Color> get, Action<Color> set)
		{
			VisualElement field = rootVisualElement.Q<VisualElement>(name);
			var target = field as INotifyValueChanged<Color>;
			if (target == null)
			{
				Debug.LogError("[ShaderBake] UXML 里缺颜色控件 " + name);
				return;
			}
			m_Syncers.Add(() => target.SetValueWithoutNotify(get()));
			field.RegisterCallback<ChangeEvent<Color>>(evt =>
			{
				set(evt.newValue);
				Commit();
			});
		}

		/// <summary>
		/// 一次参数提交的固定顺序：收拢数值 → 整体回写界面 → 请求重画。
		/// 顺序反了就会出现"界面显示 8192、实际渲 4096"这种对不上账的情况。
		/// </summary>
		private void Commit()
		{
			m_Renderer.settings.ClampAll();
			m_Renderer.InvalidateGeometry();
			SyncUiFromSettings();
			RefreshFaceUi();
			RequestRender();
		}

		private void SyncUiFromSettings()
		{
			foreach (Action sync in m_Syncers)
				sync();
		}

		private void ResetSettings()
		{
			m_Renderer.settings = ShaderBakeSettings.CreateDefault();
			RefreshGlobals();
			Commit();
			if (m_Preview != null)
				m_Preview.FitToView();
			SetStatus("参数已重置");
		}

		// ── 渲染 ────────────────────────────────────────────────

		/// <summary>
		/// 「重新渲染」按钮兼任待刷新指示灯：实时预览关掉的时候画面不会自己跟上参数，
		/// 只在按钮上写出来，比只靠状态栏一句话可靠。
		/// </summary>
		private void UpdateRenderButton()
		{
			if (m_RenderButton == null)
				return;
			bool pending = m_PendingRefresh && (m_AutoRefreshToggle == null || !m_AutoRefreshToggle.value);
			m_RenderButton.text = pending ? "重新渲染 · 待刷新" : "重新渲染";
			m_RenderButton.EnableInClassList("toolbar-emphasize", pending);
		}

		private void RequestRender()
		{
			if (m_AutoRefreshToggle != null && !m_AutoRefreshToggle.value)
			{
				// 实时预览关着的时候，改完参数画面不会自己动，所以把"待刷新"直接写在按钮上——
				// 否则只有一条状态栏提示，很容易被下一次别的状态覆盖掉，看不出来画面已经旧了
				m_PendingRefresh = true;
				UpdateRenderButton();
				SetStatus("参数已改，按「重新渲染」出图");
				return;
			}
			if (m_RenderPending)
				return;
			m_RenderPending = true;
			// 一帧里可能连着收到好几个控件的提交（拖滑块尤其明显），全部并进同一次渲染
			EditorApplication.delayCall += RenderDelayed;
		}

		private void RenderDelayed()
		{
			if (this == null || !m_RenderPending)
				return;
			DoRender();
		}

		private void DoRender()
		{
			DoRender(false);
		}

		/// <param name="fromHeartbeat">
		/// 实时预览的定时重画。心跳每秒几十次，状态栏那句"已渲染"要是照写不误，
		/// 刚按「选中格铺满」给出的提示就活不过一帧，所以心跳只更新画面与时间，不抢话。
		/// </param>
		private void DoRender(bool fromHeartbeat)
		{
			m_RenderPending = false;
			m_PendingRefresh = false;
			UpdateRenderButton();
			if (m_Renderer == null || m_Preview == null)
				return;

			bool measureCoverage = ShouldMeasureCoverage(fromHeartbeat);
			bool wantDebugView = m_DebugView != DebugView.None;
			if (wantDebugView && RenderDebugView(measureCoverage, fromHeartbeat))
				return;
			if (wantDebugView)
			{
				// 探针点不着就别让界面停在一个空视图上，也不让心跳每帧都撞一次同一个错误
				m_DebugView = DebugView.None;
				m_Override.Release();
				ReleaseDebugMaterial();
				UpdateDebugUi();
			}

			double start = EditorApplication.timeSinceStartup;
			RenderTexture rt = m_Renderer.Render();
			m_LastRenderMs = (EditorApplication.timeSinceStartup - start) * 1000.0;

			if (rt == null)
			{
				m_Preview.Clear();
				SetStatus(string.IsNullOrEmpty(m_Renderer.lastError) ? "渲染失败" : m_Renderer.lastError);
				UpdateSizeReadout(true);
				return;
			}

			m_Preview.SetResult(rt, m_Renderer.settings.output.size);
			if (measureCoverage)
				MeasureCoverage(rt);
			// 分格线与编号只跟参数、相机有关，而参数一改必然带一次非心跳渲染；
			// 心跳时再刷一遍等于每帧让 Yoga 重排整个覆盖层，这是预览发卡的来源之一
			if (!fromHeartbeat)
				UpdateFaceOverlay();
			UpdateSizeReadout(!fromHeartbeat);
			if (fromHeartbeat)
				return;
			if (string.IsNullOrEmpty(m_StatusMessage.text) || m_StatusMessage.text.StartsWith("导出") || m_StatusMessage.text.StartsWith("参数已改"))
				SetStatus("已渲染");
		}

		/// <summary>
		/// 覆盖率量的是"这一轮画面上有没有东西"，慢变化，两秒一次足够；参数一改则立刻量。
		/// 心跳时每次都量的话，每帧都要同步一次显卡，实时预览就变成卡顿本身。
		/// 不量的时候沿用上一个读数，界面不会因此少信息。
		/// </summary>
		private bool ShouldMeasureCoverage(bool fromHeartbeat)
		{
			double now = EditorApplication.timeSinceStartup;
			if (!fromHeartbeat || now >= m_NextCoverageCheck)
			{
				m_NextCoverageCheck = now + kCoverageInterval;
				return true;
			}
			return false;
		}

		private void MeasureCoverage(RenderTexture rt)
		{
			float coverage;
			Color average;
			m_LastCoverage = ShaderBakeReadback.MeasureCoverage(rt, out coverage, out average) ? coverage : -1f;
			if (m_LastCoverage >= 0f)
				m_Preview.SetCoverage(m_LastCoverage, average);
		}

		// ── 测试视图 ────────────────────────────────────────────

		private void ShowDebugMenu()
		{
			GenericMenu menu = new GenericMenu();
			AddDebugItem(menu, "无（看真实结果）", DebugView.None);
			menu.AddSeparator(string.Empty);
			AddDebugItem(menu, "UV 颜色：红=U 绿=V，四角即 00/10/01/11", DebugView.Uv);
			AddDebugItem(menu, "UV 梯度：灰度尺，量这一格占画面多大", DebugView.Gradient);
			AddDebugItem(menu, "棋盘：方块变矩形/平行四边形就是缩放或斜切不对", DebugView.Checker);
			menu.AddSeparator(string.Empty);
			AddDebugItem(menu, "纯色（管线自检）：画不出它就不是材质的问题", DebugView.SolidColor);
			if (m_DebugButton != null)
				menu.DropDown(m_DebugButton.worldBound);
			else
				menu.ShowAsContext();
		}

		private void AddDebugItem(GenericMenu menu, string label, DebugView view)
		{
			menu.AddItem(new GUIContent(label), m_DebugView == view, OnDebugChosen, view);
		}

		private void OnDebugChosen(object userData)
		{
			SetDebugView((DebugView)userData);
		}

		private void SetDebugView(DebugView view)
		{
			if (view == m_DebugView)
				return;
			m_DebugView = view;
			// 材质里写着这一次要画哪种图，换视图就得重建
			ReleaseDebugMaterial();
			if (view == DebugView.None)
			{
				// 测试视图不看了，探针占着的显存也该还回来
				m_Override.Release();
			}
			UpdateDebugUi();
			DoRender();
			SavePrefs();
		}

		private void UpdateDebugUi()
		{
			if (m_DebugButton != null)
				m_DebugButton.text = m_DebugView == DebugView.None
					? "测试视图 ▾"
					: "测试视图：" + DebugViewName(m_DebugView) + " ▾";

			Label note = rootVisualElement.Q<Label>("debug-note");
			if (note == null)
				return;
			note.style.display = m_DebugView == DebugView.None ? DisplayStyle.None : DisplayStyle.Flex;
			if (m_DebugView != DebugView.None)
				note.text = DebugViewLegend(m_DebugView);
		}

		private static string DebugViewName(DebugView view)
		{
			switch (view)
			{
				case DebugView.Uv: return "UV";
				case DebugView.Gradient: return "梯度";
				case DebugView.Checker: return "棋盘";
				case DebugView.SolidColor: return "纯色";
				default: return "无";
			}
		}

		private static string DebugViewLegend(DebugView view)
		{
			switch (view)
			{
				case DebugView.Gradient:
					return "梯度：横向那条尺是 U（左黑右白），顶上窄条是 V（下黑上白），每 0.1 一档压暗，整数 UV 处一条亮线。"
						+ "尺子只占画面中间一段 → 相机框的是整个网格，按「选中格铺满」把它拉到一格铺满；"
						+ "尺子之外是棋盘格 → 那些像素没画到东西。";
				case DebugView.Checker:
					return "棋盘：半 UV 一格的棋盘 + 每格一个色相，左下角红块、右上角蓝块。"
						+ "方块变长条=横竖缩放不等，变平行四边形=斜切或透视，红蓝换位=U 或 V 被镜像。";
				case DebugView.SolidColor:
					return "纯色：一张不吃贴图、不吃光照、不吃 UV 的绿面片。画得出来 → 管线是通的，空画面是被测 shader "
						+ "自己的事（贴图没指定、混合模式、需要光照）；画不出来 → 查相机框位与面片尺寸，与材质无关。";
				default:
					return "UV：红 = U，绿 = V。一格四角该是 黑(0,0) 红(1,0) 绿(0,1) 黄(1,1)；"
						+ "整数 UV 处压暗，每格色相不同，能数出这是第几格。四角同色就是 UV 被压成了一个点。";
			}
		}

		/// <summary>
		/// 测试视图的材质常驻，按当前视图建一次反复用——每秒重画几十次的路子上，
		/// 每次都新建再 DestroyImmediate 一张材质本身就是卡顿来源。换视图时才重建。
		/// </summary>
		private bool EnsureDebugMaterial()
		{
			if (m_DebugMaterial != null)
				return true;

			if (m_DebugView == DebugView.SolidColor)
				return EnsureSolidDebugMaterial();

			// 先按名字找：这张 shader 在 Editor 目录里，编辑器下 Shader.Find 认得到；
			// 找不到再按资产路径捞一次，覆盖包被放进 Assets/ 的安装方式
			Shader shader = Shader.Find(kDebugViewShader);
			if (shader == null)
				shader = LoadAsset<Shader>(kDebugViewShaderPath, "ShaderBakeDebugView");
			if (shader == null)
			{
				Debug.LogWarning("[ShaderBake] 测试视图不可用：包内找不到 " + kDebugViewShader + "（ShaderBakeDebugView.shader 没被导入？）");
				SetStatus("测试视图不可用：包里找不到 " + kDebugViewShader);
				return false;
			}

			m_DebugMaterial = new Material(shader)
			{
				name = "ShaderBakeDebugView",
				hideFlags = HideFlags.DontSave
			};
			m_DebugMaterial.SetFloat("_Mode", (float)(int)m_DebugView);
			return true;
		}

		private bool EnsureSolidDebugMaterial()
		{
			Shader solid = Shader.Find("Unlit/Color");
			if (solid == null)
			{
				Debug.LogWarning("[ShaderBake] 纯色探针不可用：这个工程里找不到 Unlit/Color");
				SetStatus("纯色探针不可用：找不到 Unlit/Color");
				return false;
			}
			m_DebugMaterial = new Material(solid)
			{
				name = "ShaderBakeSelfTest",
				hideFlags = HideFlags.DontSave
			};
			m_DebugMaterial.color = new Color(0.25f, 0.85f, 0.5f);
			return true;
		}

		private void ReleaseDebugMaterial()
		{
			ShaderBakeRenderer.ReleaseTempObject(m_DebugMaterial, true);
			m_DebugMaterial = null;
		}

		/// <summary>
		/// 按<b>同一套</b>面片 / 相机把测试图画一次。返回 false 表示探针不可用，调用方回落到真实结果。
		///
		/// <para>只换"看"的那张图：导出、材质属性表、写回原材质都不受影响，仍走真实材质。</para>
		/// </summary>
		private bool RenderDebugView(bool measureCoverage, bool fromHeartbeat)
		{
			if (!EnsureDebugMaterial())
				return false;

			double start = EditorApplication.timeSinceStartup;
			string error;
			RenderTexture rt = m_Override.RenderWithMaterial(m_Renderer.settings, m_DebugMaterial, out error);
			m_LastRenderMs = (EditorApplication.timeSinceStartup - start) * 1000.0;
			if (rt == null)
			{
				Debug.LogWarning("[ShaderBake] 测试视图渲染失败：" + error);
				return false;
			}

			m_Preview.SetResult(rt, m_Renderer.settings.output.size);
			if (measureCoverage)
				MeasureCoverage(rt);
			if (!fromHeartbeat)
				UpdateFaceOverlay();
			UpdateSizeReadout(!fromHeartbeat);
			return true;
		}

		/// <param name="immediate">
		/// 参数改动、换测试视图这类"画面刚变"的时刻要立刻反映；
		/// 心跳则走 5Hz 节流——数字每帧变一次等于每帧都让状态栏重排一遍
		/// </param>
		private void UpdateSizeReadout(bool immediate)
		{
			double now = EditorApplication.timeSinceStartup;
			if (!immediate && now < m_NextStatusUpdate)
				return;
			m_NextStatusUpdate = now + 0.2;

			if (m_StatusSize != null)
				m_StatusSize.text = m_Renderer.settings.output.width + "×" + m_Renderer.settings.output.height;
			if (m_StatusTime != null)
			{
				string time = m_LastRenderMs < 0.0 ? "-" : m_LastRenderMs.ToString("0.0") + " ms";
				// 只在实时预览开着时报帧率：关掉之后那个数是上一次的，摆着会误导
				if (m_LiveFrameMs > 0.5f && m_AutoRefreshToggle != null && m_AutoRefreshToggle.value)
					time += " · " + Mathf.RoundToInt(1000f / m_LiveFrameMs) + " fps";
				m_StatusTime.text = time;
			}
			UpdateMeshReadout();
		}

		private void UpdateMeshReadout()
		{
			// 顶点数只在状态栏出现：它决定细分够不够用，但不值得占一行界面
			Mesh mesh = m_Renderer != null ? m_Renderer.geometry : null;
			if (m_DebugView != DebugView.None && m_Override.geometry != null)
				mesh = m_Override.geometry;
			if (mesh == null || m_StatusMessage == null)
				return;
			if (m_StatusMessage.text.StartsWith("导出") || m_StatusMessage.text.StartsWith("参数已改"))
				return;
			m_StatusMessage.text = (m_DebugView != DebugView.None ? "测试视图 · " : "已渲染 · ")
			                     + mesh.vertexCount + " 顶点" + CoverageHint();
		}

		/// <summary> 状态栏的覆盖率后缀：把"看着像没渲染"拆成"没画出来"和"画出来但是透明的" </summary>
		private string CoverageHint()
		{
			if (m_LastCoverage < 0f)
				return string.Empty;
			// 测试图 alpha 恒为 1，这里的占比量的是"几何盖住了多少画面"，和透明没关系——
			// 用它一眼看出是相机没框住，还是材质渲不出东西
			if (m_DebugView != DebugView.None)
				return m_LastCoverage <= 0.001f ? " · 画面全空，面片不在视锥里"
				                                : " · 画面被面片盖住 " + Mathf.RoundToInt(m_LastCoverage * 100f) + "%";
			if (m_LastCoverage <= 0.001f)
				return " · 全透明，一个像素都没画出";
			return " · 不透明 " + Mathf.RoundToInt(m_LastCoverage * 100f) + "%";
		}

		private void SetStatus(string message)
		{
			if (m_StatusMessage != null)
				m_StatusMessage.text = message;
		}

		// ── 存取界面状态 ────────────────────────────────────────

		private void RestorePrefs()
		{
			m_Renderer.settings = ShaderBakeSettings.FromJson(EditorPrefs.GetString(kPrefSettings, string.Empty));

			if (m_AutoRefreshToggle != null)
				m_AutoRefreshToggle.SetValueWithoutNotify(EditorPrefs.GetBool(kPrefAutoRefresh, true));
			if (m_CheckerToggle != null)
			{
				bool checker = EditorPrefs.GetBool(kPrefChecker, true);
				m_CheckerToggle.SetValueWithoutNotify(checker);
				if (m_Preview != null) m_Preview.SetCheckerVisible(checker);
			}
			// 开机默认关：这是排查用的视图，不该下次打开窗口还挡着真实结果
			m_DebugView = (DebugView)EditorPrefs.GetInt(kPrefDebugView, (int)DebugView.None);
			UpdateDebugUi();
			if (m_DetailPane != null)
				m_DetailPane.style.width = EditorPrefs.GetFloat(kPrefDetailWidth, kDefaultDetailWidth);

			string assetPath = EditorPrefs.GetString(kPrefSource, string.Empty);
			if (!string.IsNullOrEmpty(assetPath))
			{
				UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
				if (asset is Shader shader)
				{
					if (m_ShaderField != null) m_ShaderField.SetValueWithoutNotify(shader);
					m_Source.Assign(shader);
				}
				else if (asset is Material material)
				{
					if (m_MaterialField != null) m_MaterialField.SetValueWithoutNotify(material);
					m_Source.Assign(material);
				}
			}
		}

		private void SavePrefs()
		{
			if (m_Renderer == null)
				return;
			EditorPrefs.SetString(kPrefSettings, m_Renderer.settings.ToJson());
			if (m_AutoRefreshToggle != null)
				EditorPrefs.SetBool(kPrefAutoRefresh, m_AutoRefreshToggle.value);
			if (m_CheckerToggle != null)
				EditorPrefs.SetBool(kPrefChecker, m_CheckerToggle.value);
			EditorPrefs.SetInt(kPrefDebugView, (int)m_DebugView);
			if (m_DetailPane != null && m_DetailPane.resolvedStyle.width > 1f)
				EditorPrefs.SetFloat(kPrefDetailWidth, m_DetailPane.resolvedStyle.width);
			EditorPrefs.SetString(kPrefSource, SourceAssetPath() ?? string.Empty);
		}

		private string SourceAssetPath()
		{
			if (m_Source == null)
				return null;
			UnityEngine.Object asset = (UnityEngine.Object)m_Source.sourceShader ?? m_Source.sourceMaterial;
			return asset != null ? AssetDatabase.GetAssetPath(asset) : null;
		}

		/// <summary> 没填来源时，把当前选中的 Shader / Material 接下来当来源 </summary>
		private void AdoptSelectionIfEmpty()
		{
			if (m_Renderer != null && m_Renderer.material != null)
				return;
			UnityEngine.Object active = Selection.activeObject;
			if (active is Shader shader)
			{
				if (m_ShaderField != null) m_ShaderField.SetValueWithoutNotify(shader);
				AssignSource(shader, null, false);
			}
			else if (active is Material material)
			{
				if (m_MaterialField != null) m_MaterialField.SetValueWithoutNotify(material);
				AssignSource(null, material, false);
			}
		}

		// ── 资源加载 ────────────────────────────────────────────

		/// <summary>
		/// 按 Packages 路径取，取不到再按名字搜。
		/// 这个包既能以 Packages/ 引用进来，也可能被拷进 Assets 里调试，写死一条路径会在后者上直接白屏。
		/// </summary>
		private static T LoadAsset<T>(string path, string assetName) where T : UnityEngine.Object
		{
			T asset = AssetDatabase.LoadAssetAtPath<T>(path);
			if (asset != null)
				return asset;

			string[] guids = AssetDatabase.FindAssets(assetName);
			foreach (string guid in guids)
			{
				string candidate = AssetDatabase.GUIDToAssetPath(guid);
				if (!Path.GetFileNameWithoutExtension(candidate).Equals(assetName, StringComparison.Ordinal))
					continue;
				asset = AssetDatabase.LoadAssetAtPath<T>(candidate);
				if (asset != null)
					return asset;
			}
			return null;
		}
	}
}
