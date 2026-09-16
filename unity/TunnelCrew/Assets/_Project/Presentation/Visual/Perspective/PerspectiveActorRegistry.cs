using System.Collections.Generic;
using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>3D 월드에서의 렌더 대역. 같은 대역 안에서는 <see cref="PerspectiveActorSample.DepthBias"/> 로만 앞뒤를 나눈다.</summary>
    public enum PerspectiveActorGroup
    {
        /// <summary>바닥에 눕는 표시(그림자·텔레그래프·설치 바닥판).</summary>
        Ground = 0,
        /// <summary>서 있는 액터(플레이어·크루·적·보스).</summary>
        Actor = 1,
        /// <summary>액터보다 앞에 두는 전투 연출(투사체·폭발·파편).</summary>
        Fx = 2,
        /// <summary>머리 위 정보(라벨·HP바). 5단계에서 스크린 오버레이로 다시 투영한다.</summary>
        Overhead = 3,
    }

    /// <summary>
    /// 액터 뷰가 3D 원근 렌더러에 넘기는 한 프레임짜리 표현 계약(3d-perspective-production-plan §4 1단계).
    ///
    /// 좌표는 항상 <b>시뮬레이션 XY</b> 다. 렌더 좌표(IsometricProjection)를 역변환해 채우지 않는다.
    /// 높이는 바닥에서 띄운 월드 유닛이며 점프·바운스·리프트는 전부 여기로만 표현한다.
    /// </summary>
    public struct PerspectiveActorSample
    {
        /// <summary>시뮬레이션 바닥 좌표(셀 단위).</summary>
        public Vector2 Ground;
        /// <summary>바닥에서 띄운 높이. 점프·바운스·스쿼시 오프셋이 들어간다.</summary>
        public float Height;
        public Sprite Sprite;
        public Color Tint;
        /// <summary>스프라이트 배율. 반전은 <see cref="FlipX"/>/<see cref="FlipY"/> 로 따로 준다.</summary>
        public Vector2 Scale;
        /// <summary>카메라 평면 안에서의 회전(도).</summary>
        public float Roll;
        public bool FlipX;
        public bool FlipY;
        /// <summary>true 면 스프라이트 아랫변이 <see cref="Height"/> 에 닿도록 피벗을 보정한다(발 피벗 고정).</summary>
        public bool AlignFeet;
        public bool Visible;
        public PerspectiveActorGroup Group;
        /// <summary>같은 대역 안의 앞뒤 미세 조정. 양수가 카메라 쪽이다.</summary>
        public float DepthBias;
        /// <summary>
        /// 과도기 필드. 아직 계약으로 옮기지 않은 표시를 일반 <see cref="SpriteRenderer"/> 미러가
        /// 그리고 있으므로, 계약으로 올라온 원본은 미러에서 빼야 이중으로 보이지 않는다.
        /// 5단계에서 미러를 지우면 이 필드도 함께 없앤다.
        /// </summary>
        public SpriteRenderer Source;

        public static PerspectiveActorSample Default => new PerspectiveActorSample
        {
            Tint = Color.white,
            Scale = Vector2.one,
            AlignFeet = true,
            Visible = true,
            Group = PerspectiveActorGroup.Actor,
        };
    }

    /// <summary>
    /// 액터 뷰(생산자)와 원근 렌더러(소비자) 사이의 등록 테이블.
    ///
    /// 뷰는 <see cref="Acquire"/> 로 슬롯을 받아 매 프레임 <see cref="Submit"/> 하고, 사라질 때 <see cref="Release"/> 한다.
    /// 렌더러는 <see cref="TryRead"/> 로 <b>이번 프레임에 갱신된</b> 슬롯만 읽는다 —
    /// 제출을 멈춘 액터(비활성·풀 반납)는 자동으로 화면에서 빠지므로 소비자가 생사를 추적하지 않아도 된다.
    /// </summary>
    public static class PerspectiveActors
    {
        struct Slot
        {
            public PerspectiveActorSample Sample;
            public int Frame;
            public bool Used;
            public string Name;
        }

        static readonly List<Slot> Slots = new List<Slot>();
        static readonly Stack<int> Free = new Stack<int>();

        /// <summary>슬롯 배열 길이. 소비자는 0..SlotCount-1 을 <see cref="TryRead"/> 로 훑는다.</summary>
        public static int SlotCount => Slots.Count;

        /// <summary>슬롯 구성이 바뀔 때 증가한다. 소비자가 캐시를 다시 만들 시점을 잡는 데 쓴다.</summary>
        public static int Version { get; private set; }

        /// <summary>새 슬롯을 받는다. 반환값 0 은 무효 핸들이다.</summary>
        public static int Acquire(string debugName)
        {
            int index;
            if (Free.Count > 0)
            {
                index = Free.Pop();
                var slot = Slots[index];
                slot.Used = true;
                slot.Frame = -1;
                slot.Name = debugName;
                slot.Sample = default;
                Slots[index] = slot;
            }
            else
            {
                index = Slots.Count;
                Slots.Add(new Slot { Used = true, Frame = -1, Name = debugName });
            }
            Version++;
            return index + 1;
        }

        public static void Release(int handle)
        {
            int index = handle - 1;
            if (index < 0 || index >= Slots.Count) return;
            var slot = Slots[index];
            if (!slot.Used) return;
            slot.Used = false;
            slot.Frame = -1;
            slot.Sample = default;
            Slots[index] = slot;
            Free.Push(index);
            Version++;
        }

        public static void Submit(int handle, in PerspectiveActorSample sample)
        {
            int index = handle - 1;
            if (index < 0 || index >= Slots.Count) return;
            var slot = Slots[index];
            if (!slot.Used) return;
            slot.Sample = sample;
            slot.Frame = Time.frameCount;
            Slots[index] = slot;
        }

        /// <summary>이번 프레임에 제출된 표시 대상만 읽는다.</summary>
        public static bool TryRead(int slot, out PerspectiveActorSample sample)
        {
            sample = default;
            if (slot < 0 || slot >= Slots.Count) return false;
            var s = Slots[slot];
            if (!s.Used || s.Frame != Time.frameCount || !s.Sample.Visible || s.Sample.Sprite == null) return false;
            sample = s.Sample;
            return true;
        }

        public static string NameOf(int slot) =>
            slot >= 0 && slot < Slots.Count ? Slots[slot].Name : null;

        /// <summary>도메인 리로드를 끄고 플레이해도 이전 런의 슬롯이 남지 않게 한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Slots.Clear();
            Free.Clear();
            Version = 0;
        }
    }
}
