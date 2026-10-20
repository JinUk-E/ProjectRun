Shader "MoraeGames/LocalVisionFog"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _VisionCenter;
            float4 _VisionRadii;
            float4 _OwnerCameraPosition;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.world);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                clip(.01 - distance(_WorldSpaceCameraPos, _OwnerCameraPosition.xyz));
                float3 ray = unity_OrthoParams.w > .5 ? -UNITY_MATRIX_V[2].xyz : normalize(input.world - _WorldSpaceCameraPos);
                float distanceToGround = -input.world.y / min(ray.y, -.001);
                float2 ground = (input.world + ray * max(0, distanceToGround)).xz;
                float range = distance(ground, _VisionCenter.xz);
                float fog = smoothstep(_VisionRadii.x, _VisionRadii.y, range);
                fog = _VisionRadii.z > .5 ? fog : 1;
                float cloud = sin(ground.x * .24 + _Time.y * .12) * sin(ground.y * .19 - _Time.y * .09);
                return half4(half3(.09,.13,.18) + cloud * .018, fog * .32);
            }
            ENDHLSL
        }
    }
}
