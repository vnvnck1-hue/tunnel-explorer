using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    /// <summary>떨어진 재화 한 알. 원본 <c>G.res[]</c> 의 원소.</summary>
    public sealed class LootItem
    {
        public Vec2 Position;
        public Vec2 Velocity;
        /// <summary>가짜 높이. 렌더가 스프라이트를 위로 띄우는 데만 쓴다.</summary>
        public double Z;
        public double VZ;
        public bool Landed;
        public double LandAge;
        public int Bounces;
        public ResourceKind Kind;
        public int Value;
        public double Age;
        public bool Collected;
    }

    /// <summary>
    /// 원본의 자원 물리(7281~7315행)와 <c>spawnLootBurst()</c>.
    /// z축 포물선 → 최대 2회 바운스 → 착지 0.8초 뒤 자석 → 근접 시 획득.
    ///
    /// 감쇠(0.92 / 0.85)는 원본이 프레임당 곱이었다. 고정 60Hz 틱이라 원본이 60fps 로
    /// 돌 때와 같은 값이 되고, 저사양에서 느려지던 편차만 사라진다.
    /// </summary>
    public sealed class LootSystem
    {
        public readonly List<LootItem> Items = new List<LootItem>();

        /// <summary>수집한 재화. 원본 G.gPulp / G.gBloom.</summary>
        public int Pulp { get; private set; }
        public int Bloom { get; private set; }
        /// <summary>반경 안 전리품에 중심 방향 속도를 더한다 — 붕괴 소용돌이 (원본 12565행, 75px/s).</summary>
        public void Pull(Vec2 center, double radius, double impulse)
        {
            foreach (var q in Items)
            {
                if (q.Collected) continue;
                var d = center - q.Position; double dist = Math.Max(1e-6, d.Length);
                if (dist < radius) q.Velocity += d / dist * impulse;
            }
        }

        /// <summary>자석·습득 반경 배율 — 특성(자원 흡입기). TunnelSim 이 Build 에서 넣는다.</summary>
        public double MagnetMul = 1.0, PickupMul = 1.0;
        /// <summary>운반 전리품 코어 — 보스 처치·희귀 광물. 생환해야 보관된다 (§6.5).</summary>
        public int Core { get; set; }
        /// <summary>획득한 자원 개수 합 (원본 G.nRes).</summary>
        public int TotalCollected { get; private set; }

        /// <summary>심층 배율. 원본 depthMul() = 1 + 0.25*(depth-1).</summary>
        public double DepthMul { get; set; } = 1.0;

        public event Action<ResourceCollectedEvent> Collected;

        readonly Random _fx;

        /// <summary>연출용 난수. 맵 생성 시드와 분리한다(원본도 여기는 Math.random 이었다).</summary>
        public LootSystem(int fxSeed = 12345) => _fx = new Random(fxSeed);

        /// <summary>원본 spawnLootBurst(). 타일이 부서질 때 호출한다.</summary>
        public void SpawnBurst(Vec2 at, ResourceKind kind, int value, int count, Vec2 hitDir)
        {
            double baseAngle = hitDir.SqrLength > 1e-9 ? hitDir.Angle : _fx.NextDouble() * Math.PI * 2;
            double spread = SimTuning.LootSpread;

            for (int i = 0; i < count; i++)
            {
                double a = baseAngle + (_fx.NextDouble() - .5) * spread * 2
                         + (i > 0 ? (i / (double)Math.Max(1, count)) * spread * .4 : 0);
                double sp = SimTuning.LootScatter + _fx.NextDouble() * SimTuning.LootScatterRand;

                Items.Add(new LootItem
                {
                    Position = new Vec2(at.X + (_fx.NextDouble() - .5) * 0.12,
                                        at.Y + (_fx.NextDouble() - .5) * 0.12),
                    Velocity = Vec2.FromAngle(a) * sp,
                    Z = SimTuning.LootPopZ * (.7 + _fx.NextDouble() * .5),
                    VZ = SimTuning.LootPopVz * (.75 + _fx.NextDouble() * .45),
                    Kind = kind,
                    Value = value,
                });
            }

            if (Items.Count > SimTuning.ResourceMax)
                Items.RemoveRange(0, Items.Count - SimTuning.ResourceMax);
        }

        public void Tick(WorldGrid world, Vec2 playerPos, double dt)
        {
            const double itemRadius = 0.16;   // 원본 collide(q, 8px)

            for (int i = 0; i < Items.Count; i++)
            {
                var q = Items[i];
                q.Age += dt;

                if (!q.Landed)
                {
                    q.VZ -= SimTuning.LootGravity * dt;
                    q.Z += q.VZ * dt;
                    q.Position += q.Velocity * dt;
                    q.Velocity *= 0.92;
                    CollisionSystem.Resolve(world, ref q.Position, itemRadius);

                    if (q.Z <= 0)
                    {
                        q.Z = 0;
                        // 원본: |vz| > 55px/s 이고 2회 미만이면 튄다
                        if (Math.Abs(q.VZ) > SimTuning.PxCells(55.0) && q.Bounces < 2)
                        {
                            q.Bounces++;
                            q.VZ = -q.VZ * 0.36;
                            q.Velocity *= 0.62;
                        }
                        else
                        {
                            q.VZ = 0;
                            q.Velocity *= 0.2;
                            q.Landed = true;
                            q.LandAge = 0;
                        }
                    }
                    continue;
                }

                q.LandAge += dt;
                q.Velocity *= 0.85;
                if (q.Velocity.Length > SimTuning.PxCells(4.0))
                {
                    q.Position += q.Velocity * dt;
                    CollisionSystem.Resolve(world, ref q.Position, itemRadius);
                }
                else q.Velocity = Vec2.Zero;

                double d = Vec2.Distance(q.Position, playerPos);

                if (q.LandAge >= SimTuning.LootMagnetDelay && d < SimTuning.LootMagnet * MagnetMul)
                {
                    double t = Math.Min(1.0, dt * SimTuning.LootMagnetSpeed);
                    q.Position += (playerPos - q.Position) * t;
                }

                if (d < SimTuning.LootPickup * PickupMul)
                {
                    q.Collected = true;
                    int v = Math.Max(1, JsMath.Round(q.Value * DepthMul));
                    if (q.Kind == ResourceKind.Pulp) Pulp += v; else Bloom += v;
                    TotalCollected += v;
                    Collected?.Invoke(new ResourceCollectedEvent
                    {
                        Kind = q.Kind, Amount = v, Position = q.Position,
                    });
                }
            }

            Items.RemoveAll(q => q.Collected);
        }

        public void Clear()
        {
            Items.Clear();
            Pulp = 0; Bloom = 0; TotalCollected = 0;
        }
    }
}
