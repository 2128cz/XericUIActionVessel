// UV 状态可视化：把顶点收到的第 0 套 UV 直接当颜色画出来。
// 它只吃 uv，不吃贴图、不吃光照，所以"这一格到底铺了哪段 UV"没有第二种解释。
Shader "Hidden/XericUI/ShaderBake/UV Debug"
{
	Properties
	{
		_LineThickness ("整数边界线宽", Range(1, 400)) = 90
		_CellMix ("每格色相强度", Range(0, 1)) = 0.22
	}

	SubShader
	{
		Tags { "RenderType" = "Opaque" "ForceSupported" = "True" }

		// 剔除方向交给 Cull Off：这个视图要回答的是"UV 长什么样"，
		// 不该再被正面约定影响一次，否则空画面时两条原因会搅在一起
		Cull Off
		ZWrite Off
		Lighting Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			float _LineThickness;
			float _CellMix;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv0 : TEXCOORD0;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			fixed3 HueToRgb(float hue)
			{
				// 格编号可以是负的，先 floor 取小数保证 hue 落在 0~1，否则色相会随符号跳变
				hue = hue - floor(hue);
				float3 rgb = saturate(abs(hue * 6.0 + float3(0.0, 4.0, 2.0)) - 3.0);
				return rgb;
			}

			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv0;
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				float2 cell = floor(i.uv);
				float2 f = i.uv - cell;

				// 角点即 UV：左下黑、右下红（U 到 1）、左上绿（V 到 1）、右上黄。
				// 四角是不是 00 / 10 / 01 / 11，看这个就够了
				fixed4 col = fixed4(f.x, f.y, 0.06, 1.0);

				// 每个整数格给一个稳定色相，相邻格不撞色，混在一起也能数出第几格
				col.rgb = lerp(col.rgb, HueToRgb(cell.x * 0.618033 + cell.y * 0.381966), _CellMix);

				// 整数边界压暗：格与格之间、UV 单位边界都显出来
				float line = min(min(f.x, 1.0 - f.x), min(f.y, 1.0 - f.y));
				col.rgb *= lerp(0.3, 1.0, saturate(line * _LineThickness));
				return col;
			}
			ENDCG
		}
	}

	Fallback Off
}
