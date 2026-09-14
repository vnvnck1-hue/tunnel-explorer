using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>기능명세서 §13 품질 단계.</summary>
    public enum VisualQualityTier
    {
        /// <summary>노멀 단순화, 그림자 광원 2개, 저층 안개 축소, Bloom 저품질.</summary>
        Low = 0,
        /// <summary>핵심 노멀·접촉 그림자·주요 광원 유지.</summary>
        Medium = 1,
        /// <summary>제작 기준.</summary>
        High = 2,
        /// <summary>추가 먼지·광선·고품질 Bloom과 그림자.</summary>
        Ultra = 3,
    }

    /// <summary>
    /// 품질 단계와 접근성 옵션이 실제로 무엇을 바꾸는가(기능명세서 §13·§10.3).
    ///
    /// <b>가독성은 품질 단계에서 내려가지 않는다.</b> §13 이 못 박았다 —
    /// "가독성에 필요한 벽 정면, 전경 가림, 접촉 AO는 품질 단계에서 제거하지 않는다".
    /// 그래서 <see cref="MayDisable"/> 가 그 세 기능에 대해 항상 false 를 돌려주고,
    /// 테스트가 그 사실을 잠근다. 프레임을 벌려고 이걸 끄면 게임이 안 보인다.
    /// </summary>
    public static class VisualQualityRules
    {
        /// <summary>
        /// 그림자 Light2D 예산(§13).
        ///
        /// 표의 "일반 4개 / 스트레스 8개" 는 <b>성능 봉투</b>이고, 품질 단계 목록의
        /// "Low: 그림자 광원 2개" 는 <b>단계별 값</b>이다. 둘을 섞으면 Low 가 4개를 켠다.
        /// 단계가 값을 정하고, 그 값이 봉투 안에 들어간다.
        /// </summary>
        public static int ShadowBudget(VisualQualityTier tier)
        {
            switch (tier)
            {
                case VisualQualityTier.Low: return 2;
                case VisualQualityTier.Medium: return 4;
                case VisualQualityTier.High: return 4;
                default: return 8;      // Ultra — 스트레스 장면 상한까지 허용한다
            }
        }

        /// <summary>
        /// 노멀 세기 배율. Low 는 "노멀 단순화" 다 — 끄지 않고 줄인다.
        /// 완전히 끄면 벽 정면의 형태가 평평해져 §13 의 가독성 조항과 충돌한다.
        /// </summary>
        public static float NormalStrengthScale(VisualQualityTier tier)
        {
            switch (tier)
            {
                case VisualQualityTier.Low: return 0.45f;
                case VisualQualityTier.Medium: return 0.85f;
                default: return 1f;
            }
        }

        /// <summary>저층 안개 배율(§7.5). Low 는 "저층 안개 축소".</summary>
        public static float FogScale(VisualQualityTier tier)
        {
            switch (tier)
            {
                case VisualQualityTier.Low: return 0.35f;
                case VisualQualityTier.Medium: return 0.8f;
                case VisualQualityTier.High: return 1f;
                default: return 1.15f;  // Ultra — 추가 먼지·연무
            }
        }

        /// <summary>
        /// 광과민 옵션에서 0 이 되는 항목의 배율(§10.3 "흔들림·플래시·Bloom을 줄이는 설정").
        ///
        /// 깜빡임·그레인·화면 흔들림이 여기에 걸린다. <b>줄이는</b> 것이 아니라 0 이다 —
        /// "조금 깜빡임" 은 광과민에 안전하지 않다.
        /// </summary>
        public static float FlashScale(bool reducePhotosensitivity)
            => reducePhotosensitivity ? 0f : 1f;

        /// <summary>카메라 흔들림 배율(§10.3 멀미 옵션).</summary>
        public static float ShakeScale(bool reduceMotion)
            => reduceMotion ? 0f : 1f;

        /// <summary>가독성 필수 기능. 품질 단계로 끌 수 없다(§13).</summary>
        public enum Feature
        {
            /// <summary>벽 정면 — 바닥과 벽을 구분하는 유일한 단서다.</summary>
            WallFront,
            /// <summary>전경 가림 — 끄면 깊이가 사라진다. 페이드는 §6.6 이 따로 정한다.</summary>
            ForegroundOcclusion,
            /// <summary>접촉 AO — 물체가 바닥에 붙어 있음을 보여 준다.</summary>
            ContactAo,
            /// <summary>가려진 캐릭터 실루엣 — §6.6·§15.2 가독성 장치다.</summary>
            OccludedSilhouette,

            /// <summary>대기 원근 — 없어도 형태가 읽힌다. 끌 수 있다.</summary>
            Atmosphere,
            /// <summary>동적 투사 그림자 — 없어도 형태가 읽힌다. 예산으로 줄인다.</summary>
            CastShadows,
            /// <summary>먼지·광선 VFX — 끌 수 있다.</summary>
            AmbientVfx,
        }

        /// <summary>
        /// 이 기능을 품질 단계나 옵션으로 끌 수 있는가.
        /// 가독성 필수 기능은 어떤 단계에서도 false 다(§13).
        /// </summary>
        public static bool MayDisable(Feature f)
        {
            switch (f)
            {
                case Feature.WallFront:
                case Feature.ForegroundOcclusion:
                case Feature.ContactAo:
                case Feature.OccludedSilhouette:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>이 단계에서 주변 VFX(먼지·광선)를 켜는가.</summary>
        public static bool AmbientVfxEnabled(VisualQualityTier tier)
            => tier >= VisualQualityTier.High;

        /// <summary>전투 가독성은 유지하고, 저사양에서는 착탄 파편 수만 줄인다.</summary>
        public static float CombatParticleScale(VisualQualityTier tier)
        {
            switch (tier)
            {
                case VisualQualityTier.Low: return .5f;
                case VisualQualityTier.Medium: return .75f;
                default: return 1f;
            }
        }

        /// <summary>화면에 동시에 그릴 아군 투사체 상한. 판정은 제한하지 않고 표현만 제한한다.</summary>
        public static int ProjectileVisualBudget(VisualQualityTier tier)
            => tier == VisualQualityTier.Low ? 64 : tier == VisualQualityTier.Medium ? 80 : 96;

        /// <summary>
        /// 궤적은 투사체 본체보다 버텍스 비용이 크다. 최근 탄부터 이 수만큼만 남기고,
        /// 나머지는 셰이더 내부의 짧은 속도 꼬리로 방향을 읽게 한다.
        /// </summary>
        public static int ProjectileTrailBudget(VisualQualityTier tier)
            => tier == VisualQualityTier.Low ? 20 : tier == VisualQualityTier.Medium ? 40
             : tier == VisualQualityTier.High ? 64 : 80;

        /// <summary>오래 남는 연기 리본 예산. 탄체보다 낮게 잡아 연사 중에도 오버드로우를 제한한다.</summary>
        public static int ProjectileSmokeTrailBudget(VisualQualityTier tier)
            => tier == VisualQualityTier.Low ? 10 : tier == VisualQualityTier.Medium ? 24
             : tier == VisualQualityTier.High ? 40 : 56;

        /// <summary>
        /// 실제 Light2D 는 가장 강한 탄에만 배정한다. 모든 탄은 언릿 HDR 외곽광이 있으므로
        /// 저사양에서 조명을 꺼도 탄 자체의 판독성은 사라지지 않는다.
        /// </summary>
        public static int ProjectileLightBudget(VisualQualityTier tier)
            => tier == VisualQualityTier.Low ? 0 : tier == VisualQualityTier.Medium ? 4
             : tier == VisualQualityTier.High ? 8 : 12;
    }
}
