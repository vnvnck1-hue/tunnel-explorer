using UnityEngine;

namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>
    /// 로드맵 4.8 — 연속 사격용 오브젝트 풀.
    /// 탄피·파편·버스트를 고정 용량 링 버퍼로 돌려 쓴다. 전부 사용 중이면 가장 오래된 것을
    /// 회수하므로 초당 수십 발을 쏴도 새 할당과 GC 가 생기지 않는다.
    /// </summary>
    public sealed class ModularGunnerFxPool : MonoBehaviour
    {
        const int DebrisCapacity = 320;
        const int BurstCapacity = 96;

        public static ModularGunnerFxPool Instance { get; private set; }

        ModularGunnerDebris[] _debris;
        ModularGunnerFxBurst[] _bursts;
        int _debrisNext;
        int _burstNext;

        void Awake()
        {
            Instance = this;

            var debrisRoot = new GameObject("Debris Pool").transform;
            debrisRoot.SetParent(transform, false);
            _debris = new ModularGunnerDebris[DebrisCapacity];
            for (int i = 0; i < DebrisCapacity; i++)
            {
                var go = new GameObject("Debris");
                go.transform.SetParent(debrisRoot, false);
                _debris[i] = go.AddComponent<ModularGunnerDebris>();
                _debris[i].PoolAwake();
            }

            var burstRoot = new GameObject("Burst Pool").transform;
            burstRoot.SetParent(transform, false);
            _bursts = new ModularGunnerFxBurst[BurstCapacity];
            for (int i = 0; i < BurstCapacity; i++)
            {
                var go = new GameObject("Burst");
                go.transform.SetParent(burstRoot, false);
                _bursts[i] = go.AddComponent<ModularGunnerFxBurst>();
                _bursts[i].PoolAwake();
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public ModularGunnerDebris RentDebris()
        {
            ModularGunnerDebris item = _debris[_debrisNext];
            _debrisNext = (_debrisNext + 1) % _debris.Length;
            return item;
        }

        public ModularGunnerFxBurst RentBurst()
        {
            ModularGunnerFxBurst item = _bursts[_burstNext];
            _burstNext = (_burstNext + 1) % _bursts.Length;
            return item;
        }
    }
}
