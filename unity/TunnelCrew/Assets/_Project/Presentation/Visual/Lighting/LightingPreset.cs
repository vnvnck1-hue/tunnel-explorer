using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 랩에서 쓰는 그림자 방식.
    ///
    /// <b>왜 ShadowCaster2D 가 없는가</b> — URP 17.3 은 <c>ShadowCaster2D.shadowCastingSource</c> 와
    /// 형태 주입 API 가 전부 internal 이라 리플렉션이 필요하다. <c>docs/urp-2d-lighting/09</c> §D 가
    /// "리플렉션을 더 늘리지 말고 freeform 라이트/블롭처럼 public API 로 되는 쪽을 우선한다" 고
    /// 못 박았고, 같은 문서 §B-2·B-3 이 네거티브 라이팅과 블롭 섀도우를 우리 격차로 지목했다.
    /// 그래서 랩은 <see cref="LabShadowBlob"/>(Sprite 타입 Light2D + Multiply) 만 쓴다 —
    /// 전부 public API 이고, 본선의 <c>WallShadowBuilder</c> 경로와 독립이다.
    /// </summary>
    public enum LabShadowMode
    {
        /// <summary>그림자 없음. 광원과 어둠만으로 형태가 읽히는지 보는 기준.</summary>
        None = 0,

        /// <summary>접촉 블롭만 — 프롭 발밑의 어두운 얼룩. 물체가 바닥에 붙어 보이게 한다.</summary>
        Blob = 1,

        /// <summary>접촉 블롭 + 네거티브 라이팅 — 아티스트가 지정한 어두운 영역까지 켠다.</summary>
        BlobAndNegative = 2,
    }

    /// <summary>
    /// 라이팅 프리셋 — 플레이 중에 버튼 하나로 갈아 끼우는 조명 세팅 한 벌.
    ///
    /// 캡처를 여러 장 찍어 비교하는 대신 런타임에 즉시 전환하기 위한 데이터다. 수치를 이해하고
    /// 고르는 게 아니라 <b>보이는 결과를 고른 뒤 수치를 읽는다</b> — 그래서 값은 전부 여기 모여
    /// 있고, <see cref="LightingPresetSwitcher"/> 가 확정된 프리셋을 파일로 남긴다.
    ///
    /// <b>퍼플 톤은 축이 아니다.</b> <see cref="ambientColor"/> 기본값은 레퍼런스 캘리브레이션
    /// 씬과 같은 라벤더이고, 프리셋 사이에서 바꾸지 않는 것을 규약으로 한다. 안개·틴트·비네트도
    /// 색은 <see cref="AtmosphereProfile"/> 값을 그대로 쓰고 <see cref="postScale"/> 로 세기만 민다.
    /// </summary>
    [CreateAssetMenu(menuName = "Tunnel Crew/Lighting Preset", fileName = "LightingPreset")]
    public sealed class LightingPreset : ScriptableObject
    {
        [Tooltip("버튼에 표시되는 이름.")]
        public string label = "프리셋";

        [Tooltip("이 프리셋이 무엇을 보려는 것인지. UI 에 함께 표시된다.")]
        [TextArea(1, 3)] public string note = "";

        [Header("어둠 밀도 — 전역광")]
        [Tooltip("앰비언트 색. 프리셋 사이에서 바꾸지 않는다(확정된 컬러감).")]
        public Color ambientColor = new Color(0.62f, 0.50f, 0.76f, 1f);

        [Tooltip("전역광 세기. 0.62 = 레퍼런스 캘리브레이션 씬, 0.35 = 본편 지층1, 0.16 = 짙음.")]
        [Range(0f, 2f)] public float ambientIntensity = 0.35f;

        [Header("광원")]
        [Tooltip("작업등·탐색광·광물광의 세기 배율. 어둠을 낮출 때 광원을 같이 올려 초점을 유지한다.")]
        [Range(0f, 3f)] public float lightScale = 1f;

        [Header("그림자")]
        public LabShadowMode shadowMode = LabShadowMode.Blob;

        [Tooltip("접촉 블롭의 진하기.")]
        [Range(0f, 1f)] public float blobStrength = 0.55f;

        [Tooltip("네거티브 라이팅(지정 암부)의 진하기. shadowMode 가 BlobAndNegative 일 때만 쓴다.")]
        [Range(0f, 1f)] public float negativeStrength = 0.45f;

        [Header("후처리 — 대기 프로파일 배율")]
        [Tooltip("안개 농도·깊이 색분리·비네트·그레인에 곱한다. 색은 곱하지 않는다. " +
                 "1 = 현재 AtmosphereProfile_Stratum1(사실상 꺼짐), 5 = Katana ZERO 급.")]
        [Range(0f, 8f)] public float postScale = 1f;

        [Header("팔레트 — 지층별 (Rain World 식)")]
        [Tooltip("이 프리셋의 대기 프로파일. 비면 스위처 기본(Stratum1). 지층마다 근/원 틴트·안개색이 다른 " +
                 "프로파일을 두면 광원을 늘리지 않고도 '다른 세계'가 된다.")]
        public AtmosphereProfile atmosphereProfile;

        [Tooltip("이 프리셋의 URP Volume 프로파일(색보정·톤매핑·블룸·비네트·그레인). 비면 스위처 기본 " +
                 "(Volume_Stratum1_Surface). 본편 RunBootstrap.BuildVolume 이 쓰는 것과 같은 자산이다.")]
        public UnityEngine.Rendering.VolumeProfile volumeProfile;

        [Header("블룸 — 리서치 결론 ② 의 2순위 지렛대")]
        [Tooltip("Bloom.intensity 오버라이드. 음수면 Volume 프로파일 값을 그대로 쓴다. " +
                 "본편 값: 지층1 0.60 · 지층2 0.70 · 지층3 0.80 · 이상지대 0.90.")]
        [Range(-1f, 4f)] public float bloomIntensity = -1f;

        [Tooltip("Bloom.threshold 오버라이드. 음수면 프로파일 값(지층1 0.70). 낮추면 광원 주변이 더 넓게 번진다.")]
        [Range(-1f, 2f)] public float bloomThreshold = -1f;
    }
}
