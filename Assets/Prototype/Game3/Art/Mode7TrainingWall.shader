Shader "TNDA/Mode7 Training Wall"
{
    // Stage 1's black-box walls (Mode7TrainingGround): see-through so a chase camera looking at a wall is not
    // walled in, and fading further where the camera comes close.
    Properties
    {
        _MainTex ("Wall tile", 2D) = "white" {}
        _Alpha ("Opacity", Range(0, 1)) = 0.55
        _NearAlpha ("Opacity right next to the camera", Range(0, 1)) = 0.12
        _FadeNear ("Fully faded within (world units)", Float) = 1.5
        _FadeFar ("Normal opacity beyond (world units)", Float) = 4
    }
    SubShader
    {
        // After the opaque and cut-out props (which write depth) but before the transparent car and effect sprites,
        // which are always inside the walls and so draw over them.
        Tags { "Queue"="AlphaTest+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        // Inward faces only: a chase camera backed up outside a wall does not see that wall at all.
        Cull Back
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Alpha, _NearAlpha, _FadeNear, _FadeFar;
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, float2(frac(i.uv.x), saturate(i.uv.y)));
                float near = saturate((distance(i.world, _WorldSpaceCameraPos) - _FadeNear) / max(_FadeFar - _FadeNear, .001));
                color.a = lerp(_NearAlpha, _Alpha, near);
                return color;
            }
            ENDCG
        }
    }
}
