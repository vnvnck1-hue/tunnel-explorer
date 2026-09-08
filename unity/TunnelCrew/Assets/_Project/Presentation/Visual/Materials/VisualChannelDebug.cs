using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>단독으로 볼 채널(§12.3 "Albedo/Normal/Emission/AO/Sorting/Occluder 단독 보기").</summary>
    public enum ChannelView
    {
        /// <summary>전부 합성한 최종 화면.</summary>
        Composite = 0,
        Albedo = 1,
        Normal = 2,
        Emission = 3,
        MaterialMask = 4,
        AmbientOcclusion = 5,
        /// <summary>알베도를 흰색으로 바꿔 조명 분포만 남긴다.</summary>
        LightingOnly = 6,
    }

    /// <summary>
    /// <c>WorldLit</c>·<c>CharacterLit</c> 의 채널 디버그 전역값을 한곳에서 켠다/끈다.
    ///
    /// 머티리얼마다 값을 넣지 않고 전역 유니폼을 쓴다 — 씬의 모든 재질에 한 번에 걸리고,
    /// 머티리얼 사본이 생기지 않아 배칭이 깨지지 않는다.
    ///
    /// 셰이더 쪽 값은 "끄기(mute)" 의미다. 아무도 값을 넣지 않으면 0 이 되고 0 은 "그대로 쓴다"
    /// 이므로, 이 클래스를 쓰지 않는 씬에서도 정상 합성 화면이 나온다.
    /// </summary>
    public static class VisualChannelDebug
    {
        static readonly int IdIsolate = Shader.PropertyToID("_TCChannelIsolate");
        static readonly int IdMuteNormal = Shader.PropertyToID("_TCMuteNormal");
        static readonly int IdMuteEmission = Shader.PropertyToID("_TCMuteEmission");
        static readonly int IdMuteMask = Shader.PropertyToID("_TCMuteMask");
        static readonly int IdMuteAo = Shader.PropertyToID("_TCMuteAO");

        public static ChannelView View { get; private set; } = ChannelView.Composite;
        public static bool UseNormal { get; private set; } = true;
        public static bool UseEmission { get; private set; } = true;
        public static bool UseMask { get; private set; } = true;
        public static bool UseAo { get; private set; } = true;

        public static void SetView(ChannelView view)
        {
            View = view;
            Shader.SetGlobalFloat(IdIsolate, (int)view);
        }

        /// <summary>단독 보기를 순서대로 넘긴다.</summary>
        public static ChannelView NextView(int step = 1)
        {
            int n = System.Enum.GetValues(typeof(ChannelView)).Length;
            var next = (ChannelView)((((int)View + step) % n + n) % n);
            SetView(next);
            return next;
        }

        public static void SetChannels(bool normal, bool emission, bool mask, bool ao)
        {
            UseNormal = normal; UseEmission = emission; UseMask = mask; UseAo = ao;
            Shader.SetGlobalFloat(IdMuteNormal, normal ? 0f : 1f);
            Shader.SetGlobalFloat(IdMuteEmission, emission ? 0f : 1f);
            Shader.SetGlobalFloat(IdMuteMask, mask ? 0f : 1f);
            Shader.SetGlobalFloat(IdMuteAo, ao ? 0f : 1f);
        }

        public static void ToggleNormal() => SetChannels(!UseNormal, UseEmission, UseMask, UseAo);
        public static void ToggleEmission() => SetChannels(UseNormal, !UseEmission, UseMask, UseAo);
        public static void ToggleMask() => SetChannels(UseNormal, UseEmission, !UseMask, UseAo);
        public static void ToggleAo() => SetChannels(UseNormal, UseEmission, UseMask, !UseAo);

        /// <summary>합성 화면으로 되돌린다. 캡처 세트가 각 장면 사이에 부른다.</summary>
        public static void Reset()
        {
            SetView(ChannelView.Composite);
            SetChannels(true, true, true, true);
        }

        public static string Label(ChannelView v) => v switch
        {
            ChannelView.Albedo => "Albedo 단독 (조명 없음)",
            ChannelView.Normal => "Normal 단독",
            ChannelView.Emission => "Emission 단독",
            ChannelView.MaterialMask => "Material Mask 단독 (R 금속 · G 광택 · B 습윤)",
            ChannelView.AmbientOcclusion => "AO 단독",
            ChannelView.LightingOnly => "조명만 (알베도 흰색)",
            _ => "합성",
        };
    }
}
