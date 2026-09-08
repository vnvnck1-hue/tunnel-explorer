using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 기능명세서 §7.3 광원 분류. 분류마다 그림자·깜빡임·예산 취급이 다르다.
    ///
    /// 명세서 표의 5분류 중 <c>환경광선</c>(문틈·천장 균열)은 메시 또는 셰이더 콘이 필요해
    /// §11.2 VFX 로 미뤘고, 나머지 4분류에 <see cref="Indicator"/> 를 더했다 — 실제 승인
    /// 아트의 드릴 소켓이 반지름 1.0~1.25셀 · 세기 0.25~0.35 로, 방을 밝히는 광원이 아니라
    /// 기계 표시등이다. 이를 작업등과 같이 취급하면 그림자 예산(§13)을 표시등이 먹는다.
    /// </summary>
    public enum LightClass
    {
        /// <summary>탐색광 — 플레이어·크루 손전등. 방향성, 벽 그림자, 노멀 반응.</summary>
        Scout = 0,

        /// <summary>작업등 — 랜턴·문·기계. 고정 소켓, 색 온도, 미세 깜빡임.</summary>
        Worklamp = 1,

        /// <summary>생체·광물광 — 수정·식생·광맥. Emission 중심, 저비용 비그림자.</summary>
        MineralGlow = 2,

        /// <summary>전투광 — 총구·폭발·보스. 짧은 HDR 펄스. 상시 광원이 아니다.</summary>
        Combat = 3,

        /// <summary>표시등 — 계기·패널. 아주 작고 약하다. 그림자 예산에서 제외한다.</summary>
        Indicator = 4,
    }

    /// <summary>
    /// 광원이 노멀맵을 얼마나 정확히 읽을지. URP <c>Light2D.NormalMapQuality</c> 와 짝이다.
    ///
    /// 자체 enum 을 두는 이유는 순수 규칙 코드가 URP 타입에 묶이지 않게 하기 위해서다 —
    /// 매핑은 <see cref="LightSocketRenderer"/> 가 한다.
    /// </summary>
    public enum LightNormalQuality
    {
        /// <summary>노멀을 읽지 않는다. 표시등·광물광처럼 저비용이어야 하는 광원.</summary>
        Disabled = 0,
        Fast = 1,
        Accurate = 2,
    }

    /// <summary>
    /// 분류별 광원 거동(§7.3). 수치를 런타임 코드에 흩어놓지 않기 위한 순수 함수 묶음이다.
    /// </summary>
    public static class LightClassRules
    {
        /// <summary>표시등으로 볼 최대 반지름(셀).</summary>
        public const float IndicatorMaxRangeCells = 1.5f;
        /// <summary>표시등으로 볼 최대 세기.</summary>
        public const float IndicatorMaxIntensity = 0.4f;

        /// <summary>
        /// 이 분류가 그림자를 만들 <b>자격</b>이 있는가.
        ///
        /// 자격이 있어도 예산(§13)에 들어가야 실제로 켜진다 —
        /// <see cref="ShadowBudget"/> 를 볼 것.
        /// </summary>
        public static bool WantsShadow(LightClass c)
            => c == LightClass.Scout || c == LightClass.Worklamp;

        /// <summary>
        /// 그림자를 켤 때의 세기. 탐색광이 주 광원이므로 가장 진하다.
        /// </summary>
        public static float ShadowIntensity(LightClass c)
        {
            switch (c)
            {
                case LightClass.Scout: return 0.85f;
                case LightClass.Worklamp: return 0.6f;
                default: return 0f;
            }
        }

        /// <summary>
        /// 미세 깜빡임의 세기(세기 배율의 진폭). 0 이면 깜빡이지 않는다.
        ///
        /// §7.3 은 작업등에 "미세 깜빡임" 을 요구한다. <b>미세</b>가 핵심이다 —
        /// 크게 흔들면 광과민 옵션(§14 F)에 걸리고 가독성을 해친다.
        /// </summary>
        public static float FlickerAmplitude(LightClass c)
        {
            switch (c)
            {
                case LightClass.Worklamp: return 0.06f;    // 랜턴의 불안정한 빛
                case LightClass.MineralGlow: return 0.04f; // 결정의 느린 숨쉬기
                default: return 0f;
            }
        }

        /// <summary>깜빡임 주파수(Hz). 광물광은 훨씬 느리게 숨쉰다.</summary>
        public static float FlickerHz(LightClass c)
        {
            switch (c)
            {
                case LightClass.Worklamp: return 7.3f;     // 무리수에 가깝게 — 주기가 눈에 띄지 않는다
                case LightClass.MineralGlow: return 0.45f;
                default: return 0f;
            }
        }

        /// <summary>
        /// 깜빡임 배율. <paramref name="time"/> 이 0 이면 항상 1 이므로
        /// 자동 캡처(§16.3)가 결정적으로 유지된다.
        ///
        /// 두 사인을 겹쳐 단순 맥박처럼 보이지 않게 한다. 결과는 언제나 양수다.
        /// </summary>
        public static float FlickerAt(LightClass c, float time, float seed, float amplitudeScale = 1f)
        {
            float amp = FlickerAmplitude(c) * Mathf.Max(0f, amplitudeScale);
            if (amp <= 0f || time == 0f) return 1f;

            float hz = FlickerHz(c);
            float a = Mathf.Sin((time * hz + seed) * Mathf.PI * 2f);
            float b = Mathf.Sin((time * hz * 0.37f + seed * 1.7f) * Mathf.PI * 2f);
            return 1f + amp * (a * 0.65f + b * 0.35f);
        }

        /// <summary>
        /// 이 분류가 노멀맵을 읽는가(§7.1·§7.3).
        ///
        /// <b>기본이 Disabled 인 것이 함정이었다.</b> URP <c>Light2D</c> 의
        /// <c>normalMapQuality</c> 기본값이 <c>Disabled</c> 라서, 아트가 납품한 노멀맵
        /// 46장과 <c>NormalsRendering</c> 패스가 모두 있는데도 조명이 그것을 한 번도
        /// 읽지 않았다. 켜 보니 화면 픽셀의 42%가 달라졌다(2026-09-08 실측).
        ///
        /// §7.3 은 탐색광에 "노멀 반응" 을 명시하고, 생체·광물광은 "저비용 비그림자" 다.
        /// 그래서 탐색광만 Accurate, 작업등은 Fast, 나머지는 Disabled 로 둔다.
        /// 품질 단계 Low 의 "노멀 단순화" 는 여기서 한 단계 낮추는 것으로 구현한다.
        /// </summary>
        public static LightNormalQuality NormalQuality(LightClass c, VisualQualityTier tier)
        {
            LightNormalQuality q;
            switch (c)
            {
                case LightClass.Scout: q = LightNormalQuality.Accurate; break;
                case LightClass.Worklamp: q = LightNormalQuality.Fast; break;
                default: return LightNormalQuality.Disabled;
            }

            // §13 "Low: 노멀 단순화" — 한 단계 내린다. 끄지는 않는다(형태가 사라진다).
            if (tier == VisualQualityTier.Low && q == LightNormalQuality.Accurate)
                q = LightNormalQuality.Fast;
            return q;
        }

        /// <summary>
        /// 그림자 광원 예산은 품질 단계가 정한다(§13) —
        /// <see cref="VisualQualityRules.ShadowBudget"/> 를 볼 것.
        /// 예산을 넘는 광원은 그림자만 끄고 빛은 그대로 낸다.
        /// </summary>
        public static int ShadowBudget(VisualQualityTier tier) => VisualQualityRules.ShadowBudget(tier);

        /// <summary>
        /// 그림자 우선순위. 큰 값이 먼저 그림자를 받는다.
        ///
        /// 넓고 센 광원이 그림자를 만들어야 화면이 읽힌다 — 작은 표시등이 예산을
        /// 먹으면 정작 방을 밝히는 랜턴에 그림자가 없어진다.
        /// </summary>
        public static float ShadowPriority(LightClass c, float rangeCells, float intensity)
        {
            if (!WantsShadow(c)) return -1f;
            float classBonus = c == LightClass.Scout ? 1000f : 0f;   // 탐색광은 항상 먼저
            return classBonus + Mathf.Max(0f, rangeCells) * Mathf.Max(0f, intensity);
        }

        /// <summary>
        /// 아트의 <c>emissionMode</c> 와 소켓 규모에서 분류를 유도한다.
        ///
        /// <b>임시 규칙이다.</b> manifest 가 <c>lightClass</c> 를 직접 선언해 주는 것이
        /// 옳고, 그때까지는 실제 승인 패키지(revision 16)의 값에서 유도한다. 유도 규칙을
        /// 여기 한곳에 모아 두어야 계약이 바뀔 때 한 군데만 고친다.
        ///
        /// 실제 값: <c>amber_glass</c>(작업등) · <c>magenta_warning_glass</c>(경고등) ·
        /// <c>cyan_crystal</c>(결정) · <c>amber_cyan_indicators</c>(드릴 표시등).
        /// </summary>
        public static LightClass Classify(string emissionMode, float rangeCells, float intensity)
        {
            // 규모가 먼저다 — 아주 작고 약한 소켓은 무엇이든 표시등이다.
            if (rangeCells > 0f && rangeCells <= IndicatorMaxRangeCells
                && intensity > 0f && intensity <= IndicatorMaxIntensity)
                return LightClass.Indicator;

            string m = emissionMode != null ? emissionMode.ToLowerInvariant() : string.Empty;

            // 광물·생체 발광 — 그림자를 만들지 않고 느리게 숨쉰다.
            if (m.Contains("crystal") || m.Contains("mineral") || m.Contains("moss") || m.Contains("vein"))
                return LightClass.MineralGlow;

            // 총구·폭발은 상시 소켓으로 오지 않지만 이름이 오면 존중한다.
            if (m.Contains("muzzle") || m.Contains("blast") || m.Contains("explos"))
                return LightClass.Combat;

            // 그 밖의 소켓 광원은 작업등이다(랜턴·경고등·문·기계).
            return LightClass.Worklamp;
        }
    }
}
