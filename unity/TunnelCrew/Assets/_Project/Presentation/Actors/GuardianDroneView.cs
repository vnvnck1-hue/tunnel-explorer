using TunnelCrew.Presentation.Visual;
using TunnelCrew.Sim;
using UnityEngine;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 태엽 수호자 드론의 시뮬레이션 지면 좌표와 시각 높이를 분리한다.
    /// 몸체는 공중에 떠 있지만 정렬과 그림자는 <see cref="VisualHeightAnchor.groundPosition"/>을
    /// 기준으로 남아, 레퍼런스처럼 몸체와 작은 바닥 그림자 사이의 간격으로 높이를 읽게 한다.
    /// </summary>
    public sealed class GuardianDroneView : MonoBehaviour
    {
        public const float BaseVisualHeight = 0.78f;
        public const float BodyScale = 0.78f;

        RelicSystem _relics;
        GameObject _visualRoot;
        VisualHeightAnchor _anchor;
        SpriteRenderer _body;
        Material _litMaterial;

        public bool IsVisible => _visualRoot != null && _visualRoot.activeSelf;
        public VisualHeightAnchor Anchor => _anchor;
        public SpriteRenderer BodyRenderer => _body;

        public void Bind(RelicSystem relics)
        {
            _relics = relics;
            EnsureBuilt();
            _visualRoot.SetActive(relics != null && relics.HasDrone);
        }

        public void Render(float dt)
        {
            EnsureBuilt();
            bool visible = _relics != null && _relics.HasDrone;
            if (_visualRoot.activeSelf != visible) _visualRoot.SetActive(visible);
            if (!visible) return;

            _anchor.groundPosition = new Vector2((float)_relics.DronePos.X, (float)_relics.DronePos.Y);
            _anchor.visualHeight = BaseVisualHeight + Mathf.Sin((float)_relics.DroneBob) * 0.055f;
            _body.flipX = _relics.DroneFace < 0;

            // 시뮬레이션 bob은 높이에 쓰고, 몸체에는 아주 작은 압축만 더해 날개 진동을 암시한다.
            float flap = Mathf.Sin((float)_relics.DroneBob * 5f) * 0.025f;
            _body.transform.localScale = new Vector3(BodyScale * (1f + flap), BodyScale * (1f - flap), 1f);
        }

        void EnsureBuilt()
        {
            if (_visualRoot != null) return;

            _visualRoot = new GameObject("Guardian Drone Visual");
            _visualRoot.transform.SetParent(transform, false);

            _anchor = _visualRoot.AddComponent<VisualHeightAnchor>();
            _anchor.body = new GameObject("Body").transform;
            _anchor.body.SetParent(_visualRoot.transform, false);
            _anchor.footprintCells = new Vector2(0.7f, 0.5f);
            _anchor.footprintRadius = 0.38f;
            _anchor.sortingLayer = VisualLayers.WorldEntity;
            _anchor.localOrder = 2;

            _body = _anchor.body.gameObject.AddComponent<SpriteRenderer>();
            _body.sprite = Resources.Load<Sprite>("Visual/guardian_drone");
            _body.sortingOrder = 0;
            var lit = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            if (lit != null)
            {
                _litMaterial = new Material(lit) { name = "Guardian Drone Lit" };
                _body.sharedMaterial = _litMaterial;
            }

            var shadow = _visualRoot.AddComponent<ContactShadow>();
            shadow.radius = 0.31f;
            shadow.squash = 0.42f;
            shadow.opacity = 0.34f;
            shadow.offset = new Vector2(0.10f, -0.06f);
            shadow.scaleWithVisualHeight = true;
            shadow.castLength = 0.20f;
            shadow.castDirection = new Vector2(0.72f, -0.38f);
            shadow.castOpacity = 0.42f;
        }

        void OnDestroy()
        {
            if (_litMaterial != null) Destroy(_litMaterial);
        }
    }
}
