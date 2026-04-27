// ─────────────────────────────────────────────
// CharacterStudio 全局滤镜 Shader
// 用于技能 VFX 全屏后处理（灰度、怀旧、着色、反色、黑白等）
//
// 使用方法：
// 1. 在 Unity 编辑器中打开包含此 Shader 的项目
// 2. 选中此文件，标记为 AssetBundle（如 "cs_global_filter"）
// 3. 构建 AssetBundle 并放置到 Mod 的 Effects/ 目录
// 4. C# 端通过 VfxAssetBundleLoader 加载
//
// 材质属性说明：
//   _Saturation - 饱和度 (0=全灰度, 1=原始)
//   _TintColor  - RGB色调 + Alpha混合强度
//   _Invert     - 反色 (0=正常, 1=反色)
//   _Brightness - 亮度 (1=正常)
//   _Contrast   - 对比度 (1=正常)
// ─────────────────────────────────────────────
Shader "CharacterStudio/GlobalFilter"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Saturation ("Saturation", Range(0, 2)) = 1.0
        _TintColor ("Tint Color", Color) = (1,1,1,0)
        _Invert ("Invert", Range(0, 1)) = 0
        _Brightness ("Brightness", Range(0, 3)) = 1.0
        _Contrast ("Contrast", Range(0, 3)) = 1.0
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "GlobalFilter"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half _Saturation;
            half4 _TintColor;
            half _Invert;
            half _Brightness;
            half _Contrast;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            // 根据亮度权重将 RGB 转为灰度
            half3 desaturate(half3 color, half saturation)
            {
                half gray = dot(color, half3(0.299, 0.587, 0.114));
                return lerp(half3(gray, gray, gray), color, saturation);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                // 1. 饱和度调整
                col.rgb = desaturate(col.rgb, _Saturation);

                // 2. 亮度
                col.rgb *= _Brightness;

                // 3. 对比度（以 0.5 为中心）
                col.rgb = (col.rgb - 0.5) * _Contrast + 0.5;

                // 4. 色调叠加（Alpha 控制混合强度）
                col.rgb = lerp(col.rgb, col.rgb * _TintColor.rgb, _TintColor.a);

                // 5. 反色
                col.rgb = lerp(col.rgb, 1.0 - col.rgb, _Invert);

                return col;
            }
            ENDCG
        }
    }

    Fallback Off
}
