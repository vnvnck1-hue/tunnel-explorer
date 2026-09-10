using System.Collections.Generic;
using TunnelCrew.Sim;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 본편 AI 크루 손전등을 랩에 세운다(본선 조명 대조에서 빠져 있던 항목, 2026-09-10).
    ///
    /// 코옵 화면에서 빛의 절반은 동료 손전등이다 — 이게 없는 랩은 본선보다 어둡고, 그림자 예산(§13)이
    /// 실제로 경쟁하는지도 볼 수 없다. 수치는 <c>RunBootstrap</c> 의 크루 손전등 그대로
    /// (40/56° · 반경 8.4 · 세기 2.0 · (1,.94,.80)). 그림자는 분류 규칙(Scout)을 따른다.
    ///
    /// 크루는 랩에 없으므로 <b>서 있는 동료</b>로 둔다 — 자리와 조준 방향만 준다. 시야원으로도 등록해
    /// 동료가 비춘 곳을 팀이 함께 보는(원본 LOS 합산) 동작까지 랩에서 확인한다.
    /// </summary>
    public sealed class LabCrewLights : MonoBehaviour
    {
        [System.Serializable]
        public struct Member
        {
            [Tooltip("셀 좌표(절대). 바닥 칸이어야 한다.")] public Vector2 cell;
            [Tooltip("조준 각(도). 0 = 동쪽, 90 = 북쪽.")] public float aimDegrees;
        }

        [SerializeField] Member[] _members = System.Array.Empty<Member>();
        [SerializeField] bool _enabled = true;

        readonly List<Light2D> _lights = new();
        readonly List<VisionSource> _vision = new();

        public bool Active
        {
            get => _enabled;
            set { _enabled = value; foreach (var l in _lights) if (l != null) l.enabled = value; }
        }

        /// <summary>LOS 에 합산할 시야원. 꺼져 있으면 비어 있다.</summary>
        public IReadOnlyList<VisionSource> VisionSources => _enabled ? _vision : System.Array.Empty<VisionSource>();

        public void EditorAssign(Member[] members) => _members = members ?? System.Array.Empty<Member>();

        void Awake()
        {
            foreach (var m in _members)
            {
                var go = new GameObject($"Crew Flashlight ({m.cell.x:0},{m.cell.y:0})");
                go.transform.SetParent(transform, false);
                go.transform.position = IsometricProjection.ToRender3(new Vector2(m.cell.x, m.cell.y));
                // Light2D 스팟은 위쪽(+Y)이 기준이라 90도를 뺀다 — 본편과 같다.
                go.transform.rotation = Quaternion.Euler(0f, 0f,
                    IsometricProjection.AngleToRender(m.aimDegrees * Mathf.Deg2Rad) * Mathf.Rad2Deg - 90f);

                var l = go.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.pointLightInnerAngle = 40f; l.pointLightOuterAngle = 56f;
                l.pointLightInnerRadius = 0.6f; l.pointLightOuterRadius = 8.4f;
                l.intensity = 2.0f;
                l.color = new Color(1f, 0.94f, 0.80f);
                l.shadowIntensity = LightClassRules.ShadowIntensity(LightClass.Scout);
                l.shadowSoftness = LightClassRules.ShadowSoftness(LightClass.Scout);
                l.targetSortingLayers = VisualLayers.LitGroundLevelLayerIds();   // 지면 광원 — 벽 윗면은 비추지 않는다
                l.enabled = _enabled;
                _lights.Add(l);

                _vision.Add(VisionSource.Crew(new Vec2(m.cell.x, m.cell.y)));
            }
        }
    }
}
