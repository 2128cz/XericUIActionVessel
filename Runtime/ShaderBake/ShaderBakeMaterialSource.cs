using System;
using UnityEngine;

namespace XericUI.ShaderBake
{
	/// <summary>
	/// 把"用户给的 Shader 或 Material"归一成一张可渲染的材质，并管好这张材质的生死。
	///
	/// <para><b>为什么 Shader 入口要自己造材质</b>：渲染路径只认材质，而 Shader 资产本身不能被修改
	/// （属性默认值存在 Shader 里、不在任何实例上）。造一张临时材质是唯一能就地调参数的路子，
	/// 代价是它不属于任何资产——所以用完必须还，见 <see cref="Dispose"/>。</para>
	///
	/// <para><b>Material 入口不动原资产</b>：直接编辑传进来的材质资产等于改了项目里的文件，
	/// 用户点一下颜色滑块就写脏了一个 .mat。这里也造一份副本，改坏了指望不了别人替你记。</para>
	/// </summary>
	public sealed class ShaderBakeMaterialSource : IDisposable
	{
		/// <summary> 渲染实际用的材质（可能是临时造的副本） </summary>
		public Material material { get; private set; }

		/// <summary> 这份材质是不是自己造的，决定 Dispose 时要不要销毁它 </summary>
		public bool ownsMaterial { get; private set; }

		/// <summary> 用户给的 Shader 入口，用于界面显示与材质属性列表 </summary>
		public Shader sourceShader { get; private set; }

		/// <summary> 用户给的 Material 入口，副本的参数就按它初始化 </summary>
		public Material sourceMaterial { get; private set; }

		/// <summary> 当前生效的 shader（两个入口最终都落到它上面） </summary>
		public Shader shader
		{
			get
			{
				if (sourceShader != null) return sourceShader;
				return material != null ? material.shader : null;
			}
		}

		/// <summary> 给界面看的来源说明：Shader 入口写"临时材质"，Material 入口写"副本" </summary>
		public string description
		{
			get
			{
				if (sourceShader != null) return sourceShader.name + "（临时材质）";
				if (material != null) return material.name + "（副本）";
				return "未指定";
			}
		}

		/// <summary> Shader 入口：建一张临时材质。传 null 表示清空。 </summary>
		public void Assign(Shader shader)
		{
			ReleaseOwned();
			sourceShader = shader;
			sourceMaterial = null;
			if (shader == null)
			{
				material = null;
				return;
			}
			material = new Material(shader)
			{
				name = shader.name + " (Bake)",
				hideFlags = HideFlags.DontSave
			};
			ownsMaterial = true;
		}

		/// <summary> Material 入口：复制一张临时材质，参数与来源一致。传 null 表示清空。 </summary>
		public void Assign(Material source)
		{
			ReleaseOwned();
			sourceShader = null;
			sourceMaterial = source;
			if (source == null)
			{
				material = null;
				return;
			}
			material = new Material(source)
			{
				name = source.name + " (Bake)",
				hideFlags = HideFlags.DontSave
			};
			ownsMaterial = true;
		}

		/// <summary> 把副本当前的参数写回来源材质（用户显式按"应用"才走这里） </summary>
		public bool ApplyToSource()
		{
			if (material == null || sourceMaterial == null)
				return false;
			// CopyPropertiesFromMaterial 只搬它认得的属性，材质整体替换反而会把 hideFlags 带过来
			sourceMaterial.CopyPropertiesFromMaterial(material);
			return true;
		}

		/// <summary> 丢弃副本上的改动，回到来源材质的参数 </summary>
		public bool RevertToSource()
		{
			if (sourceMaterial == null)
				return false;
			Assign(sourceMaterial);
			return true;
		}

		/// <summary> 换 shader 名/属性列表之后要把副本丢干净 </summary>
		public void Clear()
		{
			ReleaseOwned();
			sourceShader = null;
			sourceMaterial = null;
			material = null;
			ownsMaterial = false;
		}

		private void ReleaseOwned(bool immediate)
		{
			if (ownsMaterial && material != null)
				ShaderBakeRenderer.ReleaseTempObject(material, immediate);
			material = null;
			ownsMaterial = false;
		}

		private void ReleaseOwned()
		{
			ReleaseOwned(false);
		}

		/// <summary> 编辑器里退出窗口时按 immediate 释放，显存当场就还回去了 </summary>
		public void Dispose(bool immediate)
		{
			ReleaseOwned(immediate);
			sourceShader = null;
			sourceMaterial = null;
		}

		public void Dispose()
		{
			Dispose(false);
		}
	}
}
