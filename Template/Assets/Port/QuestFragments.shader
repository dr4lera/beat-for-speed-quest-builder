Shader "QuestPort/Fragments" {
 Properties { _MainTex("Texture",2D)="white"{} _Color("Color",Color)=(1,1,1,1) _Brightness("Brightness",Float)=1 _DstBlend("Destination blend",Float)=10 }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" }
 Pass { Blend SrcAlpha [_DstBlend] ZWrite Off Cull Off
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #pragma multi_compile_fog
 #include "UnityCG.cginc"
 struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; fixed4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
 struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };
 sampler2D _MainTex; fixed4 _Color; half _Brightness;
 v2f vert(appdata v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
  o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color*_Color; o.uv=v.uv;
  half lighting=.55+.55*saturate(dot(UnityObjectToWorldNormal(v.normal),normalize(float3(.3,.8,.4))));
  o.color.rgb*=_Brightness*lighting;
  UNITY_TRANSFER_FOG(o,o.vertex); return o; }
 fixed4 frag(v2f i):SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); fixed4 c=tex2D(_MainTex,i.uv)*i.color; UNITY_APPLY_FOG(i.fogCoord,c); return c; }
 ENDCG
 } }
}
