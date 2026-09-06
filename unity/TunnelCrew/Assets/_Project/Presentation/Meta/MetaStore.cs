using System;
using System.IO;
using TunnelCrew.Sim;
using UnityEngine;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 영구 저장 — 원본 <c>infSaveMeta / INF_META</c> (localStorage). 유니티에서는 <c>persistentDataPath/tc_infinite_meta_v1.json</c>.
    /// 구매는 저장이 성공해야 확정된다(§6.4.5): <see cref="TryBuyNode"/> 가 실패하면 되돌린다.
    /// </summary>
    public static class MetaStore
    {
        static MetaState _cached;
        public static string Path => System.IO.Path.Combine(Application.persistentDataPath, MetaState.Key + ".json");

        public static MetaState Load()
        {
            if (_cached != null) return _cached;
            try
            {
                if (File.Exists(Path)) _cached = JsonUtility.FromJson<MetaState>(File.ReadAllText(Path));
            }
            catch (Exception e) { Debug.LogWarning($"[meta] 불러오기 실패 — 새로 시작: {e.Message}"); }
            _cached ??= new MetaState();
            _cached.Sanitize();
            return _cached;
        }

        public static bool Save(MetaState m = null)
        {
            m ??= _cached; if (m == null) return false;
            try
            {
                var dir = System.IO.Path.GetDirectoryName(Path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                // 원자적 저장 — 임시 파일에 쓰고 교체
                var tmp = Path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(m, prettyPrint: true));
                if (File.Exists(Path)) File.Replace(tmp, Path, null); else File.Move(tmp, Path);
                return true;
            }
            catch (Exception e) { Debug.LogError($"[meta] 저장 실패: {e.Message}"); return false; }
        }

        /// <summary>노드 구매 — 차감·랭크 후 저장. 저장 실패면 롤백 (원본 infBuyPermanentNode).</summary>
        public static bool TryBuyNode(NodeDef node)
        {
            var m = Load();
            int rank = m.RankOf(node.Id), bank = m.bankedCores;
            if (!PermanentNodes.Buy(node, m)) return false;
            if (!Save(m)) { m.bankedCores = bank; m.SetRank(node.Id, rank); return false; }
            return true;
        }

        /// <summary>테스트·디버그용 — 캐시를 버리고 다시 읽는다.</summary>
        public static void Reload() { _cached = null; }
        public static void ResetForTests() { _cached = new MetaState(); }
    }
}
