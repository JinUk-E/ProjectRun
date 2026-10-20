Shader "MoraeGames/MinimapVisionFog"
{
    Properties
    {
        [PerRendererData] _MainTex ("Map", 2D) = "white" {}
        _MapSize ("Map width", Float) = 60
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _MapSize;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            Output Vert(Input v)
            {
                Output o; o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;
            }
            fixed4 Frag(Output i):SV_Target
            {
                fixed4 color=tex2D(_MainTex,i.uv)*i.color;
                float fog=smoothstep(10,14,length(i.uv-.5)*_MapSize);
                color.rgb=lerp(color.rgb,fixed3(.09,.13,.18),fog*.22);
                return color;
            }
            ENDCG
        }
    }
}
