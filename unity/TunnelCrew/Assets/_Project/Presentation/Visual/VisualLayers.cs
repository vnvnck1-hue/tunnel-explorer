namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 기능명세서 §6.4 의 렌더 레이어. 이름 문자열을 코드 여러 곳에 흩어놓지 않기 위한 단일 출처다.
    ///
    /// 프로젝트 Sorting Layer 는 <c>Tunnel Crew/비주얼 · 소팅 레이어 생성</c>(BuildVisualLayers)
    /// 이 이 순서 그대로 만든다. 기존 <c>Default</c> 하나에 sortingOrder 숫자만 크게 벌려 쓰는
    /// 방식(§4.2)은 폐기한다.
    /// </summary>
    public static class VisualLayers
    {
        /// <summary>화면 외 암흑, 먼 후경, 지하 공극.</summary>
        public const string WorldVoid = "WorldVoid";
        /// <summary>큰 바닥 덩어리.</summary>
        public const string GroundBase = "GroundBase";
        /// <summary>경계 블렌드, 균열, 레일 바닥부.</summary>
        public const string GroundDetail = "GroundDetail";
        /// <summary>먼지, 액체, 파편, 접촉 AO.</summary>
        public const string GroundDecal = "GroundDecal";
        /// <summary>북쪽 벽 정면, 후경 기둥, 배경 구조물.</summary>
        public const string BackStructure = "BackStructure";
        /// <summary>벽 상단과 고체 셀 표면.</summary>
        public const string WallTop = "WallTop";
        /// <summary>캐릭터, 적, 드롭, 설치물, 중경 소품.</summary>
        public const string WorldEntity = "WorldEntity";
        /// <summary>남쪽 벽, 문틀 전면, 전경 기둥·암반.</summary>
        public const string FrontStructure = "FrontStructure";
        /// <summary>총구 화염, 충돌, 먼지, 광선 일부.</summary>
        public const string WorldFX = "WorldFX";
        /// <summary>LOS 어둠, 안개, 깊이 색보정.</summary>
        public const string VisionAndGrade = "VisionAndGrade";
        /// <summary>핑, 텔레그래프, 월드 라벨.</summary>
        public const string WorldOverlay = "WorldOverlay";
        /// <summary>HUD 와 메뉴.</summary>
        public const string UI = "UI";

        /// <summary>§6.4 표의 순서. 앞쪽이 뒤에 그려진다(index 0 = 가장 뒤).</summary>
        public static readonly string[] InOrder =
        {
            WorldVoid,
            GroundBase,
            GroundDetail,
            GroundDecal,
            BackStructure,
            WallTop,
            WorldEntity,
            FrontStructure,
            WorldFX,
            VisionAndGrade,
            WorldOverlay,
            UI,
        };

        /// <summary>
        /// 발 위치 Y 로 상호 정렬하는 깊이 밴드(§6.5). 이 레이어들 안에서는
        /// <see cref="DepthSort"/> 가 만든 order 가 실제 앞뒤를 결정한다.
        /// </summary>
        public static readonly string[] DepthBand =
        {
            BackStructure,
            WallTop,
            WorldEntity,
            FrontStructure,
        };

        /// <summary>
        /// 이 이름의 Sorting Layer 가 프로젝트에 있는가.
        ///
        /// <c>SortingLayer.NameToID</c> 로 판정하지 않는다 — 그 함수는 인덱스가 아니라
        /// uniqueID 를 돌려주므로, "없음"과 "ID 가 0 인 레이어"를 구분할 수 없다.
        /// </summary>
        public static bool Exists(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var layers = UnityEngine.SortingLayer.layers;
            for (int i = 0; i < layers.Length; i++)
                if (layers[i].name == name) return true;
            return false;
        }
    }
}
