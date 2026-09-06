using System;
using UnityEngine;

namespace TunnelCrew.Data
{
    /// <summary>
    /// 몬스터 3종의 16프레임 스트립. 원본 <c>ENEMY_SPRITES</c>(2099~2107) 와
    /// <c>enemySpriteFrame()</c>(2144~2154) 규칙:
    ///   walk 0~5 · idle 6~7 · blink 8~11 · sprint 12~15 (광란종 질주).
    /// 단일 방향이며 좌우 반전도 하지 않는다 (보스만 facing 반전).
    /// </summary>
    [CreateAssetMenu(menuName = "Tunnel Crew/Monster Sheets", fileName = "MonsterSheets")]
    public sealed class MonsterSheetAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Kind
        {
            public string id;          // crawler / spitter / broodBeast
            public Sprite[] frames;    // 16장
        }

        public Kind[] kinds = Array.Empty<Kind>();

        public Sprite[] FramesFor(string id)
        {
            foreach (var k in kinds) if (k.id == id) return k.frames;
            return null;
        }
    }
}
