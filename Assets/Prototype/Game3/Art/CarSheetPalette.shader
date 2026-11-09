Shader "TNDA/Game3 Car Sheet Palette"
{
    // The 16-direction car sheet (CarSheet.png) paints its body in exactly four key reds. This swaps them for a ramp
    // built from the seat colour (SpriteRenderer.color); every other colour (tyres, glass, stripes, lights) stays.
    // Same _Solid / _SolidColor / _StripeColor block as CarPalette.shader: cherry power and the P3/P4 seats paint
    // the body in bands, three sprite pixels high.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        [PerRendererData] _Solid ("Solid colour (cherry power)", Float) = 0
        [PerRendererData] _SolidColor ("Solid colour / stripe A", Color) = (1,1,1,1)
        [PerRendererData] _StripeColor ("Stripe B", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
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
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _Solid;
            fixed4 _SolidColor, _StripeColor;
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o;
            }
            // Key reds, lightest first: #F87858 #E04020 #B01800 #600000.
            static const float3 keys[4] = { float3(.972549,.470588,.345098), float3(.878431,.25098,.12549),
                                            float3(.690196,.094118,0), float3(.376471,0,0) };
            // How the ramp is built from the seat colour, for each key in turn.
            static const float lift[4] = { .45, 0, 0, 0 };
            static const float shade[4] = { 1, 1, .68, .38 };
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv);
                clip(c.a-.5);
                // Match and build the ramp in sRGB: in a Linear project the texture and colours arrive linearised.
                float3 srgb=c.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                srgb=LinearToGammaSpace(c.rgb);
                #endif
                int key=-1;
                for (int k=0;k<4;k++) if (all(abs(srgb-keys[k])<.02)) key=k;
                if (key<0) return fixed4(c.rgb,1);
                float row=floor(i.uv.y*_MainTex_TexelSize.w);
                float3 seat=_Solid>.5 ? (fmod(floor(row/3),2)<.5 ? _SolidColor.rgb : _StripeColor.rgb) : i.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                seat=LinearToGammaSpace(seat);
                #endif
                float3 rgb=lerp(seat,float3(1,1,1),lift[key])*shade[key];
                #ifndef UNITY_COLORSPACE_GAMMA
                rgb=GammaToLinearSpace(rgb);
                #endif
                return fixed4(rgb,1);
            }
            ENDCG
        }
    }
}
