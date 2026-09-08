using System.Collections.Generic;
using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 기능명세서 §6.5 — 등록된 <see cref="VisualHeightAnchor"/> 를 한 패스에서 정렬한다.
    ///
    /// 앵커마다 LateUpdate 를 돌리지 않는 이유는 두 가지다. 첫째, 정렬은 순서가 정해진
    /// 한 번의 작업이어야 프레임 안에서 앞뒤가 흔들리지 않는다. 둘째, 전경 페이드(§6.6)가
    /// 같은 프레임의 "관심 캐릭터 목록"을 그대로 이어받아야 한다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class FootpointSorter : MonoBehaviour
    {
        static readonly List<VisualHeightAnchor> Anchors = new List<VisualHeightAnchor>(256);
        static FootpointSorter _instance;

        [SerializeField] WorldVisualProfile _profile;

        /// <summary>정렬 대상 전체. 전경 페이드가 관심 캐릭터를 골라 쓴다.</summary>
        public static IReadOnlyList<VisualHeightAnchor> All => Anchors;

        public WorldVisualProfile Profile
        {
            get => _profile;
            set => _profile = value;
        }

        public static FootpointSorter Instance => _instance;

        public static void Register(VisualHeightAnchor a)
        {
            if (a != null && !Anchors.Contains(a)) Anchors.Add(a);
        }

        public static void Unregister(VisualHeightAnchor a)
        {
            if (a != null) Anchors.Remove(a);
        }

        void OnEnable() => _instance = this;

        void OnDisable()
        {
            if (_instance == this) _instance = null;
        }

        void LateUpdate()
        {
            int units = _profile != null ? _profile.depthUnitsPerCell : DepthSort.DefaultUnitsPerCell;
            ApplyAll(units);
        }

        /// <summary>
        /// 등록된 앵커 전부에 정렬을 적용한다.
        ///
        /// 플레이 모드에서는 <c>LateUpdate</c> 가 부르고, 에디터 캡처(§12.3)에서는
        /// Visual Lab 이 직접 부른다. <b>일부만 부르면 안 된다</b> — 더미 하나만 적용했을 때
        /// 세트피스가 전부 <c>Default</c> 레이어에 남아 깊이층이 통째로 무너졌다.
        /// </summary>
        public static void ApplyAll(int unitsPerCell)
        {
            // 파괴된 앵커는 여기서 걸러낸다 — OnDisable 이 오지 않는 경우(씬 전환)가 있다.
            for (int i = Anchors.Count - 1; i >= 0; i--)
            {
                var a = Anchors[i];
                if (a == null) { Anchors.RemoveAt(i); continue; }
                a.Apply(unitsPerCell);
            }
        }

        /// <summary>
        /// 로컬 관심 캐릭터의 발 위치를 모은다(§6.6 "모든 로컬 관심 캐릭터를 기준으로
        /// 페이드 영역을 합친다"). 협동에서는 여러 개가 나온다.
        /// </summary>
        public static void CollectInterest(List<(Vector2 ground, float radius)> into)
        {
            into.Clear();
            for (int i = 0; i < Anchors.Count; i++)
            {
                var a = Anchors[i];
                if (a == null || !a.isLocalInterest) continue;
                into.Add((a.groundPosition, a.footprintRadius));
            }
        }
    }
}
