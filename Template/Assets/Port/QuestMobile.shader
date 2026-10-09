Shader "QuestPort/Mobile" {
 Properties {
  _MainTex("Texture", 2D) = "white" {}
  _Color("Color", Color) = (1,1,1,1)
  _EmissionColor("Emission", Color) = (0,0,0,0)
  _EmissionMap("Emission map", 2D) = "white" {}
  _BumpMap("Normal map", 2D) = "bump" {}
  _BumpScale("Normal strength", Range(0,2)) = 1
  _MaskMap("Metal / AO / Smoothness", 2D) = "white" {}
  _UseMask("Use material mask", Float) = 0
  _Metallic("Metallic", Range(0,1)) = 0
  _Glossiness("Smoothness", Range(0,1)) = .35
  _Cutoff("Cutoff", Float) = 0
  _SrcBlend("Src", Float) = 1
  _DstBlend("Dst", Float) = 0
  _ZWrite("Depth", Float) = 1
  _Cull("Cull", Float) = 2
  _ThemeNote("Themed note",Float)=0
 }
 SubShader {
  Tags { "RenderType"="Opaque" }
  Pass {
   Blend [_SrcBlend] [_DstBlend]
   ZWrite [_ZWrite]
   Cull [_Cull]
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma shader_feature_local _NORMALMAP
   #pragma shader_feature_local _MASKMAP
   #pragma shader_feature_local _REFLECTIONS
   #pragma shader_feature_local _EMISSION
   #pragma multi_compile_fog
   #pragma target 3.0
   #include "UnityCG.cginc"
   struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float4 tangent:TANGENT; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; half3 normal:TEXCOORD1; half3 tangent:TEXCOORD2; half3 bitangent:TEXCOORD3; float3 world:TEXCOORD4; UNITY_FOG_COORDS(5) UNITY_VERTEX_OUTPUT_STEREO };
   sampler2D _MainTex, _EmissionMap, _BumpMap, _MaskMap;
   samplerCUBE _QuestReflection;
   float4 _MainTex_ST, _Color, _EmissionColor;
   float _Cutoff, _BumpScale, _UseMask, _Metallic, _Glossiness;
   half3 _QuestLightDir, _QuestLightColor, _QuestAmbient;
   half4 _QuestNoteColor, _QuestNoteEmission;
   half _ThemeNote, _QuestFlash;
   v2f vert(appdata v) {
    v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.vertex=UnityObjectToClipPos(v.vertex); o.uv=TRANSFORM_TEX(v.uv,_MainTex);
    o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
    o.normal=UnityObjectToWorldNormal(v.normal);
    o.tangent=UnityObjectToWorldDir(v.tangent.xyz);
    o.bitangent=cross(o.normal,o.tangent)*v.tangent.w*unity_WorldTransformParams.w;
    UNITY_TRANSFER_FOG(o,o.vertex); return o;
   }
   fixed4 frag(v2f i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    fixed4 c=tex2D(_MainTex,i.uv)*lerp(_Color,_QuestNoteColor,_ThemeNote); clip(c.a-_Cutoff);
    half3 n=normalize(i.normal);
    #ifdef _NORMALMAP
     half3 bump=UnpackNormal(tex2D(_BumpMap,i.uv)); bump.xy*=_BumpScale;
     n=normalize(i.tangent*bump.x+i.bitangent*bump.y+n*bump.z);
    #endif
    half4 mask=1;
    #ifdef _MASKMAP
     mask=tex2D(_MaskMap,i.uv);
    #endif
    half metal=_Metallic*lerp(1,mask.r,_UseMask);
    half ao=lerp(1,mask.g,_UseMask);
    half gloss=_Glossiness*lerp(1,mask.a,_UseMask);
    half3 l=normalize(_QuestLightDir);
    half ndl=saturate(dot(n,l));
    half3 diffuse=c.rgb*(_QuestAmbient+.65*ndl*_QuestLightColor)*lerp(.4,1,ao)*(1-metal*.7);
    #ifdef _REFLECTIONS
    half3 v=normalize(_WorldSpaceCameraPos-i.world);
    half3 h=normalize(v+l);
    half3 f0=lerp(half3(.035,.035,.035),c.rgb,metal);
    half fres=pow(1-saturate(dot(n,v)),5);
    half spec=pow(saturate(dot(n,h)),lerp(12,100,gloss))*(.25+gloss)*ndl;
    half3 environment=texCUBElod(_QuestReflection,half4(reflect(-v,n),lerp(7,1,gloss))).rgb;
    c.rgb=diffuse+f0*spec*2+environment*(f0+fres*.12)*(.12+metal*.35)*ao;
    #else
    c.rgb=diffuse;
    #endif
    #ifdef _EMISSION
     c.rgb+=tex2D(_EmissionMap,i.uv).rgb*_EmissionColor.rgb*(1-_ThemeNote);
    #endif
    c.rgb+=_QuestNoteEmission.rgb*_ThemeNote+_QuestFlash*.2;
    UNITY_APPLY_FOG(i.fogCoord,c); return c;
   }
   ENDCG
  }
 }
}
