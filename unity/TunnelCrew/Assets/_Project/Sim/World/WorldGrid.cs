using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    /// <summary>
    /// 런타임 월드 상태. 원본의 <c>G.cell / G.hp / G.band / G.dec / G.relic</c> 을 대신한다.
    ///
    /// 좌표계: **1셀 = 1 유닛**. 셀 (c,r) 의 중심은 (c+0.5, r+0.5) 다.
    /// 원본의 <c>cxw(c) = c*CELL + CELL/2</c> 를 셀 단위로 옮긴 것이다.
    ///
    /// 타일을 바꾸는 모든 경로가 <see cref="Version"/> 을 올린다. 원본에서 각 변경 지점이
    /// <c>G.compDirty</c> 와 <c>LOS.markDirty()</c> 를 손으로 호출해야 했고 하나만 빠져도
    /// 시야·길찾기가 어긋나던 문제(analysis-01 §9.5)를 구조적으로 막는다.
    /// </summary>
    public sealed class WorldGrid
    {
        public int Cols { get; }
        public int Rows { get; }
        public int CellCount => Cols * Rows;

        readonly TileType[] _tiles;
        readonly byte[] _band;
        readonly byte[] _dec;

        /// <summary>손상된 타일만 지연 생성. 없으면 만점 체력이다 (원본 G.hp Map 과 동일).</summary>
        readonly Dictionary<int, double> _hp = new Dictionary<int, double>();

        /// <summary>벽 속에 묻힌 유물이 있는 셀.</summary>
        public HashSet<int> BuriedRelics { get; }

        /// <summary>타일이 바뀔 때마다 증가. 시야·길찾기·렌더가 이 값으로 캐시를 무효화한다.</summary>
        public int Version { get; private set; }

        /// <summary>벽 체력 배율. 지층별로 달라진다 (INF.wallHpMul).</summary>
        public double WallHpMul { get; set; } = 1.0;

        public int EntryCol { get; }
        public int EntryRow { get; }
        public int ExitCell { get; }
        public bool ExitOpen { get; private set; }

        /// <summary>이번 런에서 부순 블록 수 (원본 G.nBlk).</summary>
        public int BlocksBroken { get; private set; }

        public WorldGrid(DungeonResult gen)
        {
            Cols = gen.Cols;
            Rows = gen.Rows;
            _tiles = gen.Tiles;
            _band = gen.Band;
            _dec = gen.Dec;
            BuriedRelics = gen.Relics ?? new HashSet<int>();
            EntryCol = gen.EntryCol;
            EntryRow = gen.EntryRow;
            ExitCell = gen.Exit;
            ExitOpen = gen.ExitOpen;
        }

        // ───────────────────────────── 인덱싱
        public int Index(int c, int r) => r * Cols + c;
        public bool InBounds(int c, int r) => c >= 0 && r >= 0 && c < Cols && r < Rows;
        /// <summary>원본 `inb` — 외곽 한 줄을 제외한 내부 판정.</summary>
        public bool InInterior(int c, int r) => c >= 1 && r >= 1 && c < Cols - 1 && r < Rows - 1;

        public TileType At(int c, int r) => InBounds(c, r) ? _tiles[Index(c, r)] : TileType.Rock;
        public TileType AtIndex(int k) => _tiles[k];

        /// <summary>원본 `solid(c,r)` — 통행 불가 여부. 맵 밖은 막힌 것으로 본다.</summary>
        public bool IsSolid(int c, int r) => !InBounds(c, r) || _tiles[Index(c, r)] != TileType.Empty;

        public bool IsBedrock(int c, int r) => TileTypes.IsBedrock(At(c, r));

        public byte BandAt(int k) => _band[k];
        public byte DecAt(int k) => _dec[k];

        /// <summary>셀 중심의 월드 좌표 (셀 단위).</summary>
        public static Vec2 CellCenter(int c, int r) => new Vec2(c + 0.5, r + 0.5);

        /// <summary>월드 좌표 → 셀. 원본 `toCell` 과 같이 floor 한다.</summary>
        public static (int c, int r) ToCell(Vec2 p)
            => ((int)Math.Floor(p.X), (int)Math.Floor(p.Y));

        public Vec2 WorldSize => new Vec2(Cols, Rows);
        public Vec2 EntryPosition => CellCenter(EntryCol, EntryRow);

        // ───────────────────────────── 체력 / 파괴
        public double MaxHp(TileType t) => TileTypes.BaseHp(t) * WallHpMul;

        public double HpAt(int k)
        {
            if (_hp.TryGetValue(k, out double h)) return h;
            return MaxHp(_tiles[k]);
        }

        /// <summary>0 = 손상 없음 … 3 = 거의 파괴. 원본 `db = min(3, round((1-hp/full)*3))`.</summary>
        public int DamageStage(int k)
        {
            var t = _tiles[k];
            if (t == TileType.Empty || TileTypes.IsBedrock(t)) return 0;
            double full = MaxHp(t);
            if (full <= 0) return 0;
            double ratio = 1.0 - HpAt(k) / full;
            return Math.Min(3, JsMath.Round(ratio * 3));
        }

        /// <summary>
        /// 원본 <c>damage()</c> 의 규칙 부분. 파괴되면 true.
        /// 파티클·사운드·XP·장악도 같은 부수효과는 여기서 처리하지 않고
        /// <see cref="TileDamaged"/> / <see cref="TileBroken"/> 이벤트로 넘긴다
        /// (analysis-04 §0 의 "부수효과는 이벤트로").
        /// </summary>
        public bool Damage(int c, int r, double amount, Vec2 hitDir)
        {
            if (!IsSolid(c, r)) return false;
            int k = Index(c, r);
            var t = _tiles[k];
            if (TileTypes.IsBedrock(t)) return false;

            double h = HpAt(k) - amount;
            if (h <= 0)
            {
                _tiles[k] = TileType.Empty;
                _hp.Remove(k);
                BlocksBroken++;
                Version++;

                bool hadRelic = BuriedRelics.Remove(k);
                if (k == ExitCell) ExitOpen = true;

                TileBroken?.Invoke(new TileBrokenEvent
                {
                    Cell = k, Col = c, Row = r, Type = t,
                    HitDir = hitDir, HadBuriedRelic = hadRelic,
                    OpenedExit = k == ExitCell,
                });
                return true;
            }

            _hp[k] = h;
            TileDamaged?.Invoke(new TileDamagedEvent
            {
                Cell = k, Col = c, Row = r, Type = t,
                Progress = 1.0 - h / MaxHp(t), HitDir = hitDir,
            });
            return false;
        }

        /// <summary>
        /// 규칙을 거치지 않고 칸을 비운다. 기반암 균열(드릴러)·엔지니어 발판·보스 벽처럼
        /// 체력 감산이 아닌 경로로 타일이 사라질 때 쓴다. 파괴 이벤트는 그대로 낸다.
        /// </summary>
        public void ForceClear(int c, int r)
        {
            if (!InBounds(c, r)) return;
            int k = Index(c, r);
            var t = _tiles[k];
            if (t == TileType.Empty) return;
            _tiles[k] = TileType.Empty;
            _hp.Remove(k);
            BlocksBroken++;
            Version++;
            bool hadRelic = BuriedRelics.Remove(k);
            if (k == ExitCell) ExitOpen = true;
            TileBroken?.Invoke(new TileBrokenEvent { Cell = k, Col = c, Row = r, Type = t, HadBuriedRelic = hadRelic, OpenedExit = k == ExitCell });
        }

        /// <summary>
        /// 타일을 놓는다 — 보스 장갑·소환 벽. 파괴 이벤트가 아니라 <see cref="TileChanged"/> 로 알린다.
        /// hp 를 주면 그 체력으로 시작한다 (보스 벽 5배 경도).
        /// </summary>
        public void SetTile(int c, int r, TileType t, double? hp = null)
        {
            if (!InBounds(c, r)) return;
            int k = Index(c, r);
            _tiles[k] = t;
            _dec[k] = 0;
            if (hp.HasValue) _hp[k] = hp.Value; else _hp.Remove(k);
            Version++;
            TileChanged?.Invoke(k);
        }

        /// <summary>
        /// 규칙·집계 없이 칸을 비운다 — 보스가 몸으로 벽을 뭉개는 경우. 장악도·XP 에 잡히지 않는다
        /// (원본 bossCrushWalls 도 infOnBlockBroken 을 부르지 않았다).
        /// </summary>
        public void ClearSilent(int c, int r)
        {
            if (!InBounds(c, r)) return;
            int k = Index(c, r);
            if (_tiles[k] == TileType.Empty) return;
            _tiles[k] = TileType.Empty;
            _hp.Remove(k);
            _dec[k] = 0;
            Version++;
            TileChanged?.Invoke(k);
        }

        public event Action<TileBrokenEvent> TileBroken;
        public event Action<TileDamagedEvent> TileDamaged;
        /// <summary>파괴·손상이 아닌 경로로 타일이 바뀌었다 (셀 인덱스). 렌더·그림자가 구독한다.</summary>
        public event Action<int> TileChanged;
    }
}
