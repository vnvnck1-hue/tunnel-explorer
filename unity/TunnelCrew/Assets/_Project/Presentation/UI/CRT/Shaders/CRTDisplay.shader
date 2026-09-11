Shader "TunnelCrew/CRT/Display"
{
    Properties { _History("Previous phosphor frame", 2D) = "black" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "CRT glass and signal"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D(_History);
            float4 _DisplaySize; // output px, reciprocal px
            float4 _Glass; // curvature, vignette, corner radius, feather
            float4 _Signal; // aberration px, noise, jitter px, event amplitude
            float4 _Beam; // period px, scan strength, beam width response, interlace
            float4 _Phosphor; // mask style, mask strength, bloom, frame-rate-adjusted persistence decay
            float4 _Transmission; // bleed, echo, echo px, history validity
            float4 _Timing; // time, roll strength, roll speed, delta time
            float4 _Tone; // neutral brightness, contrast, black floor
            float4 _SignalTiming; // noise Hz, seconds per sync sweep, band count, scan cycles/second

            float Hash(float2 value)
            {
                uint2 p=(uint2)floor(value);
                uint n=p.x*1973u+p.y*9277u+0x68bc21ebu;
                n=(n^(n>>16))*0x7feb352du;
                n=(n^(n>>15))*0x846ca68bu;
                n^=n>>16;
                return (float(n&0x00ffffffu)+.5)*(1.0/16777216.0);
            }
            float3 Sample(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv)).rgb; }
            float Lum(float3 c) { return dot(c, float3(.2126, .7152, .0722)); }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = input.texcoord;
                float2 centered = screenUV * 2 - 1;
                float aspect = _DisplaySize.x / _DisplaySize.y;
                // Symmetric barrel: strength stable in height units, moderated on ultrawide sides.
                float2 shape = centered * float2(min(aspect / 1.777778, 1.3), 1);
                float2 uv = (centered * (1 + _Glass.x * shape.yx * shape.yx) + 1) * .5;
                float2 glassPoint = (uv * 2 - 1) * float2(aspect, 1);
                float radius = _Glass.z;
                float2 q = abs(glassPoint) - float2(aspect - radius - .012, 1 - radius - .012);
                float distance = length(max(q, 0)) + min(max(q.x, q.y), 0) - radius;
                float glass = 1 - smoothstep(-_Glass.w, _Glass.w, distance);
                if (glass < .001) return half4(.0005, .0003, .0002, 1);

                float t = _Timing.x;
                float eventAmount = _Signal.w;
                float cycle = floor(t / _SignalTiming.y);
                float travel = frac(t / _SignalTiming.y);
                float active = 1 - step(.065, travel); // one subtle sync sweep per 5.7 seconds
                float bandY = frac(travel * 15.38 + Hash(float2(cycle, 3)));
                float band = 1 - smoothstep(2, 8, abs(uv.y - bandY) * _DisplaySize.y);
                [unroll] for(int i=1;i<3;i++)
                    if(i<_SignalTiming.z)
                        band=max(band,1-smoothstep(2,8,abs(uv.y-frac(bandY+i*.317))*_DisplaySize.y));
                float eventBand = 1 - smoothstep(3, 14, abs(uv.y - frac(t * 3.1)) * _DisplaySize.y);
                uv.x += clamp(band * active * _Signal.z * sin(t * 37) + eventBand * eventAmount * 1.5,-1.5,1.5) * _DisplaySize.z;
                float radius2=dot(centered,centered);
                float inverseRadius=rsqrt(max(radius2,.00000001));
                float edge = smoothstep(.2, 1.05, radius2*inverseRadius * .8);
                float2 rgbShift = centered*inverseRadius * _DisplaySize.zw * _Signal.x * edge;
                float3 color = float3(Sample(uv + rgbShift).r, Sample(uv).g, Sample(uv - rgbShift).b);

                // Bandwidth-limited relay signal: same-color horizontal bleed, no sepia conversion.
                if (_Transmission.x + _Transmission.y > .001)
                {
                    float3 smear = Sample(uv - float2(1.5 * _DisplaySize.z, 0));
                    color = lerp(color, smear, _Transmission.x);
                    float3 echo = Sample(uv - float2(_Transmission.z * _DisplaySize.z, 0));
                    color = lerp(color, max(color, echo * .7), _Transmission.y);
                }
                // Bright electron beams bloom locally. This does not add ambient color to dark caves.
                if (_Phosphor.z > .001)
                {
                    // Two horizontal taps reproduce the elongated electron beam. The wider
                    // Arcade spot (> .15 bloom) additionally gathers above and below the line.
                    float3 neighbours = (Sample(uv + float2(2 * _DisplaySize.z, 0)) +
                        Sample(uv - float2(2 * _DisplaySize.z, 0))) * .5;
                    if(_Phosphor.z>.15)
                        neighbours=neighbours*.5+(Sample(uv + float2(0, 2 * _DisplaySize.w)) +
                            Sample(uv - float2(0, 2 * _DisplaySize.w)))*.25;
                    color += max(neighbours - .18, 0) * _Phosphor.z;
                }
                float brightness = saturate(Lum(color) * 1.8);
                float scanWave = .5 + .5 * cos((screenUV.y * _DisplaySize.y / _Beam.x+t*_SignalTiming.w)*6.2831853);
                // Beam grows with signal brightness; bright HUD strokes survive scanline troughs.
                float trough = saturate(1-scanWave);
                trough=lerp(trough,trough*trough*trough,brightness*_Beam.z);
                color *= 1 - trough * _Beam.y * (1 - brightness * .35);

                float2 pixel = screenUV * _DisplaySize.xy;
                if (_Phosphor.x > .5)
                {
                    float stripe = frac(pixel.x / 3);
                    float3 grille = float3(stripe < .333, stripe >= .333 && stripe < .667, stripe >= .667);
                    if (_Phosphor.x > 1.5)
                    {
                        float stagger = fmod(floor(pixel.y / 3), 2) * 1.5;
                        float2 dotUV = frac((pixel + float2(stagger, 0)) / float2(3, 3)) - .5;
                        float aperture = 1 - smoothstep(.22, .49, length(dotUV));
                        grille = lerp(float3(.55, .55, .55), grille, .3) * aperture;
                    }
                    // Mask average compensated, preventing the mask from changing world hue.
                    color *= lerp(float3(1, 1, 1), .82 + grille * .54, _Phosphor.y);
                }
                if(_Beam.w>.001)
                {
                    float fields=fmod(floor(pixel.y)+floor(t*30),2);
                    color*=1-fields*_Beam.w;
                }
                if(_Timing.y>.001)
                {
                    float rollDistance=(frac(uv.y-t*_Timing.z)-.5)*7;
                    float roll=saturate(1-rollDistance*rollDistance);
                    color*=1-roll*roll*_Timing.y;
                }
                if(_Signal.y>.0001)
                {
                    float noise = Hash(floor(pixel) + floor(t * _SignalTiming.x) * float2(31, 17)) - .5;
                    color += noise * _Signal.y * (.12 + saturate(Lum(color)) * .5);
                }
                float vignette = smoothstep(.32, 1.4, dot(centered, centered));
                color *= max(.70, 1 - vignette * _Glass.y);
                // Scalar luminance response: display controls never introduce an amber/sepia world grade.
                if(abs(_Tone.x-1)+abs(_Tone.y-1)+_Tone.z>.0001)
                {
                    float luminance=max(0,Lum(color));
                    float adjusted=max(_Tone.z,(luminance-.18)*_Tone.y+.18)*_Tone.x;
                    color=luminance>.0001?color*(adjusted/luminance):adjusted.xxx;
                }

                // Previous FINAL screen UV, not curved UV: history must never be warped twice.
                if (_Phosphor.w > .001 && _Transmission.w > .5)
                {
                    float3 previous = SAMPLE_TEXTURE2D(_History, sampler_LinearClamp, screenUV).rgb;
                    color = max(color, previous * _Phosphor.w);
                }
                float rimEnvelope=saturate(1-abs(distance+.008)*80);
                float rim= rimEnvelope*rimEnvelope*.006;
                color += rim * float3(1, .6, .2);
                return half4(max(color, 0) * glass, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
