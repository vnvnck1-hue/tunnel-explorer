using System;
using System.Collections.Generic;

namespace TunnelCrew.Sim
{
    /// <summary>
    /// 영구 저장 상태 — 원본 <c>INF_META</c> (<c>tc_infinite_meta_v1</c>, 11616). 계정 단위 기록·보관 코어·
    /// 해금·영구 노드 랭크·유물. 직렬화는 Presentation 의 <c>MetaStore</c>(JsonUtility) 가 맡고, 이 클래스는
    /// 규칙만 가진다 (Sim 어셈블리는 엔진을 참조하지 않는다).
    ///
    /// 저장 정책(§8.2): 보스 코어는 **생환해야만** bankedCores 에 들어간다. 다운 시에는 영구 노드의
    /// keepRate/keepMin/remoteSent 만큼만 보존된다.
    /// </summary>
    [Serializable]
    public sealed class MetaState
    {
        public const string Key = "tc_infinite_meta_v1";
        public int version = 1;

        public int bestDepth;
        public int bestBlocks;
        public int totalBosses;
        public int escapes;
        public int bankedCores;

        // 해금 플래그 (원본 unlocks{ricochet,explosive,breach})
        public bool unlockRicochet, unlockExplosive, unlockBreach;

        // 영구 노드 랭크 — 평면 구조. JsonUtility 가 Dictionary 를 못 다뤄 병렬 배열로 둔다.
        public List<string> rankIds = new List<string>();
        public List<int> rankValues = new List<int>();

        // 유물 (원본 relics{owned,sockets[5],age[5],seq})
        public List<string> relicOwned = new List<string>();
        public string[] relicSockets = new string[5];
        public int[] relicAge = new int[5];
        public int relicSeq;

        public int RankOf(string nodeId)
        {
            int i = rankIds.IndexOf(nodeId);
            return i < 0 ? 0 : rankValues[i];
        }

        public void SetRank(string nodeId, int rank)
        {
            int i = rankIds.IndexOf(nodeId);
            if (rank <= 0) { if (i >= 0) { rankIds.RemoveAt(i); rankValues.RemoveAt(i); } return; }
            if (i < 0) { rankIds.Add(nodeId); rankValues.Add(rank); } else rankValues[i] = rank;
        }

        /// <summary>원본 infInitPermanentMetaOnly 3단계 — 모르는 ID·초과 랭크를 정리한다 (레거시 마이그레이션은 없다: 유니티 세이브는 v1 부터).</summary>
        public void Sanitize()
        {
            for (int i = rankIds.Count - 1; i >= 0; i--)
            {
                var node = PermanentNodes.ById(rankIds[i]);
                if (node == null || rankValues[i] <= 0) { rankIds.RemoveAt(i); rankValues.RemoveAt(i); continue; }
                rankValues[i] = Math.Min(node.MaxRank, rankValues[i]);
            }
            if (relicSockets == null || relicSockets.Length != 5) relicSockets = new string[5];
            if (relicAge == null || relicAge.Length != 5) relicAge = new int[5];
            relicOwned.RemoveAll(id => Relics.ById(id) == null);
            for (int i = relicOwned.Count - 1; i >= 0; i--) if (relicOwned.IndexOf(relicOwned[i]) != i) relicOwned.RemoveAt(i);
            var seen = new HashSet<string>();
            for (int i = 0; i < 5; i++)
            {
                var id = relicSockets[i];
                if (string.IsNullOrEmpty(id)) { relicSockets[i] = null; continue; }
                if (Relics.ById(id) == null || !relicOwned.Contains(id) || !seen.Add(id)) relicSockets[i] = null;
            }
            // 왕관이 앞 4칸에 없으면 5번 소켓은 잠긴다
            bool crown = false; for (int i = 0; i < 4; i++) if (relicSockets[i] == Relics.Crown) crown = true;
            if (!crown) relicSockets[4] = null;
        }

        /// <summary>원본 infEndRun 의 기록·해금 갱신. 코어 정산은 <see cref="Settle"/>.</summary>
        public List<string> RecordRun(int depth, int totalBlocks, int bosses)
        {
            var newly = new List<string>();
            bestDepth = Math.Max(bestDepth, depth);
            bestBlocks = Math.Max(bestBlocks, totalBlocks);
            totalBosses += bosses;
            if (bestDepth >= 2 && !unlockRicochet) { unlockRicochet = true; newly.Add("도탄 탄두"); }
            if (totalBosses >= 3 && !unlockExplosive) { unlockExplosive = true; newly.Add("발파 탄두"); }
            if (bestDepth >= 3 && !unlockBreach) { unlockBreach = true; newly.Add("돌파 규격"); }
            return newly;
        }

        /// <summary>정산 결과 — 원본 infEndRun 14911~14917.</summary>
        public struct Settlement { public int Returned, Kept, Lost, Remote; public bool Escaped; }

        /// <summary>
        /// 코어 정산. 생환이면 전부 보관, 아니면 keepRate·keepMin·remoteSent(원격 전송) 중 큰 쪽만 보존.
        /// 유물(밀수꾼 주머니 +25%p)은 호출자가 relicKeepRate 로 넘긴다.
        /// </summary>
        public Settlement Settle(int core, bool escaped, PermState perm, double relicKeepRate = 0)
        {
            int remoteSent = Math.Min(core, perm?.RemoteSent ?? 0);
            double keepRate = Math.Min(.95, (perm?.KeepRate ?? 0) + relicKeepRate);
            int keepBase = keepRate > 0 ? Math.Max((int)Math.Floor(core * keepRate), Math.Min((int)(perm?.KeepMin ?? 0), core)) : 0;
            int kept = escaped ? 0 : Math.Min(core, Math.Max(keepBase, remoteSent));
            int lost = escaped ? 0 : Math.Max(0, core - kept);
            if (escaped) { escapes++; bankedCores += core; }
            else if (kept > 0) bankedCores += kept;
            return new Settlement { Returned = escaped ? core : 0, Kept = kept, Lost = lost, Remote = remoteSent, Escaped = escaped };
        }
    }
}
