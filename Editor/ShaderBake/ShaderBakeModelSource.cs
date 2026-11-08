using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace XericUI.ShaderBake.Editor
{
	/// <summary>
	/// 网格来源：从场景选中物体取网格，或把外部模型文件拉进工程再取网格。
	///
	/// <para><b>这里只搬网格，不搬层级</b>：烘焙是"一张网格 + 一个材质"离屏渲一次，
	/// 多层物体（带骨骼、带多个材质球）在这里会被拆成"挑一个网格"。
	/// 想要整棵树的效果请先把它们合到一个网格上，或者用 <c>SubMesh</c> 那一格逐层渲。</para>
	/// </summary>
	internal static class ShaderBakeModelSource
	{
		/// <summary> 外部模型文件的落点：不在工程里的文件必须先有个能导入的位置 </summary>
		private const string kImportFolder = "Assets/XericShaderBakeImports";

		/// <summary> 选中物体（含未激活的子物体）里三角数最多的网格 </summary>
		public static Mesh PickFromSelection(out string note)
		{
			note = string.Empty;
			GameObject selected = Selection.activeGameObject;
			if (selected == null)
			{
				note = "场景里没选中物体";
				return null;
			}

			Mesh best = null;
			int bestCount = 0;
			foreach (MeshFilter filter in selected.GetComponentsInChildren<MeshFilter>(true))
			{
				int count = CountTriangles(filter.sharedMesh);
				if (count > bestCount)
				{
					best = filter.sharedMesh;
					bestCount = count;
				}
			}
			foreach (SkinnedMeshRenderer skinned in selected.GetComponentsInChildren<SkinnedMeshRenderer>(true))
			{
				int count = CountTriangles(skinned.sharedMesh);
				if (count > bestCount)
				{
					best = skinned.sharedMesh;
					bestCount = count;
				}
			}

			if (best == null)
				note = selected.name + " 及其子物体里没有网格";
			else
				note = "取自 " + selected.name + "：" + best.name + "（" + bestCount + " 三角）";
			return best;
		}

		/// <summary>
		 /// 选一个模型文件导入并取网格。已经在工程里的文件不复制，只走一次导入。
		 /// </summary>
		public static Mesh ImportFromFile(out string note)
		{
			note = string.Empty;
			string path = EditorUtility.OpenFilePanelWithFilters(
				"导入模型",
				Directory.Exists(kImportFolder) ? kImportFolder : Application.dataPath,
				new[] { "模型文件", "fbx,obj,dae,blend,3ds,ply,stl,glb,gltf", "所有文件", string.Empty });
			if (string.IsNullOrEmpty(path))
			{
				note = "已取消";
				return null;
			}

			string assetPath = ToProjectPath(path);
			if (assetPath == null)
			{
				assetPath = CopyIntoProject(path);
				if (assetPath == null)
				{
					note = "复制到工程失败";
					return null;
				}
			}

			AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
			Mesh best = BiggestMeshAt(assetPath, out int triangleCount);
			if (best == null)
				note = assetPath + " 里没有可用的网格";
			else
				note = "已导入 " + assetPath + "：" + best.name + "（" + triangleCount + " 三角）";
			return best;
		}

		private static int CountTriangles(Mesh mesh)
		{
			if (mesh == null)
				return 0;
			int total = 0;
			for (int i = 0; i < mesh.subMeshCount; i++)
				total += (int)mesh.GetIndexCount(i);
			return total / 3;
		}

		/// <summary> 工程内的绝对路径换成 Assets/... 相对路径，不在工程内返回 null </summary>
		private static string ToProjectPath(string absolutePath)
		{
			string normalized = absolutePath.Replace('\\', '/');
			string dataPath = Application.dataPath.Replace('\\', '/');
			if (!normalized.StartsWith(dataPath + "/", StringComparison.OrdinalIgnoreCase))
				return null;
			return "Assets" + normalized.Substring(dataPath.Length);
		}

		private static string CopyIntoProject(string absolutePath)
		{
			try
			{
				if (!Directory.Exists(kImportFolder))
					Directory.CreateDirectory(kImportFolder);
				string target = kImportFolder + "/" + Path.GetFileName(absolutePath);
				File.Copy(absolutePath, target, true);
				AssetDatabase.Refresh();
				return target;
			}
			catch (Exception e)
			{
				Debug.LogError("[ShaderBake] 复制模型失败：" + e.Message);
				return null;
			}
		}

		/// <summary>
		/// 模型里往往混着 LOD、碰撞体和皮肤网格，挑三角最多的那个通常就是用户想看的主体。
		/// </summary>
		private static Mesh BiggestMeshAt(string assetPath, out int triangleCount)
		{
			triangleCount = 0;
			Mesh best = null;
			var seen = new HashSet<Mesh>();

			foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
			{
				IEnumerable<Mesh> candidates = null;
				var filter = obj as MeshFilter;
				var skinned = obj as SkinnedMeshRenderer;
				var prefab = obj as GameObject;
				if (filter != null && filter.sharedMesh != null)
					candidates = new[] { filter.sharedMesh };
				else if (skinned != null && skinned.sharedMesh != null)
					candidates = new[] { skinned.sharedMesh };
				else if (prefab != null)
					candidates = CollectFromPrefab(prefab);

				if (candidates == null)
					continue;
				foreach (Mesh mesh in candidates)
				{
					if (mesh == null || !seen.Add(mesh))
						continue;
					int count = CountTriangles(mesh);
					if (count > triangleCount)
					{
						best = mesh;
						triangleCount = count;
					}
				}
			}
			return best;
		}

		private static IEnumerable<Mesh> CollectFromPrefab(GameObject prefab)
		{
			var list = new List<Mesh>();
			foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
				if (filter.sharedMesh != null) list.Add(filter.sharedMesh);
			foreach (SkinnedMeshRenderer skinned in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
				if (skinned.sharedMesh != null) list.Add(skinned.sharedMesh);
			Mesh mesh = prefab.GetComponent<Mesh>();
			if (mesh != null) list.Add(mesh);
			return list;
		}
	}
}
