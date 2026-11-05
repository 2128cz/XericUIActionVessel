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

		private const string kPrefSettings = "XericUI.ShaderBake.Settings";
		private const string kPrefAutoRefresh = "XericUI.ShaderBake.AutoRefresh";
		private const string kPrefChecker = "XericUI.ShaderBake.Checker";
		private const string kPrefDetailWidth = "XericUI.ShaderBake.DetailWidth";
		private const string kPrefSource = "XericUI.ShaderBake.SourceAsset";

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
		private ToolbarToggle m_AutoRefreshToggle;
		private ToolbarToggle m_CheckerToggle;
		private Label m_StatusMessage;
		private Label m_StatusSize;
		private Label m_StatusTime;
		private Label m_StatusSource;
		private VisualElement m_DetailPane;
		private VisualElement m_DetailSplit;

		private readonly List<Action> m_Syncers = new List<Action>();

		private bool m_RenderPending;
		private double m_LastRenderMs = -1;
		private int m_SelectedFace;

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
		}

		private void OnDisable()
		{
			SavePrefs();
			if (m_Exporter != null)
				m_Exporter.settingsReplaced -= OnSettingsReplaced;
			if (m_PropertyTable != null)
				m_PropertyTable.Dispose();
			if (m_Preview != null)
				m_Preview.Dispose();
			// 临时材质一定要还：它挂着的是 shader，不销毁就是每次开关窗口漏一份
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
			m_StatusMessage = Require<Label>("status-message");
			m_StatusSize = Require<Label>("status-size");
			m_StatusTime = Require<Label>("status-time");
			m_StatusSource = Require<Label>("status-source");
			m_DetailPane = Require<VisualElement>("detail-pane");
			m_DetailSplit = Require<VisualElement>("detail-split");
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
				render.clicked += DoRender;

			if (m_AutoRefreshToggle != null)
				m_AutoRefreshToggle.RegisterValueChangedCallback(evt => { SavePrefs(); });

			if (m_CheckerToggle != null)
				m_CheckerToggle.RegisterValueChangedCallback(evt =>
				{
					if (m_Preview != null) m_Preview.SetCheckerVisible(evt.newValue);
					SavePrefs();
				});

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
			Collapsible("sec-source", "sec-source-body");
			Collapsible("sec-output", "sec-output-body");
			Collapsible("sec-surface", "sec-surface-body");
			Collapsible("sec-camera", "sec-camera-body");
			Collapsible("sec-globals", "sec-globals-body");
			Collapsible("sec-props", "sec-props-body");
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
			Label note = rootVisualElement.Q<Label>("props-note");
			if (note != null)
				note.text = m_Source.shader == null
					? "未指定 shader"
					: (m_PropertyTable.rowCount + " 项可编辑属性（隐藏属性不列出）");
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
					: "选中第 " + (m_SelectedFace + 1) + " 格：UV (" + cell.uvX + ", " + cell.uvY + ")";
			}
			SetFaceFieldsEnabled(!meshMode);
			UpdateFaceOverlay();
		}

		private void SetFaceFieldsEnabled(bool enabled)
		{
			IntegerField x = rootVisualElement.Q<IntegerField>("face-uv-x");
			IntegerField y = rootVisualElement.Q<IntegerField>("face-uv-y");
			if (x != null) x.SetEnabled(enabled);
			if (y != null) y.SetEnabled(enabled);
		}

		/// <summary> 把选中格的屏幕框和 UV 编号贴到浏览图上 </summary>
		private void UpdateFaceOverlay()
		{
			if (m_Preview == null || m_Renderer == null)
				return;
			ShaderBakeSurfaceSettings surface = m_Renderer.settings.surface;
			if (surface.kind == ShaderBakeSurfaceKind.Mesh && surface.sourceMesh != null)
			{
				m_Preview.SetFaceSelection(false, Vector2.zero, Vector2.zero, string.Empty);
				return;
			}

			Vector2 min, max;
			if (!m_Renderer.TryGetFaceViewportBounds(m_SelectedFace, out min, out max))
			{
				m_Preview.SetFaceSelection(false, Vector2.zero, Vector2.zero, string.Empty);
				return;
			}
			ShaderBakeFaceCell cell = surface.GetFace(m_SelectedFace);
			m_Preview.SetFaceSelection(true, min, max, "UV (" + cell.uvX + ", " + cell.uvY + ")");
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
		}

		private void SetCamPos(float x, float y, float z) { m_Renderer.settings.camera.position = new Vector3(x, y, z); }
		private void SetCamRot(float x, float y, float z) { m_Renderer.settings.camera.eulerAngles = new Vector3(x, y, z); }

		private void FitCamera()
		{
			m_Renderer.FitCameraToGeometry();
			Commit();
			SetStatus("相机已框住面片");
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

		private void BindFloat(string name, Func<float> get, Action<float> set)
		{
			FloatField field = rootVisualElement.Q<FloatField>(name);
			if (field == null)
			{
				Debug.LogError("[ShaderBake] UXML 里缺控件 " + name);
				return;
			}
			// 延迟提交：一边打字一边回写会把光标和"0."这类中间输入搅乱，等回车/失焦再收
			field.isDelayed = true;
			m_Syncers.Add(() => field.SetValueWithoutNotify(get()));
			field.RegisterValueChangedCallback(evt =>
			{
				set(evt.newValue);
				Commit();
			});
		}

		private void BindInt(string name, Func<int> get, Action<int> set)
		{
			IntegerField field = rootVisualElement.Q<IntegerField>(name);
			if (field == null)
			{
				Debug.LogError("[ShaderBake] UXML 里缺控件 " + name);
				return;
			}
			field.isDelayed = true;
			m_Syncers.Add(() => field.SetValueWithoutNotify(get()));
			field.RegisterValueChangedCallback(evt =>
			{
				set(evt.newValue);
				Commit();
			});
		}

		private void BindBool(string name, Func<bool> get, Action<bool> set)
		{
			Toggle field = rootVisualElement.Q<Toggle>(name);
			if (field == null)
			{
				Debug.LogError("[ShaderBake] UXML 里缺控件 " + name);
				return;
			}
			m_Syncers.Add(() => field.SetValueWithoutNotify(get()));
			field.RegisterValueChangedCallback(evt =>
			{
				set(evt.newValue);
				Commit();
			});
		}

		private void BindColor(string name, Func<Color> get, Action<Color> set)
		{
			ColorField field = rootVisualElement.Q<ColorField>(name);
			if (field == null)
			{
				Debug.LogError("[ShaderBake] UXML 里缺控件 " + name);
				return;
			}
			m_Syncers.Add(() => field.SetValueWithoutNotify(get()));
			field.RegisterValueChangedCallback(evt =>
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

		private void RequestRender()
		{
			if (m_AutoRefreshToggle != null && !m_AutoRefreshToggle.value)
			{
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
			m_RenderPending = false;
			if (m_Renderer == null || m_Preview == null)
				return;

			double start = EditorApplication.timeSinceStartup;
			RenderTexture rt = m_Renderer.Render();
			m_LastRenderMs = (EditorApplication.timeSinceStartup - start) * 1000.0;

			if (rt == null)
			{
				m_Preview.Clear();
				SetStatus(string.IsNullOrEmpty(m_Renderer.lastError) ? "渲染失败" : m_Renderer.lastError);
				UpdateSizeReadout();
				return;
			}

			m_Preview.SetResult(rt, m_Renderer.settings.output.size);
			UpdateFaceOverlay();
			UpdateSizeReadout();
			if (string.IsNullOrEmpty(m_StatusMessage.text) || m_StatusMessage.text.StartsWith("导出"))
				SetStatus("已渲染");
			SavePrefs();
		}

		private void UpdateSizeReadout()
		{
			if (m_StatusSize != null)
				m_StatusSize.text = m_Renderer.settings.output.width + "×" + m_Renderer.settings.output.height;
			if (m_StatusTime != null)
				m_StatusTime.text = m_LastRenderMs < 0.0 ? "-" : m_LastRenderMs.ToString("0.0") + " ms";
			UpdateMeshReadout();
		}

		private void UpdateMeshReadout()
		{
			// 顶点数只在状态栏出现：它决定细分够不够用，但不值得占一行界面
			Mesh mesh = m_Renderer != null ? m_Renderer.geometry : null;
			if (mesh == null || m_StatusMessage == null)
				return;
			if (m_StatusMessage.text.StartsWith("导出") || m_StatusMessage.text.StartsWith("参数已改"))
				return;
			m_StatusMessage.text = "已渲染 · " + mesh.vertexCount + " 顶点";
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
