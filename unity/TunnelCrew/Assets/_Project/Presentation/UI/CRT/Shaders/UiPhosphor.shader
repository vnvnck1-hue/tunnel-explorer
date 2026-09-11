Shader "TunnelCrew/CRT/UI phosphor"
{
    Properties
    {
        [PerRendererData] _MainTex("Icon",2D)="white"{}
        _Color("Tint",Color)=(1,.8392,.3529,1)
        _StencilComp("Stencil Comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil Operation",Float)=0
        _StencilWriteMask("Stencil Write Mask",Float)=255
        _StencilReadMask("Stencil Read Mask",Float)=255
        _ColorMask("Color Mask",Float)=15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct App { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Vary { float4 position:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float4 local:TEXCOORD1; };
            sampler2D _MainTex; half4 _Color; half4 _TextureSampleAdd; float4 _ClipRect;
            Vary Vert(App v) { Vary o;o.local=v.vertex;o.position=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o; }
            half4 Frag(Vary i):SV_Target
            {
                half4 source=tex2D(_MainTex,i.uv)+_TextureSampleAdd;
                half luminance=dot(source.rgb,half3(.2126,.7152,.0722));
                // Preserve silhouette and engravings. No CRT distortion is baked into the icon.
                half intensity=pow(saturate(luminance),.72);
                half4 result=half4(i.color.rgb*intensity,source.a*i.color.a);
                #ifdef UNITY_UI_CLIP_RECT
                result.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a-.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
