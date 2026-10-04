Shader "Poker/Gifts/TimeStop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Screen", 2D) = "white" {}
        _Radius ("Wave progress", Range(0,1)) = 0
        _Strength ("Frozen amount", Range(0,1)) = 1
        _Aspect ("Screen aspect", Float) = 1.777778
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off Lighting Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            float _Radius, _Strength, _Aspect;
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=(i.uv-0.5)*float2(_Aspect,1);
                float distance=length(p);
                float radius=_Radius*(length(float2(_Aspect,1))*0.5+0.12);
                float front=distance-radius;
                float mask=(1-smoothstep(-0.015,0.015,front))*step(0.0001,_Radius);
                float band=exp(-front*front/0.0012)*step(0.001,_Radius)*(1-step(0.999,_Radius));
                float2 direction=p/max(distance,0.001)/float2(_Aspect,1);
                float2 uv=saturate(i.uv+direction*sin(front*145)*band*0.016*_Strength);
                // ScreenCapture's render target has the graphics API's native vertical orientation.
                #if UNITY_UV_STARTS_AT_TOP
                uv.y=1-uv.y;
                #endif
                fixed3 rgb=tex2D(_MainTex,uv).rgb;
                float gray=dot(rgb,float3(0.2126,0.7152,0.0722));
                rgb=lerp(rgb,gray.xxx,mask);
                rgb+=band*0.035;
                return fixed4(rgb,saturate(max(mask,band)*_Strength)*i.color.a);
            }
            ENDCG
        }
    }
}
