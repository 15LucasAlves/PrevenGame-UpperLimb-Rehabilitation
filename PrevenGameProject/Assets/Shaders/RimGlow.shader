// RimGlow — contorno Fresnel (rim light) para o objeto controlado pelo Ommo.
// Built-in Render Pipeline (o projeto não usa URP). Aplica um brilho nas bordas
// viradas para longe da câmara, dando um efeito de contorno luminoso sem precisar
// de post-processing/bloom. Usado no dardo ativo (Gamification) e no cubo (Clinical Trial).
Shader "PrevenGame/RimGlow"
{
    Properties
    {
        _Color        ("Cor Base", Color)         = (0.95, 0.95, 0.95, 1)
        _MainTex      ("Albedo (RGB)", 2D)        = "white" {}
        _RimColor     ("Cor do Contorno", Color)  = (1, 1, 1, 1)
        _RimPower     ("Foco do Contorno", Range(0.5, 8.0)) = 3.0
        _RimIntensity ("Intensidade do Contorno", Range(0.0, 6.0)) = 2.0
        _Glossiness   ("Suavidade", Range(0,1))   = 0.4
        _Metallic     ("Metálico", Range(0,1))    = 0.0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        // Surface shader com iluminação Standard; emissão recebe o termo de Fresnel.
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;     // preenchido automaticamente pelo Unity (espaço tangente)
        };

        fixed4 _Color;
        fixed4 _RimColor;
        half   _RimPower;
        half   _RimIntensity;
        half   _Glossiness;
        half   _Metallic;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo    = c.rgb;
            o.Metallic  = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha     = c.a;

            // Fresnel: bordas (normal perpendicular ao olhar) brilham mais.
            half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Emission = _RimColor.rgb * pow(rim, _RimPower) * _RimIntensity;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
