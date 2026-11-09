Shader "TNDA/Mode7 Boundary"
{
    Properties
    {
        _MainTex ("Tiles", 2D) = "white" {}
        _FrameCount ("Horizontal animation frames", Float) = 1
        _Frame ("Frame", Float) = 0
        _Bob ("Spectator hop in pixels", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        // The chase camera can travel outside the arena: hide the back of the near wall so it cannot fill the view.
        Cull Back
        ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _FrameCount, _Frame, _Bob;
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = float2((frac(i.uv.x) + _Frame) / _FrameCount, saturate(i.uv.y));
                // The guardrail stays on the ground while the spectators above it hop by whole source pixels.
                if (uv.y > .31) uv.y -= _Bob / 64;
                fixed4 color = tex2D(_MainTex, uv);
                clip(color.a - .5);
                return color;
            }
            ENDCG
        }
    }
}
