// Made with Amplify Shader Editor v1.9.9.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "LinRuochen/UI/RoundPolygon"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        [IntRange] _Sides( "Sides", Range( 3, 10 ) ) = 4
        _Roundness( "Roundness", Range( 0, 1 ) ) = 0.32
        _Width( "Width", Range( 0, 1.5 ) ) = 0.5
        _Height( "Height", Range( 0, 1.5 ) ) = 0.5
        _Rotation( "Rotation", Range( 0, 360 ) ) = 0
        _ZeroColor( "Zero Color", Color ) = ( 0, 0, 0, 0 )
        [Toggle( _ENABLEBORDER_ON )] _EnableBorder( "Enable Border", Float ) = 0
        _DeltaWidth( "Delta Width", Range( 0, 0.75 ) ) = 0.5
        _DeltaHeight( "Delta Height", Range( 0, 0.75 ) ) = 0.5
        _BorderColor( "Border Color", Color ) = ( 1, 1, 1, 1 )
        _Transform( "Transform", Vector ) = ( 1, 1, 0, 0 )
        _TransformBorder( "Transform Border", Vector ) = ( 1, 1, 0, -0.2 )

    }

    SubShader
    {
		LOD 0

        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

        Stencil
        {
        	Ref [_Stencil]
        	ReadMask [_StencilReadMask]
        	WriteMask [_StencilWriteMask]
        	Comp [_StencilComp]
        	Pass [_StencilOp]
        }


        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        
        Pass
        {
            Name "Default"
        CGPROGRAM
            #define ASE_VERSION 19901

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #define ASE_NEEDS_FRAG_COLOR
            #define ASE_NEEDS_TEXTURE_COORDINATES0
            #define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
            #pragma shader_feature_local _ENABLEBORDER_ON


            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4  mask : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
                
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            uniform float4 _ZeroColor;
            uniform float4 _Transform;
            uniform float _Rotation;
            uniform float _Width;
            uniform float _Height;
            uniform float _Sides;
            uniform float _Roundness;
            uniform float4 _BorderColor;
            uniform float4 _TransformBorder;
            uniform float _DeltaWidth;
            uniform float _DeltaHeight;


            v2f vert(appdata_t v )
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                

                v.vertex.xyz +=  float3( 0, 0, 0 ) ;

                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.worldPosition = v.vertex;
                OUT.vertex = vPosition;

                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                float2 maskUV = (v.vertex.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
                OUT.texcoord = v.texcoord;
                OUT.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN ) : SV_Target
            {
                //Round up the alpha color coming from the interpolator (to 1.0/256.0 steps)
                //The incoming alpha could have numerical instability, which makes it very sensible to
                //HDR color transparency blend, when it blends with the world's texture.
                const half alphaPrecision = half(0xff);
                const half invAlphaPrecision = half(1.0/alphaPrecision);
                IN.color.a = round(IN.color.a * alphaPrecision)*invAlphaPrecision;

                float2 texCoord7 = IN.texcoord.xy * (_Transform).xy + (_Transform).zw;
                float temp_output_11_0 = radians( _Rotation );
                float cos8 = cos( temp_output_11_0 );
                float sin8 = sin( temp_output_11_0 );
                float2 rotator8 = mul( texCoord7 - float2( 0.5,0.5 ) , float2x2( cos8 , -sin8 , sin8 , cos8 )) + float2( 0.5,0.5 );
                float2 break188_g2 = ( ( rotator8 * float2( 2,2 ) ) - float2( 1,1 ) );
                float temp_output_197_0_g2 = _Width;
                float temp_output_191_0_g2 = _Height;
                float2 appendResult181_g2 = (float2(( break188_g2.x / ( temp_output_197_0_g2 + ( temp_output_197_0_g2 * 1E-06 ) ) ) , ( break188_g2.y / ( temp_output_191_0_g2 + ( temp_output_191_0_g2 * 1E-06 ) ) )));
                float fullAngle201_g2 = ( 6.28318548202515 / floor( abs( _Sides ) ) );
                float temp_output_122_0_g2 = ( fullAngle201_g2 / 2.0 );
                float temp_output_120_0_g2 = cos( temp_output_122_0_g2 );
                #if ( SHADER_TARGET >= 50 )
                float recip118_g2 = rcp( temp_output_120_0_g2 );
                #else
                float recip118_g2 = 1.0 / temp_output_120_0_g2;
                #endif
                float diagonal182_g2 = recip118_g2;
                float2 temp_output_180_0_g2 = ( appendResult181_g2 * diagonal182_g2 );
                float2 break179_g2 = temp_output_180_0_g2;
                float2 appendResult103_g2 = (float2(atan2( break179_g2.y , break179_g2.x ) , length( temp_output_180_0_g2 )));
                float2 break104_g2 = appendResult103_g2;
                float Half_Angle135_g2 = temp_output_122_0_g2;
                float polaruvx170_g2 = abs( ( ( ( ( ( ( UNITY_PI * 0.5 ) + UNITY_PI ) + break104_g2.x ) + Half_Angle135_g2 ) % fullAngle201_g2 ) - Half_Angle135_g2 ) );
                float clampResult130_g2 = clamp( _Roundness , 1E-06 , 1.0 );
                float chamferAngle172_g2 = ( clampResult130_g2 * Half_Angle135_g2 );
                float remainingAngle175_g2 = ( Half_Angle135_g2 - chamferAngle172_g2 );
                float2 appendResult121_g2 = (float2(temp_output_120_0_g2 , sin( Half_Angle135_g2 )));
                float2 temp_output_112_0_g2 = ( ( tan( remainingAngle175_g2 ) / tan( Half_Angle135_g2 ) ) * ( diagonal182_g2 * appendResult121_g2 ) );
                float temp_output_111_0_g2 = length( temp_output_112_0_g2 );
                float temp_output_90_0_g2 = ( 1.0 - temp_output_112_0_g2.x );
                float lerpResult71_g2 = lerp( ( cos( polaruvx170_g2 ) * break104_g2.y ) , ( break104_g2.y / sqrt( ( ( ( temp_output_111_0_g2 * temp_output_111_0_g2 ) + ( temp_output_90_0_g2 * temp_output_90_0_g2 ) ) - ( ( ( cos( ( UNITY_PI - ( ( 1.0 - ( ( polaruvx170_g2 - remainingAngle175_g2 ) / chamferAngle172_g2 ) ) * Half_Angle135_g2 ) ) ) * temp_output_111_0_g2 ) * temp_output_90_0_g2 ) * 2.0 ) ) ) ) , ( ( Half_Angle135_g2 - polaruvx170_g2 ) < chamferAngle172_g2 ? 1.0 : 0.0 ));
                float temp_output_2_0 = saturate( ( ( 1.0 - lerpResult71_g2 ) / fwidth( lerpResult71_g2 ) ) );
                float4 lerpResult12 = lerp( _ZeroColor , IN.color , temp_output_2_0);
                float2 texCoord36 = IN.texcoord.xy * (_TransformBorder).xy + (_TransformBorder).zw;
                float cos37 = cos( temp_output_11_0 );
                float sin37 = sin( temp_output_11_0 );
                float2 rotator37 = mul( texCoord36 - float2( 0.5,0.5 ) , float2x2( cos37 , -sin37 , sin37 , cos37 )) + float2( 0.5,0.5 );
                float2 break188_g3 = ( ( rotator37 * float2( 2,2 ) ) - float2( 1,1 ) );
                float temp_output_197_0_g3 = ( _Width - ( _DeltaWidth * 0.5 ) );
                float temp_output_191_0_g3 = ( _Height - ( _DeltaHeight * 0.5 ) );
                float2 appendResult181_g3 = (float2(( break188_g3.x / ( temp_output_197_0_g3 + ( temp_output_197_0_g3 * 1E-06 ) ) ) , ( break188_g3.y / ( temp_output_191_0_g3 + ( temp_output_191_0_g3 * 1E-06 ) ) )));
                float fullAngle201_g3 = ( 6.28318548202515 / floor( abs( _Sides ) ) );
                float temp_output_122_0_g3 = ( fullAngle201_g3 / 2.0 );
                float temp_output_120_0_g3 = cos( temp_output_122_0_g3 );
                #if ( SHADER_TARGET >= 50 )
                float recip118_g3 = rcp( temp_output_120_0_g3 );
                #else
                float recip118_g3 = 1.0 / temp_output_120_0_g3;
                #endif
                float diagonal182_g3 = recip118_g3;
                float2 temp_output_180_0_g3 = ( appendResult181_g3 * diagonal182_g3 );
                float2 break179_g3 = temp_output_180_0_g3;
                float2 appendResult103_g3 = (float2(atan2( break179_g3.y , break179_g3.x ) , length( temp_output_180_0_g3 )));
                float2 break104_g3 = appendResult103_g3;
                float Half_Angle135_g3 = temp_output_122_0_g3;
                float polaruvx170_g3 = abs( ( ( ( ( ( ( UNITY_PI * 0.5 ) + UNITY_PI ) + break104_g3.x ) + Half_Angle135_g3 ) % fullAngle201_g3 ) - Half_Angle135_g3 ) );
                float clampResult130_g3 = clamp( _Roundness , 1E-06 , 1.0 );
                float chamferAngle172_g3 = ( clampResult130_g3 * Half_Angle135_g3 );
                float remainingAngle175_g3 = ( Half_Angle135_g3 - chamferAngle172_g3 );
                float2 appendResult121_g3 = (float2(temp_output_120_0_g3 , sin( Half_Angle135_g3 )));
                float2 temp_output_112_0_g3 = ( ( tan( remainingAngle175_g3 ) / tan( Half_Angle135_g3 ) ) * ( diagonal182_g3 * appendResult121_g3 ) );
                float temp_output_111_0_g3 = length( temp_output_112_0_g3 );
                float temp_output_90_0_g3 = ( 1.0 - temp_output_112_0_g3.x );
                float lerpResult71_g3 = lerp( ( cos( polaruvx170_g3 ) * break104_g3.y ) , ( break104_g3.y / sqrt( ( ( ( temp_output_111_0_g3 * temp_output_111_0_g3 ) + ( temp_output_90_0_g3 * temp_output_90_0_g3 ) ) - ( ( ( cos( ( UNITY_PI - ( ( 1.0 - ( ( polaruvx170_g3 - remainingAngle175_g3 ) / chamferAngle172_g3 ) ) * Half_Angle135_g3 ) ) ) * temp_output_111_0_g3 ) * temp_output_90_0_g3 ) * 2.0 ) ) ) ) , ( ( Half_Angle135_g3 - polaruvx170_g3 ) < chamferAngle172_g3 ? 1.0 : 0.0 ));
                float4 lerpResult29 = lerp( lerpResult12 , _BorderColor , saturate( ( temp_output_2_0 - saturate( ( ( 1.0 - lerpResult71_g3 ) / fwidth( lerpResult71_g3 ) ) ) ) ));
                #ifdef _ENABLEBORDER_ON
                float4 staticSwitch24 = lerpResult29;
                #else
                float4 staticSwitch24 = lerpResult12;
                #endif
                

                half4 color = staticSwitch24;

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color.a *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                color.rgb *= color.a;

                return color;
            }
        ENDCG
        }
    }
    CustomEditor "AmplifyShaderEditor.MaterialInspector"
	
	Fallback Off
}
/*ASEBEGIN
Version=19901
Node;AmplifyShaderEditor.Vector4Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;30;-1536,-512;Inherit;False;Property;_Transform;Transform;10;0;Create;True;0;0;0;False;0;False;1,1,0,0;1,1,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector4Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;33;-1536,-256;Inherit;False;Property;_TransformBorder;Transform Border;11;0;Create;True;0;0;0;False;0;False;1,1,0,-0.2;1,1,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;18;-1024,448;Inherit;False;Property;_DeltaHeight;Delta Height;8;0;Create;True;0;0;0;False;0;False;0.5;0.5;0;0.75;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;10;-1280,-368;Inherit;False;Property;_Rotation;Rotation;4;0;Create;True;0;0;0;False;0;False;0;0;0;360;0;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;32;-1280,-448;Inherit;False;False;False;True;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;31;-1280,-512;Inherit;False;True;True;False;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;17;-1024,352;Inherit;False;Property;_DeltaWidth;Delta Width;7;0;Create;True;0;0;0;False;0;False;0.5;0.5;0;0.75;0;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;34;-1280,-192;Inherit;False;False;False;True;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;35;-1280,-256;Inherit;False;True;True;False;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4;-768,64;Inherit;False;Property;_Width;Width;2;0;Create;True;0;0;0;False;0;False;0.5;0;0;1.5;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;5;-768,144;Inherit;False;Property;_Height;Height;3;0;Create;True;0;0;0;False;0;False;0.5;0.5;0;1.5;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;21;-768,352;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.5;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;23;-768,448;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.5;False;1;FLOAT;0
Node;AmplifyShaderEditor.RadiansOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;11;-992,-368;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;7;-1024,-512;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;36;-1024,-256;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3;-768,0;Inherit;False;Property;_Sides;Sides;0;1;[IntRange];Create;True;0;0;0;False;0;False;4;0;3;10;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;6;-768,224;Inherit;False;Property;_Roundness;Roundness;1;0;Create;True;0;0;0;False;0;False;0.32;0.32;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;19;-576,320;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;22;-576,416;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RotatorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;8;-768,-512;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RotatorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;37;-768,-256;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2;-384,0;Inherit;False;Rounded Polygon;-1;;2;fecbb830e021088498d67321a21209fa;0;5;36;FLOAT2;0,0;False;126;FLOAT;4;False;197;FLOAT;0.5;False;191;FLOAT;0.5;False;128;FLOAT;0.32;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;16;-384,240;Inherit;False;Rounded Polygon;-1;;3;fecbb830e021088498d67321a21209fa;0;5;36;FLOAT2;0,0;False;126;FLOAT;4;False;197;FLOAT;0.5;False;191;FLOAT;0.5;False;128;FLOAT;0.32;False;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;13;-128,96;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;14;-128,-128;Inherit;False;Property;_ZeroColor;Zero Color;5;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;27;-128,304;Inherit;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;12;128,0;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;25;128,272;Inherit;False;Property;_BorderColor;Border Color;9;0;Create;True;0;0;0;False;0;False;1,1,1,1;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;38;144.1035,530.6523;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;29;384,96;Inherit;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;24;640,0;Inherit;False;Property;_EnableBorder;Enable Border;6;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;0;960,0;Float;False;True;-1;3;AmplifyShaderEditor.MaterialInspector;0;3;LinRuochen/UI/RoundPolygon;5056123faa0c79b47ab6ad7e8bf059a4;True;Default;0;0;Default;2;False;True;3;1;False;;10;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;True;True;True;True;True;0;True;_ColorMask;False;False;False;False;False;False;False;True;True;0;True;_Stencil;255;True;_StencilReadMask;255;True;_StencilWriteMask;0;True;_StencilComp;0;True;_StencilOp;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;2;False;;True;0;True;unity_GUIZTestMode;False;True;5;Queue=Transparent=Queue=0;IgnoreProjector=True;RenderType=Transparent=RenderType;PreviewType=Plane;CanUseSpriteAtlas=True;False;False;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;3;False;0;;0;0;Standard;0;0;1;True;False;;False;0
WireConnection;32;0;30;0
WireConnection;31;0;30;0
WireConnection;34;0;33;0
WireConnection;35;0;33;0
WireConnection;21;0;17;0
WireConnection;23;0;18;0
WireConnection;11;0;10;0
WireConnection;7;0;31;0
WireConnection;7;1;32;0
WireConnection;36;0;35;0
WireConnection;36;1;34;0
WireConnection;19;0;4;0
WireConnection;19;1;21;0
WireConnection;22;0;5;0
WireConnection;22;1;23;0
WireConnection;8;0;7;0
WireConnection;8;2;11;0
WireConnection;37;0;36;0
WireConnection;37;2;11;0
WireConnection;2;36;8;0
WireConnection;2;126;3;0
WireConnection;2;197;4;0
WireConnection;2;191;5;0
WireConnection;2;128;6;0
WireConnection;16;36;37;0
WireConnection;16;126;3;0
WireConnection;16;197;19;0
WireConnection;16;191;22;0
WireConnection;16;128;6;0
WireConnection;27;0;2;0
WireConnection;27;1;16;0
WireConnection;12;0;14;0
WireConnection;12;1;13;0
WireConnection;12;2;2;0
WireConnection;38;0;27;0
WireConnection;29;0;12;0
WireConnection;29;1;25;0
WireConnection;29;2;38;0
WireConnection;24;1;12;0
WireConnection;24;0;29;0
WireConnection;0;0;24;0
ASEEND*/
//CHKSM=433F3A80DC4479B363CCE115625C4C9858BF5EBF