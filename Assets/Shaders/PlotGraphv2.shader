Shader "Unlit/PlotGraph"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Pass {
            ZWrite Off Cull Off Fog { Mode Off }
            Blend SrcAlpha OneMinusSrcAlpha
            SetTexture [_MainTex] { combine texture }
        }
    }
}