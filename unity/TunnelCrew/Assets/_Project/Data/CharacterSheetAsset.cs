using System;
using UnityEngine;

namespace TunnelCrew.Data
{
    /// <summary>
    /// 한 직업의 방향별 애니메이션 프레임.
    ///
    /// 원본은 좌측 5방향(sw, w, nw, n, s)만 그리고 se/e/ne 는 런타임에 X 반전해서 썼다.
    /// 그 규칙은 유지하되, 프레임 목록은 임포트 시점에 굳혀 런타임 파일 조회를 없앤다.
    /// </summary>
    [CreateAssetMenu(menuName = "Tunnel Crew/Character Sheets", fileName = "CharacterSheets")]
    public sealed class CharacterSheetAsset : ScriptableObject
    {
        [Serializable]
        public sealed class DirectionSet
        {
            [Tooltip("sw / w / nw / n / s 중 하나")]
            public string direction;
            public Sprite[] walk;
            public Sprite[] fall;
        }

        public string role;
        public DirectionSet[] directions = Array.Empty<DirectionSet>();

        public DirectionSet Find(string direction)
        {
            foreach (var d in directions)
                if (d.direction == direction) return d;
            return null;
        }
    }
}
