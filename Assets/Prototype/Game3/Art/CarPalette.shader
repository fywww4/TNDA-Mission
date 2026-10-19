Shader "TNDA/Game3 Car Palette"
{
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
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv);
                clip(c.a-.5);
                // Cherry mode: everything except the black outline and windows becomes bands across the car, three
                // sprite pixels wide, alternating stripe A and B (whole texels, so the stripes stay crisp pixel art).
                float row=floor(i.uv.y*_MainTex_TexelSize.w);
                fixed3 stripe=fmod(floor(row/3),2) < .5 ? _SolidColor.rgb : _StripeColor.rgb;
                fixed3 rgb=max(c.r,max(c.g,c.b)) < .3 ? fixed3(0,0,0)
                    : _Solid > .5 ? stripe
                    : min(c.r,min(c.g,c.b)) > .72 ? fixed3(.960784,.960784,.960784) : i.color.rgb;
                return fixed4(rgb,1);
            }
            ENDCG
        }
    }
}
