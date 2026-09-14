using NUnit.Framework;
using System.Linq;
using TunnelCrew.EditorTools.ArtPipeline;
using TunnelCrew.Presentation;
using TunnelCrew.Presentation.Visual;
using TunnelCrew.Sim;
using UnityEngine;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 배치 2 의 나머지 규칙: 아트 계약(§12.2 · 아트 규격 §4·§6), 카메라 프로파일(§10),
    /// 접촉 그림자의 발 위치 추종(§7.4-1).
    /// </summary>
    public class VisualBatch2Tests
    {
        // ───────────────────────────── 파일명 규칙 (아트 규격 §4)

        [Test]
        public void 정상_파일명을_통과시키고_채널을_알려준다()
        {
            Assert.IsTrue(ApprovedArtContract.IsValidFileName(
                "tr01_wall_front_rock_a_albedo.png", out string channel, out _));
            Assert.AreEqual("albedo", channel);

            Assert.IsTrue(ApprovedArtContract.IsValidFileName(
                "tr01_prop_worklamp_b_normal.png", out channel, out _));
            Assert.AreEqual("normal", channel);
        }

        [Test]
        public void 대문자와_하이픈을_거부한다()
        {
            Assert.IsFalse(ApprovedArtContract.IsValidFileName(
                "TR01_wall_front_a_albedo.png", out _, out string why));
            Assert.IsNotNull(why);

            Assert.IsFalse(ApprovedArtContract.IsValidFileName(
                "tr01-wall-front-a-albedo.png", out _, out _));
        }

        [Test]
        public void 접두어와_채널_접미어를_요구한다()
        {
            Assert.IsFalse(ApprovedArtContract.IsValidFileName(
                "wall_front_rock_a_albedo.png", out _, out _), "tr01_ 접두어가 없다");
            Assert.IsFalse(ApprovedArtContract.IsValidFileName(
                "tr01_wall_front_rock_a_diffuse.png", out _, out _), "diffuse 는 채널이 아니다");
            Assert.IsFalse(ApprovedArtContract.IsValidFileName(
                "tr01_wall_albedo.png", out _, out _), "조각이 부족하다");
        }

        [Test]
        public void 개념_보드와_배치_보드_접미어를_허용한다()
        {
            Assert.IsTrue(ApprovedArtContract.IsValidFileName(
                "tr01_room_maintenance_a_concept.png", out string ch, out _));
            Assert.AreEqual("concept", ch);

            Assert.IsTrue(ApprovedArtContract.IsValidFileName(
                "tr01_module_kit_a_layout.png", out ch, out _));
            Assert.AreEqual("layout", ch);
        }

        [Test]
        public void png_이_아닌_확장자를_거부한다()
        {
            Assert.IsFalse(ApprovedArtContract.IsValidFileName(
                "tr01_floor_quiet_a_albedo.tga", out _, out _));
        }

        // ───────────────────────────── 슬롯 매핑

        [Test]
        public void 슬롯은_assetId_의_ID_범위로_정한다()
        {
            // 아트 규격 §7 의 ID 범위가 1순위 기준점이다.
            Assert.AreEqual(ApprovedArtContract.KitSlot.FloorBase,
                ApprovedArtContract.SlotForAssetId("TR01-FLR-BASE-A"));
            Assert.AreEqual(ApprovedArtContract.KitSlot.FloorEdge,
                ApprovedArtContract.SlotForAssetId("TR01-FLR-EDGE-N"));
            Assert.AreEqual(ApprovedArtContract.KitSlot.WallTop,
                ApprovedArtContract.SlotForAssetId("TR01-WTP-BASE-A"));
            Assert.AreEqual(ApprovedArtContract.KitSlot.WallTopRim,
                ApprovedArtContract.SlotForAssetId("TR01-WTP-RIM-A"));
            Assert.AreEqual(ApprovedArtContract.KitSlot.WallFront,
                ApprovedArtContract.SlotForAssetId("TR01-WFR-BASE-A"));
            Assert.AreEqual(ApprovedArtContract.KitSlot.ContactAo,
                ApprovedArtContract.SlotForAssetId("TR01-AO-CONTACT-A"));

            // 세트피스 계열은 이 배치에서 연결하지 않는다.
            Assert.AreEqual(ApprovedArtContract.KitSlot.None,
                ApprovedArtContract.SlotForAssetId("TR01-ARC-001"));
            Assert.AreEqual(ApprovedArtContract.KitSlot.None,
                ApprovedArtContract.SlotForAssetId("TR01-HERO-DRILL-A"));
        }

        [Test]
        public void 파일명_보조_매핑이_실제_납품_이름을_인식한다()
        {
            // 실제 승인 아트의 파일명. 카테고리가 두 조각이라 위치 기반 파싱으로는
            // 구분되지 않았고, 그 때문에 WTP·WFR·AO 가 전부 None 으로 떨어졌다.
            Assert.AreEqual(ApprovedArtContract.KitSlot.FloorBase,
                ApprovedArtContract.SlotFor("tr01_floor_base_a_albedo.png"));
            Assert.AreEqual(ApprovedArtContract.KitSlot.WallFront,
                ApprovedArtContract.SlotFor("tr01_wall_front_a_albedo.png"));
            Assert.AreEqual(ApprovedArtContract.KitSlot.WallTop,
                ApprovedArtContract.SlotFor("tr01_wall_top_a_albedo.png"));
            Assert.AreEqual(ApprovedArtContract.KitSlot.WallTopRim,
                ApprovedArtContract.SlotFor("tr01_wall_top_rim_a_albedo.png"),
                "wall_top_rim 이 wall_top 보다 먼저 걸려야 한다");
            Assert.AreEqual(ApprovedArtContract.KitSlot.ContactAo,
                ApprovedArtContract.SlotFor("tr01_contact_ao_a_albedo.png"));
            Assert.AreEqual(ApprovedArtContract.KitSlot.None,
                ApprovedArtContract.SlotFor("tr01_arch_gate_a_albedo.png"));
        }

        [Test]
        public void ResolveSlot_은_assetId_를_먼저_보고_파일명으로_넘어간다()
        {
            // assetId 가 결론을 내면 파일명이 달라도 그것을 따른다.
            Assert.AreEqual(ApprovedArtContract.KitSlot.WallTopRim,
                ApprovedArtContract.ResolveSlot("TR01-WTP-RIM-A", "tr01_floor_base_a_albedo.png"));

            // assetId 로 알 수 없으면 파일명으로 넘어간다.
            Assert.AreEqual(ApprovedArtContract.KitSlot.WallFront,
                ApprovedArtContract.ResolveSlot("SOMETHING-ELSE", "tr01_wall_front_a_albedo.png"));

            // 둘 다 모르면 None.
            Assert.AreEqual(ApprovedArtContract.KitSlot.None,
                ApprovedArtContract.ResolveSlot("TR01-ARC-001", "tr01_arch_gate_a_albedo.png"));
        }

        [Test]
        public void 세트피스와_소품은_이_배치에서_연결하지_않는다()
        {
            // ARC·PIL·LIN·LGT·DEC·HERO·FGV·VFX 는 footprint 예약과 소켓 데이터가 필요하다.
            Assert.AreEqual(ApprovedArtContract.KitSlot.None,
                ApprovedArtContract.SlotFor("tr01_arch_gate_a_albedo.png"));
            Assert.AreEqual(ApprovedArtContract.KitSlot.None,
                ApprovedArtContract.SlotFor("tr01_prop_worklamp_b_albedo.png"));
            Assert.AreEqual(ApprovedArtContract.KitSlot.None,
                ApprovedArtContract.SlotFor("tr01_vfx_dust_motes_a_albedo.png"));
        }

        [Test]
        public void 힌트가_없으면_슬롯에서_기본_레이어를_유추한다()
        {
            Assert.AreEqual("GroundBase",
                ApprovedArtContract.DefaultLayerForSlot(ApprovedArtContract.KitSlot.FloorBase));
            Assert.AreEqual("GroundDecal",
                ApprovedArtContract.DefaultLayerForSlot(ApprovedArtContract.KitSlot.ContactAo));
            Assert.AreEqual("WallTop",
                ApprovedArtContract.DefaultLayerForSlot(ApprovedArtContract.KitSlot.WallTop));
            Assert.AreEqual("BackStructure",
                ApprovedArtContract.DefaultLayerForSlot(ApprovedArtContract.KitSlot.WallFront));
            Assert.AreEqual("WorldEntity",
                ApprovedArtContract.DefaultLayerForSlot(ApprovedArtContract.KitSlot.None));

            // 아트가 실제로 쓴 'GroundAO' 는 별칭으로 받는다.
            Assert.AreEqual("GroundDecal",
                ApprovedArtContract.SortingLayerForHint("GroundAO", out var m));
            Assert.AreEqual(ApprovedArtContract.HintMatch.Alias, m);
        }

        [Test]
        public void 피벗이_없으면_슬롯에서_유추한다()
        {
            // 바닥·데칼은 중심, 벽·구조물은 하단(발점) — 좌상단 원점 Y 아래로 증가.
            ApprovedArtContract.DefaultPivotPixels(ApprovedArtContract.KitSlot.FloorBase,
                128, 128, out int fx, out int fy);
            Assert.AreEqual(64, fx);
            Assert.AreEqual(64, fy, "바닥은 footprint 중심");

            ApprovedArtContract.DefaultPivotPixels(ApprovedArtContract.KitSlot.WallFront,
                128, 192, out int wx, out int wy);
            Assert.AreEqual(64, wx);
            Assert.AreEqual(192, wy, "벽 정면은 하단이 발점");

            // 유추값이 Unity 피벗으로 옳게 변환되는지
            ApprovedArtContract.PivotToUnity(wx, wy, 128, 192, out float u, out float v);
            Assert.AreEqual(0.5f, u, 1e-5f);
            Assert.AreEqual(0f, v, 1e-5f);
        }

        [Test]
        public void 시각_높이는_바닥_슬롯에서_뜻이_없다()
        {
            Assert.IsFalse(ApprovedArtContract.VisualHeightMatters(ApprovedArtContract.KitSlot.FloorBase));
            Assert.IsFalse(ApprovedArtContract.VisualHeightMatters(ApprovedArtContract.KitSlot.ContactAo));
            Assert.IsTrue(ApprovedArtContract.VisualHeightMatters(ApprovedArtContract.KitSlot.WallFront));
            Assert.IsTrue(ApprovedArtContract.VisualHeightMatters(ApprovedArtContract.KitSlot.None));
        }

        // ───────────────────────────── 피벗 좌표계 계약 (아트 규격 §10)

        [Test]
        public void 피벗은_이미지_좌상단_원점에서_Unity_좌하단_정규화로_바뀐다()
        {
            // 128×128 캔버스의 발점이 아래 중앙이면 manifest 는 (64, 128) 로 적는다
            // — Y 가 아래로 증가하므로 캔버스 맨 아래는 128 이다.
            ApprovedArtContract.PivotToUnity(64, 128, 128, 128, out float u, out float v);
            Assert.AreEqual(0.5f, u, 1e-5f);
            Assert.AreEqual(0f, v, 1e-5f, "Unity 피벗은 좌하단 원점이라 0 이 된다");

            // 가운데
            ApprovedArtContract.PivotToUnity(64, 64, 128, 128, out u, out v);
            Assert.AreEqual(0.5f, u, 1e-5f);
            Assert.AreEqual(0.5f, v, 1e-5f);

            // 좌상단
            ApprovedArtContract.PivotToUnity(0, 0, 128, 128, out u, out v);
            Assert.AreEqual(0f, u, 1e-5f);
            Assert.AreEqual(1f, v, 1e-5f);
        }

        [Test]
        public void 타일링_자산은_셀_배수와_정확히_일치해야_한다()
        {
            var floor = ApprovedArtContract.KitSlot.FloorBase;

            // 1셀 타일 = 정확히 128×128. 실제 승인 아트가 이 크기로 온다.
            Assert.IsTrue(ApprovedArtContract.CanvasMatchesFootprint(
                128, 128, 1, 1, 0f, floor, out _));

            // 패딩을 넣으면 타일 이음새가 생긴다 → 오류다.
            Assert.IsFalse(ApprovedArtContract.CanvasMatchesFootprint(
                144, 144, 1, 1, 0f, floor, out string why));
            Assert.IsNotNull(why);
            Assert.IsTrue(why.Contains("이음새"), why);

            // 세로가 셀 높이와 안 맞으면 거부한다.
            Assert.IsFalse(ApprovedArtContract.CanvasMatchesFootprint(
                128, 130, 1, 1, 0f, floor, out _));

            // 여러 셀 바닥 타일 — 2×2 는 256×256.
            Assert.IsTrue(ApprovedArtContract.CanvasMatchesFootprint(
                256, 256, 2, 2, 0f, ApprovedArtContract.KitSlot.FloorBase, out _));
            Assert.IsFalse(ApprovedArtContract.CanvasMatchesFootprint(
                256, 128, 2, 2, 0f, ApprovedArtContract.KitSlot.FloorBase, out _));
        }

        [Test]
        public void 벽_정면은_footprint_깊이가_아니라_시각_높이로_판정한다()
        {
            // §8.6 의 벽 정면 높이는 0.75~1.5셀이다. footprint 는 1셀 경계에 서 있어도
            // 그려지는 높이는 다르다 — 두 축을 max 로 뭉개면 이 자산들이 거부된다.
            var wf = ApprovedArtContract.KitSlot.WallFront;

            Assert.IsTrue(ApprovedArtContract.CanvasMatchesFootprint(
                128, 96, 1, 1, 0.75f, wf, out string why), why ?? "0.75셀 = 96px");
            Assert.IsTrue(ApprovedArtContract.CanvasMatchesFootprint(
                128, 128, 1, 1, 1.0f, wf, out _), "1셀 = 128px");
            Assert.IsTrue(ApprovedArtContract.CanvasMatchesFootprint(
                128, 192, 1, 1, 1.5f, wf, out _), "1.5셀 = 192px");

            // 선언한 높이와 캔버스가 다르면 거부한다.
            Assert.IsFalse(ApprovedArtContract.CanvasMatchesFootprint(
                128, 128, 1, 1, 1.5f, wf, out _), "1.5셀인데 128px 이면 불일치");

            // 바닥 슬롯에서는 시각 높이를 무시하고 footprint 깊이를 쓴다.
            Assert.IsTrue(ApprovedArtContract.CanvasMatchesFootprint(
                128, 128, 1, 1, 0.75f, ApprovedArtContract.KitSlot.FloorBase, out _),
                "바닥은 시각 높이가 뜻이 없다");
        }

        [Test]
        public void 비타일_자산은_셀_배수_이상이면_되고_패딩은_권장이다()
        {
            var setpiece = ApprovedArtContract.KitSlot.None;

            // 아치: 5셀 폭 × 시각높이 3.9셀 → 640×512 (실제 승인 아트 크기)
            Assert.IsTrue(ApprovedArtContract.CanvasMatchesFootprint(
                640, 512, 5, 1, 3.9f, setpiece, out _));

            // 패딩이 있어도 통과한다.
            Assert.IsTrue(ApprovedArtContract.CanvasMatchesFootprint(
                640 + 16, 512 + 16, 5, 1, 3.9f, setpiece, out _));

            // 가로가 footprint 미달이면 오류다.
            Assert.IsFalse(ApprovedArtContract.CanvasMatchesFootprint(
                512, 512, 5, 1, 3.9f, setpiece, out string why));
            Assert.IsNotNull(why);

            // 시각 높이를 못 담으면 오류다.
            Assert.IsFalse(ApprovedArtContract.CanvasMatchesFootprint(
                640, 256, 5, 1, 3.9f, setpiece, out _));

            // 패딩 유무는 별도 판정 — 640×512 는 정확히 맞아떨어져 패딩이 없다.
            Assert.IsFalse(ApprovedArtContract.HasPadding(640, 512, 5, 1, 3.9f, 8));
            Assert.IsTrue(ApprovedArtContract.HasPadding(640 + 16, 512 + 16, 5, 1, 3.9f, 8));
        }

        [Test]
        public void 타일_슬롯은_타일링으로_비타일_슬롯은_아니게_분류된다()
        {
            Assert.IsTrue(ApprovedArtContract.IsTilingSlot(ApprovedArtContract.KitSlot.FloorBase));
            Assert.IsTrue(ApprovedArtContract.IsTilingSlot(ApprovedArtContract.KitSlot.WallTop));
            Assert.IsTrue(ApprovedArtContract.IsTilingSlot(ApprovedArtContract.KitSlot.WallFront));
            Assert.IsTrue(ApprovedArtContract.IsTilingSlot(ApprovedArtContract.KitSlot.ContactAo));
            Assert.IsFalse(ApprovedArtContract.IsTilingSlot(ApprovedArtContract.KitSlot.None),
                "세트피스·소품은 Tilemap 에 깔리지 않는다");
        }

        [Test]
        public void 알파는_실루엣이_필요한_자산에만_필수다()
        {
            // 셀을 꽉 채우는 불투명 타일 — 알파 채널이 없어도 정상이다.
            Assert.IsFalse(ApprovedArtContract.RequiresSilhouetteAlpha(ApprovedArtContract.KitSlot.FloorBase));
            Assert.IsFalse(ApprovedArtContract.RequiresSilhouetteAlpha(ApprovedArtContract.KitSlot.WallTop));
            Assert.IsFalse(ApprovedArtContract.RequiresSilhouetteAlpha(ApprovedArtContract.KitSlot.WallTopRim));
            Assert.IsFalse(ApprovedArtContract.RequiresSilhouetteAlpha(ApprovedArtContract.KitSlot.WallFront));

            // 부분만 덮는 데칼·조각과 세트피스는 알파가 필요하다.
            Assert.IsTrue(ApprovedArtContract.RequiresSilhouetteAlpha(ApprovedArtContract.KitSlot.ContactAo));
            Assert.IsTrue(ApprovedArtContract.RequiresSilhouetteAlpha(ApprovedArtContract.KitSlot.OuterCorner));
            Assert.IsTrue(ApprovedArtContract.RequiresSilhouetteAlpha(ApprovedArtContract.KitSlot.None));
        }

        [Test]
        public void Linear_로_임포트할_채널을_구분한다()
        {
            Assert.IsTrue(ApprovedArtContract.IsLinearChannel("normal"));
            Assert.IsTrue(ApprovedArtContract.IsLinearChannel("mask"));
            Assert.IsTrue(ApprovedArtContract.IsLinearChannel("ao"));
            Assert.IsTrue(ApprovedArtContract.IsLinearChannel("emission"));
            Assert.IsFalse(ApprovedArtContract.IsLinearChannel("albedo"), "Albedo 는 sRGB 다");
        }

        [Test]
        public void 아트_레이어_힌트를_구현_레이어로_옮긴다()
        {
            // 아트 쪽 별칭 — 결정적으로 매핑되므로 참고로만 알린다.
            Assert.AreEqual("BackStructure",
                ApprovedArtContract.SortingLayerForHint("WorldStructure", out var match));
            Assert.AreEqual(ApprovedArtContract.HintMatch.Alias, match);

            Assert.AreEqual("FrontStructure",
                ApprovedArtContract.SortingLayerForHint("WorldForeground", out match));
            Assert.AreEqual(ApprovedArtContract.HintMatch.Alias, match);

            // §6.4 이름은 그대로 통과한다.
            Assert.AreEqual("FrontStructure",
                ApprovedArtContract.SortingLayerForHint("FrontStructure", out match));
            Assert.AreEqual(ApprovedArtContract.HintMatch.Exact, match);

            // 매핑 불가는 경고 대상이다.
            Assert.AreEqual("WorldEntity",
                ApprovedArtContract.SortingLayerForHint("무엇인가", out match));
            Assert.AreEqual(ApprovedArtContract.HintMatch.Unknown, match);

            Assert.AreEqual("WorldEntity",
                ApprovedArtContract.SortingLayerForHint(null, out match));
            Assert.AreEqual(ApprovedArtContract.HintMatch.Unknown, match);

            // §6.4 이름은 모두 그대로 통과해야 한다.
            foreach (var name in VisualLayers.InOrder)
            {
                Assert.AreEqual(name, ApprovedArtContract.SortingLayerForHint(name, out var m),
                    $"{name} 은 그대로 통과해야 한다");
                Assert.AreEqual(ApprovedArtContract.HintMatch.Exact, m, $"{name} 은 §6.4 이름이다");
            }
        }

        // ───────────────────────────── 카메라 프로파일 (§10)

        static WorldVisualProfile MakeProfile()
        {
            var p = ScriptableObject.CreateInstance<WorldVisualProfile>();
            p.baseViewCells = 11.5f;
            p.combatViewCells = 10.5f;
            p.coopViewCells = 17f;
            p.verticalAnchorFromBottom = 0.56f;
            p.characterScreenHeightRange = new Vector2(0.13f, 0.18f);
            return p;
        }

        [Test]
        public void 줌_모드마다_가시_셀_수가_다르다()
        {
            var p = MakeProfile();
            try
            {
                Assert.AreEqual(11.5f, p.ViewCellsFor(CameraViewMode.Base), 1e-4f);
                Assert.AreEqual(10.5f, p.ViewCellsFor(CameraViewMode.Combat), 1e-4f);
                Assert.AreEqual(17f, p.ViewCellsFor(CameraViewMode.Coop), 1e-4f);

                // 근접 ≤ 기본 ≤ 협동 — 순서가 뒤집히면 동적 줌이 튄다.
                Assert.LessOrEqual(p.ViewCellsFor(CameraViewMode.Combat), p.ViewCellsFor(CameraViewMode.Base));
                Assert.GreaterOrEqual(p.ViewCellsFor(CameraViewMode.Coop), p.ViewCellsFor(CameraViewMode.Base));
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void orthographicSize_는_가시_높이의_절반이다()
        {
            var p = MakeProfile();
            try
            {
                Assert.AreEqual(11.5f * 0.5f, p.OrthographicSizeFor(CameraViewMode.Base), 1e-4f);
                Assert.AreEqual(17f * 0.5f, p.OrthographicSizeFor(CameraViewMode.Coop), 1e-4f);
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void 기본_줌에서_캐릭터_화면_비율이_목표_범위에_들어온다()
        {
            var p = MakeProfile();
            try
            {
                // §3.3 — 일반 캐릭터는 화면 높이의 13~18%.
                // 아트 규격 §3 의 캐릭터 기준 높이 1.2~1.5셀.
                Assert.IsTrue(p.CharacterRatioInRange(1.5f, CameraViewMode.Base),
                    $"1.5셀 / 11.5셀 = {p.CharacterScreenRatio(1.5f, CameraViewMode.Base):P1}");
                Assert.IsTrue(p.CharacterRatioInRange(1.5f, CameraViewMode.Combat));

                // 협동 줌아웃에서는 작아진다 — 명세가 허용하는 동작이다.
                Assert.IsFalse(p.CharacterRatioInRange(1.5f, CameraViewMode.Coop),
                    "협동 줌에서는 목표 범위 아래로 내려간다");
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void 세로_앵커는_유효_범위로_잘린다()
        {
            var p = MakeProfile();
            try
            {
                p.verticalAnchorFromBottom = 5f;
                Assert.LessOrEqual(p.AnchorFromBottom, 0.9f);
                p.verticalAnchorFromBottom = -3f;
                Assert.GreaterOrEqual(p.AnchorFromBottom, 0.1f);
            }
            finally { Object.DestroyImmediate(p); }
        }

        // ───────────────────────────── 접촉 그림자 (§7.4-1)

        [Test]
        public void 접촉_그림자가_발_위치를_따라간다()
        {
            IsometricProjection.SetPreset(ProjectionPreset.ReferenceTopDown);

            var profile = MakeProfile();
            var rendererGo = new GameObject("ContactShadowRenderer");
            var actorGo = new GameObject("Actor");

            try
            {
                var renderer = rendererGo.AddComponent<ContactShadowRenderer>();
                renderer.Profile = profile;

                var anchor = actorGo.AddComponent<VisualHeightAnchor>();
                anchor.groundPosition = new Vector2(3f, 4f);
                var shadow = actorGo.AddComponent<ContactShadow>();
                ContactShadowRenderer.Register(shadow);

                renderer.Apply();
                Assert.AreEqual(1, renderer.ActiveCount, "등록된 개체 하나에 그림자 하나");

                var sr = rendererGo.GetComponentInChildren<SpriteRenderer>();
                Assert.IsNotNull(sr, "그림자 스프라이트가 만들어져야 한다");
                Assert.IsNotNull(sr.sprite, "전용 스프라이트가 없으면 생성한 타원을 쓴다");

                var expected = IsometricProjection.ToRender(new Vector2(3f, 4f));
                Assert.AreEqual(expected.x, sr.transform.position.x, 1e-3f);
                Assert.AreEqual(expected.y, sr.transform.position.y, 1e-3f);

                // 발 위치를 옮기면 따라와야 한다.
                anchor.groundPosition = new Vector2(9.25f, 1.5f);
                renderer.Apply();

                expected = IsometricProjection.ToRender(new Vector2(9.25f, 1.5f));
                Assert.AreEqual(expected.x, sr.transform.position.x, 1e-3f);
                Assert.AreEqual(expected.y, sr.transform.position.y, 1e-3f);

                // 정렬은 발 위치 Y 로 — 바닥 데칼끼리도 앞뒤가 있어야 한다.
                Assert.AreEqual(DepthSort.OrderFor(1.5f, profile.depthUnitsPerCell), sr.sortingOrder);

                ContactShadowRenderer.Unregister(shadow);
                renderer.Apply();
                Assert.AreEqual(0, renderer.ActiveCount);
                Assert.IsFalse(sr.enabled, "풀은 파괴하지 않고 끈다 — GC 를 만들지 않는다");
            }
            finally
            {
                Object.DestroyImmediate(actorGo);
                Object.DestroyImmediate(rendererGo);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void 접촉_그림자는_컴포넌트_값이_0_이면_프로파일_값을_쓴다()
        {
            IsometricProjection.SetPreset(ProjectionPreset.ReferenceTopDown);

            var profile = MakeProfile();
            profile.contactShadowRadius = 0.5f;
            profile.contactShadowOpacity = 0.4f;

            var rendererGo = new GameObject("ContactShadowRenderer");
            var actorGo = new GameObject("Actor");
            try
            {
                var renderer = rendererGo.AddComponent<ContactShadowRenderer>();
                renderer.Profile = profile;

                actorGo.AddComponent<VisualHeightAnchor>().groundPosition = Vector2.zero;
                var shadow = actorGo.AddComponent<ContactShadow>();
                shadow.radius = 0f;      // 0 = 프로파일 값
                shadow.opacity = 0f;
                ContactShadowRenderer.Register(shadow);

                renderer.Apply();
                var sr = rendererGo.GetComponentInChildren<SpriteRenderer>();

                // 스프라이트는 1유닛 정사각형이고 크기는 스케일로 준다 → 지름 = 반지름 × 2
                Assert.AreEqual(1.0f, sr.transform.localScale.x, 1e-3f, "0.5셀 반지름 → 지름 1유닛");
                Assert.AreEqual(0.4f, sr.color.a, 1f / 255f);

                // 컴포넌트가 값을 주면 그것이 이긴다.
                shadow.radius = 0.25f;
                shadow.opacity = 0.9f;
                renderer.Apply();
                Assert.AreEqual(0.5f, sr.transform.localScale.x, 1e-3f);
                Assert.AreEqual(0.9f, sr.color.a, 1f / 255f);

                ContactShadowRenderer.Unregister(shadow);
            }
            finally
            {
                Object.DestroyImmediate(actorGo);
                Object.DestroyImmediate(rendererGo);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void 공중에_뜬_물체는_그림자가_작고_옅어진다()
        {
            IsometricProjection.SetPreset(ProjectionPreset.ReferenceTopDown);

            var profile = MakeProfile();
            profile.contactShadowRadius = 0.4f;
            profile.contactShadowOpacity = 0.5f;

            var rendererGo = new GameObject("ContactShadowRenderer");
            var actorGo = new GameObject("Actor");
            try
            {
                var renderer = rendererGo.AddComponent<ContactShadowRenderer>();
                renderer.Profile = profile;

                var anchor = actorGo.AddComponent<VisualHeightAnchor>();
                anchor.groundPosition = new Vector2(2f, 2f);
                anchor.visualHeight = 0f;
                var shadow = actorGo.AddComponent<ContactShadow>();
                shadow.scaleWithVisualHeight = true;
                ContactShadowRenderer.Register(shadow);

                renderer.Apply();
                var sr = rendererGo.GetComponentInChildren<SpriteRenderer>();
                float groundedScale = sr.transform.localScale.x;
                float groundedAlpha = sr.color.a;

                anchor.visualHeight = 2f;    // 공중
                renderer.Apply();

                Assert.Less(sr.transform.localScale.x, groundedScale, "높이가 오르면 작아진다");
                Assert.Less(sr.color.a, groundedAlpha, "높이가 오르면 옅어진다");

                // 정렬 기준은 여전히 지면이다(§6.5).
                Assert.AreEqual(DepthSort.OrderFor(2f, profile.depthUnitsPerCell), sr.sortingOrder);

                ContactShadowRenderer.Unregister(shadow);
            }
            finally
            {
                Object.DestroyImmediate(actorGo);
                Object.DestroyImmediate(rendererGo);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void 수호자_드론은_지면_좌표와_시각_높이를_분리한다()
        {
            var go = new GameObject("GuardianDroneViewTest");
            try
            {
                var relics = new RelicSystem();
                relics.Ids.Add("r_guardian");
                relics.DronePos = new Vec2(4.25, 7.5);
                relics.DroneBob = System.Math.PI * .5;
                relics.DroneFace = -1;

                var view = go.AddComponent<GuardianDroneView>();
                view.Bind(relics);
                view.Render(0f);

                Assert.IsTrue(view.IsVisible);
                Assert.AreEqual(new Vector2(4.25f, 7.5f), view.Anchor.groundPosition);
                Assert.Greater(view.Anchor.visualHeight, GuardianDroneView.BaseVisualHeight);
                Assert.IsTrue(view.BodyRenderer.flipX);
                Assert.IsNotNull(view.BodyRenderer.sprite);
                Assert.IsNotNull(view.GetComponentInChildren<ContactShadow>());

                relics.Ids.Clear();
                view.Render(0f);
                Assert.IsFalse(view.IsVisible);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 방향성_그림자는_접촉_AO_뒤로_분리되어_그려진다()
        {
            IsometricProjection.SetPreset(ProjectionPreset.ReferenceTopDown);

            var profile = MakeProfile();
            var rendererGo = new GameObject("ContactShadowRenderer");
            var actorGo = new GameObject("Actor");
            try
            {
                var renderer = rendererGo.AddComponent<ContactShadowRenderer>();
                renderer.Profile = profile;
                actorGo.AddComponent<VisualHeightAnchor>().groundPosition = new Vector2(3f, 4f);
                var shadow = actorGo.AddComponent<ContactShadow>();
                shadow.castLength = 0.5f;
                shadow.castDirection = new Vector2(1f, -0.5f);
                ContactShadowRenderer.Register(shadow);

                renderer.Apply();
                var sprites = rendererGo.GetComponentsInChildren<SpriteRenderer>();
                Assert.AreEqual(2, renderer.ActiveCount, "접촉 AO와 방향성 투사 그림자 두 겹");
                Assert.AreEqual(2, sprites.Length);
                var contact = sprites.Single(s => s.name.StartsWith("Contact AO"));
                var cast = sprites.Single(s => s.name.StartsWith("Directional cast"));
                Assert.Greater(cast.transform.position.x, contact.transform.position.x);
                Assert.Less(cast.transform.position.y, contact.transform.position.y);
                Assert.Less(cast.color.a, contact.color.a);
                Assert.Less(cast.sortingOrder, contact.sortingOrder);

                ContactShadowRenderer.Unregister(shadow);
            }
            finally
            {
                Object.DestroyImmediate(actorGo);
                Object.DestroyImmediate(rendererGo);
                Object.DestroyImmediate(profile);
            }
        }

        // ───────────────────────────── 채널 디버그 전역값

        [Test]
        public void 채널_끄기는_기본값이_전부_켜짐이다()
        {
            VisualChannelDebug.Reset();
            Assert.AreEqual(ChannelView.Composite, VisualChannelDebug.View);
            Assert.IsTrue(VisualChannelDebug.UseNormal);
            Assert.IsTrue(VisualChannelDebug.UseEmission);
            Assert.IsTrue(VisualChannelDebug.UseMask);
            Assert.IsTrue(VisualChannelDebug.UseAo);

            VisualChannelDebug.ToggleNormal();
            Assert.IsFalse(VisualChannelDebug.UseNormal);
            Assert.IsTrue(VisualChannelDebug.UseEmission, "하나만 바뀌어야 한다");

            VisualChannelDebug.Reset();
            Assert.IsTrue(VisualChannelDebug.UseNormal);
        }

        [Test]
        public void 채널_단독_보기가_순환한다()
        {
            VisualChannelDebug.Reset();
            int n = System.Enum.GetValues(typeof(ChannelView)).Length;
            for (int i = 0; i < n; i++) VisualChannelDebug.NextView();
            Assert.AreEqual(ChannelView.Composite, VisualChannelDebug.View,
                "한 바퀴 돌면 합성으로 돌아온다");
            VisualChannelDebug.Reset();
        }

        // ───────────────────────────── PNG 헤더 읽기

        [Test]
        public void PNG_헤더에서_크기와_알파를_읽는다()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "tc-png-" + System.Guid.NewGuid().ToString("N") + ".png");
            var tex = new Texture2D(37, 19, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.red);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            try
            {
                var info = PngInfo.Read(path);
                Assert.IsTrue(info.Valid, info.Error);
                Assert.AreEqual(37, info.Width);
                Assert.AreEqual(19, info.Height);
                Assert.IsTrue(info.HasAlpha);
            }
            finally { System.IO.File.Delete(path); }
        }

        [Test]
        public void 없는_파일은_읽기_실패로_보고한다()
        {
            var info = PngInfo.Read(System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "tc-does-not-exist-" + System.Guid.NewGuid() + ".png"));
            Assert.IsFalse(info.Valid);
            Assert.IsNotNull(info.Error);
        }

        [Test]
        public void PNG_가_아닌_파일을_거부한다()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "tc-not-png-" + System.Guid.NewGuid().ToString("N") + ".png");
            System.IO.File.WriteAllText(path, "this is not a png");
            try
            {
                var info = PngInfo.Read(path);
                Assert.IsFalse(info.Valid);
            }
            finally { System.IO.File.Delete(path); }
        }

        // ───────────────────────────── MiniJson

        [Test]
        public void MiniJson_이_manifest_모양을_읽는다()
        {
            var root = MiniJson.AsMap(MiniJson.Parse(@"{
              ""a"": 1, ""b"": ""x"", ""c"": [1, 2], ""d"": { ""e"": true }, ""f"": null,
              ""g"": 3.25, ""h"": -7
            }", out string error));

            Assert.IsNull(error, error);
            Assert.IsNotNull(root);
            Assert.IsTrue(MiniJson.TryGetInt(root, "a", out int a));
            Assert.AreEqual(1, a);
            Assert.AreEqual("x", MiniJson.GetString(root, "b"));
            Assert.IsTrue(MiniJson.TryGetInt2(root, "c", out int c0, out int c1));
            Assert.AreEqual(1, c0);
            Assert.AreEqual(2, c1);
            Assert.IsTrue(MiniJson.TryGetFloat(root, "g", out float g));
            Assert.AreEqual(3.25f, g, 1e-5f);
            Assert.IsTrue(MiniJson.TryGetInt(root, "h", out int h));
            Assert.AreEqual(-7, h);
        }

        [Test]
        public void MiniJson_은_깨진_입력을_예외가_아니라_오류로_돌려준다()
        {
            Assert.IsNull(MiniJson.Parse("{ \"a\": ", out string e1));
            Assert.IsNotNull(e1);

            Assert.IsNull(MiniJson.Parse("", out string e2));
            Assert.IsNotNull(e2);

            Assert.IsNull(MiniJson.Parse("{ \"a\" 1 }", out string e3));
            Assert.IsNotNull(e3);
        }
    }
}
