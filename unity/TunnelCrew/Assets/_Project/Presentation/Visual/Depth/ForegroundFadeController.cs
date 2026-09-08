using System.Collections.Generic;
using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 기능명세서 §6.6 — 전경 오클루전과 플레이어 가시성 보정.
    ///
    /// 남쪽 벽·천장 립·기둥이 캐릭터 앞에 그려져야 공간이 생기지만, 그 때문에 플레이어가
    /// 보이지 않아서는 안 된다. 겹치면 목표 알파로 내렸다가 벗어나면 복원한다.
    ///
    /// 페이드는 <b>시각 표현만</b> 바꾼다 — 전투 판정과 네트워크 상태에는 손대지 않는다(§15.2).
    /// 그룹 단위로 합쳐 하나의 벽 덩어리가 조각조각 사라지지 않게 한다.
    /// </summary>
    [DefaultExecutionOrder(110)]   // FootpointSorter(100) 다음 — 같은 프레임의 발 위치를 쓴다
    public sealed class ForegroundFadeController : MonoBehaviour
    {
        static readonly List<ForegroundOccluder> Occluders = new List<ForegroundOccluder>(128);

        [SerializeField] WorldVisualProfile _profile;

        readonly List<(Vector2 ground, float radius)> _interest = new List<(Vector2, float)>(8);
        readonly Dictionary<int, bool> _groupHit = new Dictionary<int, bool>(32);
        readonly Dictionary<int, float> _groupAlpha = new Dictionary<int, float>(32);

        public WorldVisualProfile Profile { get => _profile; set => _profile = value; }

        /// <summary>접근성 옵션 — 끄면 전경이 늘 불투명하다(§10.3, §14 F).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 전경 투명도 옵션(§10.3). 0 이면 프로파일 값을 쓴다.
        ///
        /// <b>더 잘 보이게 하는 방향만 허용한다.</b> 값이 프로파일보다 낮을 때만 적용해
        /// 전경을 더 투명하게 만든다 — 더 불투명하게 고정하는 것은 §13 이 전경 가림을
        /// 가독성 필수로 둔 취지와 어긋난다.
        /// </summary>
        public static float AlphaOverride { get; set; }

        public static void Register(ForegroundOccluder o)
        {
            if (o != null && !Occluders.Contains(o)) Occluders.Add(o);
        }

        public static void Unregister(ForegroundOccluder o)
        {
            if (o != null) Occluders.Remove(o);
        }

        /// <summary>
        /// 이 지점이 전경 오클루더에 덮여 있는가. 덮고 있는 것들 중 <b>가장 낮은 현재 알파</b>도
        /// 함께 준다 — 실루엣 패스(§6.6)가 "얼마나 가려졌는가" 를 알아야 하기 때문이다.
        ///
        /// 관심 캐릭터는 벽이 이미 페이드해서 알파가 낮고, 적은 페이드가 일어나지 않아
        /// 알파가 1 이다. 두 경우 모두 "덮였다" 는 참이지만 보정 방식이 다르다.
        /// </summary>
        public static bool CoveredAt(Vector2 ground, float radius, out float lowestAlpha)
        {
            lowestAlpha = 1f;
            bool covered = false;
            for (int i = 0; i < Occluders.Count; i++)
            {
                var o = Occluders[i];
                if (o == null || !o.Overlaps(ground, radius)) continue;
                covered = true;
                if (o.Alpha < lowestAlpha) lowestAlpha = o.Alpha;
            }
            return covered;
        }

        void LateUpdate() => Step(Time.unscaledDeltaTime);

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 캡처용 한 스텝. 시간이 흐르지 않으므로 페이드를 목표값까지 즉시 보낸다 —
        /// 캡처는 "겹쳤을 때 어떻게 보이는가" 를 봐야 하고, 중간 보간 프레임이 아니다.
        /// </summary>
        public void EditorTick() => Step(10f);
#endif

        void Step(float dt)
        {
            float targetAlpha = _profile != null ? _profile.foregroundFadeAlpha : 0.34f;
            if (AlphaOverride > 0f && AlphaOverride < targetAlpha) targetAlpha = AlphaOverride;
            float outTime = _profile != null ? _profile.foregroundFadeOut : 0.22f;
            float inTime = _profile != null ? _profile.foregroundFadeIn : 0.32f;
            float extra = _profile != null ? _profile.foregroundFadeRadius : 0.9f;

            FootpointSorter.CollectInterest(_interest);

            // 1패스: 그룹별로 "관심 캐릭터와 겹쳤는가" 를 합친다.
            _groupHit.Clear();
            for (int i = Occluders.Count - 1; i >= 0; i--)
            {
                var o = Occluders[i];
                if (o == null) { Occluders.RemoveAt(i); continue; }

                bool hit = false;
                if (Enabled)
                    for (int j = 0; j < _interest.Count && !hit; j++)
                        hit = o.Overlaps(_interest[j].ground, _interest[j].radius + extra);

                _groupHit[o.fadeGroup] = _groupHit.TryGetValue(o.fadeGroup, out bool prev) ? prev || hit : hit;
            }

            // 2패스: 그룹 알파를 시간 기반으로 보간한다. 프레임률과 무관해야 한다.
            // 1 → targetAlpha 전체 구간을 outTime/inTime 안에 지나가는 속도.
            // 남은 거리에 비례한 Lerp 를 쓰면 §6.6 이 지정한 시간이 지켜지지 않는다.
            float span = Mathf.Max(0.001f, 1f - targetAlpha);
            foreach (var kv in _groupHit)
            {
                float cur = _groupAlpha.TryGetValue(kv.Key, out float a) ? a : 1f;
                float goal = kv.Value ? targetAlpha : 1f;
                float time = kv.Value ? outTime : inTime;
                float step = time > 0.0001f ? span * dt / time : 1f;
                _groupAlpha[kv.Key] = Mathf.MoveTowards(cur, goal, step);
            }

            // 3패스: 적용.
            for (int i = 0; i < Occluders.Count; i++)
            {
                var o = Occluders[i];
                if (o == null) continue;
                float a = _groupAlpha.TryGetValue(o.fadeGroup, out float g) ? g : 1f;
                // 오클루더가 자기 목표 알파를 따로 선언했으면 그 하한을 지킨다(아트별 설정).
                if (o.fadeTargetAlpha > 0f && a < o.fadeTargetAlpha) a = o.fadeTargetAlpha;
                o.ApplyAlpha(a);
            }
        }

        /// <summary>Visual Lab 과 회귀 캡처가 상태를 초기화할 때 쓴다.</summary>
        public void ResetFade()
        {
            _groupAlpha.Clear();
            for (int i = 0; i < Occluders.Count; i++) Occluders[i]?.ApplyAlpha(1f);
        }
    }
}
