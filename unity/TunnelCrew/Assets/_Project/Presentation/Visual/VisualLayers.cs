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
        /// Unity 기본 Sorting Layer. 이 체계에는 자리가 없지만 본선 Run 씬의 타일맵·캐릭터가
        /// 아직 전부 여기 있어서 <see cref="Lit"/> 이 반드시 포함해야 한다.
        /// </summary>
        public const string UnlayeredDefault = "Default";

        /// <summary>
        /// 2D 광원이 비춰야 하는 레이어(§7.1 "통합 월드 재질" — 바닥·벽·오브젝트가 같은
        /// 재질과 같은 빛을 받는다).
        ///
        /// <b>이 목록이 있는 이유</b> — <c>Light2D</c> 의 대상 레이어를 씬에 손으로 적어 두면
        /// 레이어를 새로 만들 때 조용히 빠진다. 실제로 <c>WorldEntity</c>·<c>FrontStructure</c>
        /// 가 빠져 있어서 수정·상자·드릴·난간이 빛을 한 줄기도 받지 못했다(2026-09-09).
        /// 바닥만 밝고 오브젝트는 평평한 화면이 그 결과였다.
        ///
        /// <b><see cref="UnlayeredDefault"/> 가 반드시 들어 있어야 한다.</b> 본선 Run 씬은 아직
        /// 이 레이어 체계를 쓰지 않아 바닥·벽 타일맵과 캐릭터가 전부 <c>Default</c> 에 있다.
        /// 2026-09-09 에 이 목록을 만들며 <c>Default</c> 를 빼는 바람에
        /// <c>RunBootstrap.UseNormalMaps</c> 를 지나는 램프·손전등·플레이어 후광이
        /// <b>아무것도 비추지 못했다</b> — 게임이 전역광만으로 평평하게 보였다. 본선을 레이어
        /// 체계로 옮기는 것은 렌더 순서를 건드리는 별도 작업이고, 그때까지 <c>Default</c> 는
        /// 실제 콘텐츠가 사는 레이어다.
        ///
        /// 빠진 것: <see cref="WorldVoid"/>(빛이 닿을 표면이 아니다), <see cref="WorldFX"/>·
        /// <see cref="VisionAndGrade"/>·<see cref="WorldOverlay"/>·<see cref="UI"/>
        /// (자체 발광·후처리·UI 라 2D 조명을 곱하면 안 된다). 화면을 덮는
        /// <c>DarknessOverlay</c> 도 <c>Default</c> 에 있지만 언릿 셰이더라 영향을 받지 않는다.
        /// </summary>
        public static readonly string[] Lit =
        {
            UnlayeredDefault,
            GroundBase,
            GroundDetail,
            GroundDecal,
            BackStructure,
            WallTop,
            WorldEntity,
            FrontStructure,
        };

        /// <summary>
        /// <see cref="Lit"/> 의 Sorting Layer ID. <c>Light2D.targetSortingLayers</c> 가 ID 를 받는다.
        /// 레이어가 추가·삭제되면 <see cref="SortingLayer.layers"/> 가 바뀌므로 매번 다시 만든다
        /// (호출 지점이 프레임마다 도는 곳이 아니다).
        /// </summary>
        /// <summary>
        /// <b>지면 높이</b> 광원이 비추는 레이어. <see cref="Lit"/> 에서 <see cref="WallTop"/> 과
        /// <see cref="FrontStructure"/> 를 뺀 것이다.
        ///
        /// <b>왜 빼는가</b> — 그 둘은 벽의 <b>윗면</b>이다. 바닥에 서 있는 캐릭터의 손전등이
        /// 수평으로 비추는데 벽 윗면이 밝아지면 벽에 높이가 없다는 뜻이 된다(2026-09-10 지적).
        /// 지면 광원은 바닥·벽 <b>정면</b>(BackStructure)·개체만 비춘다. 윗면을 밝히는 것은
        /// 전역광과 천장·공중 광원의 몫이다.
        /// </summary>
        public static readonly string[] LitGroundLevel =
        {
            UnlayeredDefault,
            GroundBase,
            GroundDetail,
            GroundDecal,
            BackStructure,
            WorldEntity,
        };

        /// <summary><see cref="LitGroundLevel"/> 의 Sorting Layer ID.</summary>
        public static int[] LitGroundLevelLayerIds() => IdsOf(LitGroundLevel);

        /// <summary>
        /// <b>높이가 있는 면</b> — 벽 윗면과 전경 cap. <see cref="Lit"/> 에서 <see cref="LitGroundLevel"/> 을 뺀 나머지다.
        ///
        /// 개정 R2(2026-09-10): 전역광을 둘로 나눈다. 바닥용 전역광과 <b>윗면용 전역광</b>이 서로 다른
        /// 레이어를 비추고, 윗면 쪽은 훨씬 어둡다(기획서 §7.2 "벽 상단·정면·바닥에 서로 다른 최소광 계수").
        /// 그러지 않으면 방에 인접한 벽 윗면이 바닥과 같은 밝기로 통째로 드러나 폐쇄감이 사라진다 —
        /// 코어키퍼에서 윗면은 빛이 옆(방)에서 오기 때문에 정면보다 훨씬 어둡다.
        /// </summary>
        public static readonly string[] LitElevated =
        {
            WallTop,
            FrontStructure,
        };

        /// <summary><see cref="LitElevated"/> 의 Sorting Layer ID.</summary>
        public static int[] LitElevatedLayerIds() => IdsOf(LitElevated);

        /// <summary>
        /// 벽 그림자(<c>ShadowCaster2D</c>)가 <b>떨어지는</b> 레이어. <see cref="LitGroundLevel"/> 에서
        /// <see cref="WorldEntity"/> 를 뺀 것이다.
        ///
        /// <b>왜 빼는가</b> — URP 2D 그림자는 광원이 비추는 모든 레이어에 드리운다. 캐릭터가 벽 옆에 서면
        /// 벽 그림자가 캐릭터를 통째로 덮어 "벽 뒤에 있다"고 읽힌다(2026-09-10 지적). 벽 그림자는
        /// 바닥·벽 정면에만 떨어지고, 개체는 자기 접촉 그림자(ContactShadow)로만 바닥에 붙는다.
        /// 벽 윗면·전경 cap 도 제외 — 옆 벽의 그림자가 윗면에 떨어지면 높이 관계가 뒤집혀 읽힌다.
        /// </summary>
        public static readonly string[] ShadowReceivers =
        {
            UnlayeredDefault,
            GroundBase,
            GroundDetail,
            GroundDecal,
            BackStructure,
        };

        /// <summary><see cref="ShadowReceivers"/> 의 Sorting Layer ID.</summary>
        public static int[] ShadowReceiverLayerIds() => IdsOf(ShadowReceivers);

        /// <summary>이름 목록을 Sorting Layer ID 로 바꾼다. 없는 레이어는 건너뛴다.</summary>
        static int[] IdsOf(string[] names)
        {
            var ids = new System.Collections.Generic.List<int>(names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                if (!Exists(names[i])) continue;
                ids.Add(UnityEngine.SortingLayer.NameToID(names[i]));
            }
            return ids.ToArray();
        }

        public static int[] LitLayerIds()
        {
            var ids = new System.Collections.Generic.List<int>(Lit.Length);
            for (int i = 0; i < Lit.Length; i++)
            {
                if (!Exists(Lit[i])) continue;
                ids.Add(UnityEngine.SortingLayer.NameToID(Lit[i]));
            }
            return ids.ToArray();
        }

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
