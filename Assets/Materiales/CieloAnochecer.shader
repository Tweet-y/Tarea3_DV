Shader "Juego/CieloAnochecer"
{
    Properties
    {
        _Zenit ("Cielo superior", Color) = (0.045, 0.065, 0.16, 1)
        _Horizonte ("Horizonte", Color) = (0.38, 0.19, 0.23, 1)
        _Suelo ("Bajo horizonte", Color) = (0.035, 0.045, 0.075, 1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Entrada { float4 vertex : POSITION; };
            struct Salida { float4 vertex : SV_POSITION; float3 direccion : TEXCOORD0; };
            half4 _Zenit, _Horizonte, _Suelo;
            Salida vert(Entrada v)
            {
                Salida o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.direccion = v.vertex.xyz;
                return o;
            }
            half4 frag(Salida i) : SV_Target
            {
                float altura = normalize(i.direccion).y;
                half3 color = altura >= 0 ? lerp(_Horizonte.rgb, _Zenit.rgb, pow(saturate(altura), 0.55)) :
                    lerp(_Horizonte.rgb, _Suelo.rgb, saturate(-altura * 5));
                return half4(color, 1);
            }
            ENDCG
        }
    }
}
