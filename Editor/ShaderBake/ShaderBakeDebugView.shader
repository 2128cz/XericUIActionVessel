// 测试视图：把"这一格面片在画面里到底占多大、铺了哪段 UV"变成一眼能读的颜色。
// 三种图都只吃顶点 uv0，不吃贴图、不吃光照、不吃时间，所以画面为空或变形只有几何与 UV 两种原因。
Shader "Hidden/XericUI/ShaderBake/Debug View"
{
	Properties
	{
		_Mode ("视图（0=UV 1=梯度 2=棋盘）", Float) = 0
		_LineThickness ("整数边界线宽", Range(1, 400)) = 90
		_UvTiling ("UV 重复次数", Vector) = (1, 1, 0, 0)
		_UvOffset ("UV 偏移", Vector) = (0, 0, 0, 0)
	}

	SubShader
	{
		Tags { "RenderType" = "Opaque" "ForceSupported" = "True" }

		// 剔除方向交给 Cull Off：这些视图要回答的是几何与 UV，
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

			float _Mode;
			float _LineThickness;
			float4 _UvTiling;
			float4 _UvOffset;

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

			// 每个整数格给一个稳定色相，相邻格不撞色，混在一起也能数出这是第几格
			fixed3 CellHue(float2 cell)
			{
				return HueToRgb(cell.x * 0.618033 + cell.y * 0.381966);
			}

			// 按<b>绝对 UV</b> 压暗整数边界：重复次数大于 1 时格中间也会有一条，
			// 这样"这一格跨了几个 UV 单位"数得出来，而不是被静悄悄地截断
			float EdgeMask(float2 uv)
			{
				// 变量别叫 line：HLSL 把它当保留字，fxc 直接报"unexpected token 'line'"
				float2 toInt = abs(frac(uv + 0.5) - 0.5);
				float edge = min(toInt.x, toInt.y);
				return lerp(0.3, 1.0, saturate(edge * _LineThickness));
			}

			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv0;
				return o;
			}

			// UV 视图：红 = U，绿 = V。角点即 UV，一格四角该是 黑(0,0) 红(1,0) 绿(0,1) 黄(1,1)。
			// (0,0) 必须是纯黑——掺了每格色相就分不出哪一头是原点
			fixed4 UvView(float2 f, float2 uv)
			{
				fixed3 col = fixed3(f.x, f.y, 0.0);
				return fixed4(col * EdgeMask(uv), 1.0);
			}

			// 梯度视图：两条灰度尺。主体沿 U 由黑到白，顶部窄带沿 V 由黑到白，
			// 每 0.1 一档压暗。量的是一格铺满之后刻度在画面上跨了多少像素——
			// 尺子只占画面中间一小段，就说明相机框的是整个网格而不是这一格
			fixed4 GradientView(float2 f, float2 uv)
			{
				float ruler = lerp(f.x, f.y, step(0.86, f.y));
				float tenth = frac(ruler * 10.0);
				float tick = min(tenth, 1.0 - tenth);
				float gray = ruler * lerp(0.35, 1.0, saturate(tick * 40.0));
				gray *= EdgeMask(uv);
				return fixed4(gray, gray, gray, 1.0);
			}

			// 棋盘视图：半 UV 一格的棋盘 + 每格一个色相 + 左下角红、右上角蓝。
			// 方块变矩形=横竖缩放不等，变平行四边形=斜切或透视，两角换位置=U 或 V 被镜像
			fixed4 CheckerView(float2 cell, float2 f, float2 uv)
			{
				float2 q = floor(f * 2.0);
				float check = fmod(q.x + q.y, 2.0);
				fixed3 col = CellHue(cell) * (0.2 + 0.8 * check);

				// 角标放在格内靠边的一小块，压过棋盘本身
				float bottomLeft = max(f.x, f.y);
				float topRight = max(1.0 - f.x, 1.0 - f.y);
				col = lerp(col, fixed3(1.0, 0.16, 0.16), saturate((0.15 - bottomLeft) * 30.0));
				col = lerp(col, fixed3(0.16, 0.4, 1.0), saturate((0.15 - topRight) * 30.0));
				return fixed4(col * EdgeMask(uv), 1.0);
			}

			fixed4 frag(v2f i) : SV_Target
			{
				// 先把 UV 换算成"每格 0~1"：重复次数不为 1 时直接 frac(uv) 会在每个整数处重置，
				// 一格明明跨 0~2 却显示成两段；除以重复次数才是一条连续递增的尺子
				float2 local = (i.uv - _UvOffset.xy) / max(_UvTiling.xy, 0.0001);
				float2 cell = floor(local);
				float2 f = local - cell;

				if (_Mode < 0.5)
					return UvView(f, i.uv);
				if (_Mode < 1.5)
					return GradientView(f, i.uv);
				return CheckerView(cell, f, i.uv);
			}
			ENDCG
		}
	}

	Fallback Off
}
