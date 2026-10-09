Shader "QuestPort/Water" {
 Properties { _Color("Deep water",Color)=(.035,.16,.21,1) }
 SubShader {
  Tags { "RenderType"="Opaque" "Queue"="Geometry" }
  Pass {
   Cull Off
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma multi_compile_fog
   #pragma target 3.0
   #include "UnityCG.cginc"
   struct appdata { float4 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct v2f { float4 vertex:SV_POSITION; float3 world:TEXCOORD0; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };
   half4 _Color;
   v2f vert(appdata v) {
    v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.vertex=UnityObjectToClipPos(v.vertex); o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
    UNITY_TRANSFER_FOG(o,o.vertex); return o;
   }
   half4 frag(v2f i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float2 phase=i.world.xz*.65+float2(_Time.y*.6,-_Time.y*.4);
    half3 n=normalize(half3(sin(phase.x+phase.y)*.06,1,cos(phase.y-phase.x)*.05));
    half3 view=normalize(_WorldSpaceCameraPos-i.world);
    half fres=pow(1-saturate(dot(n,view)),3);
    half3 sky=half3(.3,.46,.57);
    half highlight=pow(saturate(dot(n,normalize(view+half3(.35,.8,.4)))),64)*.35;
    half4 c=half4(lerp(_Color.rgb,sky,fres*.7)+highlight*half3(1,.9,.7),1);
    UNITY_APPLY_FOG(i.fogCoord,c); return c;
   }
   ENDCG
  }
 }
}
