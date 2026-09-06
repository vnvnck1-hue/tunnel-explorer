using System;
using System.Collections.Generic;
using UnityEngine;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 원본 <c>AU.tone</c> / <c>AU.hit</c>(Web Audio 절차 합성)를 오프라인으로 렌더해 <see cref="AudioClip"/> 으로 만든다.
    /// Unity 에는 Web Audio 그래프가 없으므로 같은 파라미터(파형·주파수 슬라이드·지수 엔벌로프·바이쿼드 필터·노이즈 버퍼)를
    /// 샘플 단위로 계산한다. 결과는 이름별로 한 번만 굽고 재사용한다.
    /// 원본 SFX 버스의 소프트 리미터(|x|≤.62 선형 · 그 위 tanh)도 렌더 끝에 적용한다.
    /// </summary>
    public static class AudioSynth
    {
        public const int Rate = 44100;

        public enum Wave : byte { Sine, Triangle, Square, Sawtooth }
        public enum Filter : byte { None, Lowpass, Bandpass, Highpass }

        /// <summary>한 겹 — tone(오실레이터) 또는 hit(노이즈).</summary>
        public struct Layer
        {
            public bool Noise;
            public Wave Type; public float F, Dur, Slide, Peak, Atk, At, Lp;   // tone
            public float F0, F1, Q, RateMul; public Filter Ft;               // hit
        }

        /// <summary>AU.tone(f, dur, {type, g, slide, lp, atk, at}) — peak 는 g × 카테고리 배율을 이미 곱한 값.</summary>
        public static Layer Tone(float f, float dur, float peak, Wave type = Wave.Triangle, float slide = 0, float lp = 0, float atk = .006f, float at = 0)
            => new Layer { Type = type, F = f, Dur = dur, Peak = peak, Slide = slide, Lp = lp, Atk = atk, At = at };
        /// <summary>AU.hit(dur, {f0, f1, g, q, rate, ft, at}).</summary>
        public static Layer Hit(float dur, float f0, float f1, float peak, float q = 1.1f, float rate = 1f, Filter ft = Filter.Bandpass, float at = 0)
            => new Layer { Noise = true, Dur = dur, F0 = f0, F1 = f1, Peak = peak, Q = q, RateMul = rate, Ft = ft, At = at };

        static float[] _noise;
        /// <summary>원본 노이즈 버퍼 — 0.5초, (1-i/n)^.5 로 감쇠.</summary>
        static float[] NoiseBuffer()
        {
            if (_noise != null) return _noise;
            int n = Rate / 2; _noise = new float[n];
            var rng = new System.Random(7);
            for (int i = 0; i < n; i++) _noise[i] = (float)((rng.NextDouble() * 2 - 1) * Math.Sqrt(1 - (double)i / n));
            return _noise;
        }

        public static AudioClip Render(string name, IList<Layer> layers)
        {
            float total = .03f;
            foreach (var l in layers) total = Mathf.Max(total, l.At + l.Dur + .03f);
            int len = Mathf.CeilToInt(total * Rate);
            var buf = new float[len];
            foreach (var l in layers) { if (l.Noise) RenderHit(buf, l); else RenderTone(buf, l); }
            // 소프트 리미터 (원본 softCurve): |x|≤.62 선형, 그 위 tanh 포화
            for (int i = 0; i < len; i++)
            {
                float s = buf[i], a = Mathf.Abs(s);
                if (a > .62f) buf[i] = Mathf.Sign(s) * (.62f + .38f * (float)Math.Tanh((a - .62f) / .38f));
            }
            var clip = AudioClip.Create(name, len, 1, Rate, false);
            clip.SetData(buf, 0);
            return clip;
        }

        static float Osc(Wave w, double phase)
        {
            double p = phase - Math.Floor(phase);
            switch (w)
            {
                case Wave.Sine: return (float)Math.Sin(p * Math.PI * 2);
                case Wave.Square: return p < .5 ? 1f : -1f;
                case Wave.Sawtooth: return (float)(2 * p - 1);
                default: return (float)(p < .5 ? 4 * p - 1 : 3 - 4 * p);   // triangle
            }
        }
        /// <summary>exponentialRampToValueAtTime — v0 → v1 을 T 동안 지수로.</summary>
        static float ExpRamp(float v0, float v1, float t, float T) => T <= 0 ? v1 : v0 * Mathf.Pow(v1 / v0, Mathf.Clamp01(t / T));

        static void RenderTone(float[] buf, Layer l)
        {
            int start = Mathf.RoundToInt(l.At * Rate), n = Mathf.CeilToInt(l.Dur * Rate);
            double phase = 0; float peak = Mathf.Max(.0002f, l.Peak);
            var lp = l.Lp > 0 ? new Biquad() : null;
            if (lp != null) lp.Set(Filter.Lowpass, l.Lp, .707f);
            for (int i = 0; i < n && start + i < buf.Length; i++)
            {
                float t = i / (float)Rate;
                float f = l.Slide > 0 ? ExpRamp(l.F, Mathf.Max(30, l.Slide), t, l.Dur) : l.F;
                float env = t < l.Atk ? ExpRamp(.0001f, peak, t, l.Atk) : ExpRamp(peak, .0001f, t - l.Atk, Mathf.Max(.001f, l.Dur - l.Atk));
                float s = Osc(l.Type, phase) * env;
                phase += f / Rate;
                if (lp != null) s = lp.Process(s);
                buf[start + i] += s;
            }
        }
        static void RenderHit(float[] buf, Layer l)
        {
            var noise = NoiseBuffer();
            int start = Mathf.RoundToInt(l.At * Rate), n = Mathf.CeilToInt(l.Dur * Rate);
            float peak = Mathf.Max(.0002f, l.Peak), rate = l.RateMul <= 0 ? 1 : l.RateMul;
            var bq = new Biquad(); var ft = l.Ft == Filter.None ? Filter.Bandpass : l.Ft;
            double pos = 0;
            for (int i = 0; i < n && start + i < buf.Length; i++)
            {
                float t = i / (float)Rate;
                if ((i & 31) == 0) bq.Set(ft, l.F1 > 0 ? ExpRamp(l.F0, l.F1, t, l.Dur) : l.F0, l.Q <= 0 ? 1.1f : l.Q);
                int k = (int)pos; if (k >= noise.Length - 1) break;
                float frac = (float)(pos - k);
                float s = noise[k] + (noise[k + 1] - noise[k]) * frac;
                pos += rate;
                s = bq.Process(s) * ExpRamp(peak, .0001f, t, l.Dur);
                buf[start + i] += s;
            }
        }

        /// <summary>RBJ 바이쿼드 — Web Audio BiquadFilterNode 와 같은 공식.</summary>
        sealed class Biquad
        {
            float b0, b1, b2, a1, a2, x1, x2, y1, y2;
            public void Set(Filter type, float freq, float q)
            {
                freq = Mathf.Clamp(freq, 10, Rate * .45f);
                float w0 = 2 * Mathf.PI * freq / Rate, cs = Mathf.Cos(w0), sn = Mathf.Sin(w0), alpha = sn / (2 * Mathf.Max(.05f, q));
                float a0;
                switch (type)
                {
                    case Filter.Lowpass: b0 = (1 - cs) / 2; b1 = 1 - cs; b2 = (1 - cs) / 2; a0 = 1 + alpha; a1 = -2 * cs; a2 = 1 - alpha; break;
                    case Filter.Highpass: b0 = (1 + cs) / 2; b1 = -(1 + cs); b2 = (1 + cs) / 2; a0 = 1 + alpha; a1 = -2 * cs; a2 = 1 - alpha; break;
                    default: b0 = alpha; b1 = 0; b2 = -alpha; a0 = 1 + alpha; a1 = -2 * cs; a2 = 1 - alpha; break;   // bandpass (constant skirt)
                }
                b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;
            }
            public float Process(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }
    }
}
