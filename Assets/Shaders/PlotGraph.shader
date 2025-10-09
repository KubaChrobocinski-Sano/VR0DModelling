// A simple, modern shader for UI plotting that correctly handles transparency.
Shader "Unlit/PlotGraph"
{
    Properties
    {
        // The texture that the C# script draws the plot onto.
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // Set up the correct tags and render queue for a transparent UI element.
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            // Standard blending mode for UI elements.
            Blend SrcAlpha OneMinusSrcAlpha
            
            // Standard settings for a 2D UI shader.
            Cull Off
            Lighting Off
            ZWrite Off
            ZTest [Always]

            // Use a simple programmable shader instead of outdated fixed-function pipeline.
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR; // The RawImage provides a vertex color (for tinting/alpha).
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;

            // Vertex shader: prepares data for the fragment shader.
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color; // Pass the vertex color from the RawImage.
                return o;
            }

            // Fragment shader: determines the final color of each pixel.
            fixed4 frag (v2f i) : SV_Target
            {
                // Multiply the texture color by the vertex color.
                // This is the standard way to handle UI elements, allowing the RawImage's color property to tint the plot.
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                return col;
            }
            ENDCG
        }
    }
}
