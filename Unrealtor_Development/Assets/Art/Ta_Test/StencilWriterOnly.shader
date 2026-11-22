Shader "Custom/StencilWriterOnly"
{
     SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1" }
        ZWrite Off  ZTest LEqual
        ColorMask 0
        Pass { Name "WriteStencil"
            Stencil { Ref 1  Comp Always  Pass Replace } }
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+1" }
        ZWrite Off  ZTest LEqual
        ColorMask 0
        Pass { Name "WriteStencilTransparent"
            Stencil { Ref 1  Comp Always  Pass Replace } }
    }
}
