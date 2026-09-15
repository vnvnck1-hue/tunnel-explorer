using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>풀에서 빌려 쓰는 순간 발광 버스트. 스프라이트와 Light2D 를 함께 감쇠시킨다.</summary>
    public sealed class ModularGunnerFxBurst : MonoBehaviour
    {
        SpriteRenderer _renderer;
        Light2D _light;
        float _life;
        float _age;
        float _startScale;
        float _endScale;
        float _lightIntensity;
        Color _color;
        bool _active;
        Sprite[] _frames;

        /// <summary>풀이 한 번만 부르는 구성.</summary>
        public void PoolAwake()
        {
            _renderer = gameObject.AddComponent<SpriteRenderer>();
            _light = gameObject.AddComponent<Light2D>();
            _light.lightType = Light2D.LightType.Point;
            _light.pointLightInnerAngle = 360f;
            _light.pointLightOuterAngle = 360f;
            Release();
        }

        public void Prepare(Sprite sprite, Material material, Color color, float life, float startScale,
            float endScale, int sortingOrder, float lightRadius, float lightIntensity)
            => Prepare(sprite, null, material, color, life, startScale, endScale, sortingOrder, lightRadius, lightIntensity);

        /// <summary>
        /// frames 를 주면 수명 동안 시트를 순서대로 재생한다(로드맵 4.3 · 4.9의 전용 시트).
        /// 없으면 기존처럼 스프라이트 한 장을 키우며 사라진다.
        /// </summary>
        public void Prepare(Sprite sprite, Sprite[] frames, Material material, Color color, float life, float startScale,
            float endScale, int sortingOrder, float lightRadius, float lightIntensity)
        {
            _frames = frames != null && frames.Length > 0 ? frames : null;
            _renderer.sprite = _frames != null ? _frames[0] : sprite;
            _renderer.sharedMaterial = material;
            _renderer.color = color;
            _renderer.sortingOrder = sortingOrder;
            _renderer.enabled = true;

            _life = life;
            _age = 0f;
            _startScale = startScale;
            _endScale = endScale;
            _color = color;
            _lightIntensity = lightIntensity;
            transform.localScale = Vector3.one * startScale;

            bool lit = lightIntensity > 0f;
            _light.enabled = lit;
            if (lit)
            {
                _light.color = color;
                _light.intensity = lightIntensity;
                _light.pointLightInnerRadius = lightRadius * 0.18f;
                _light.pointLightOuterRadius = lightRadius;
            }

            _active = true;
            gameObject.SetActive(true);
        }

        void Release()
        {
            _active = false;
            _renderer.enabled = false;
            _light.enabled = false;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!_active) return;

            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / Mathf.Max(0.001f, _life));

            Color c = _color;
            if (_frames != null)
            {
                // 전용 시트는 프레임이 이미 확산·감쇠를 그리고 있다.
                // 크기를 고정하고 알파도 마지막 구간에서만 내려 픽셀이 뭉개지지 않게 한다.
                int index = Mathf.Min(_frames.Length - 1, Mathf.FloorToInt(t * _frames.Length));
                _renderer.sprite = _frames[index];
                transform.localScale = Vector3.one * _endScale;
                c.a *= t > 0.75f ? Mathf.InverseLerp(1f, 0.75f, t) : 1f;
            }
            else
            {
                transform.localScale = Vector3.one * Mathf.Lerp(_startScale, _endScale, t);
                c.a *= 1f - t;
            }
            _renderer.color = c;

            if (_light.enabled) _light.intensity = _lightIntensity * (1f - t) * (1f - t);
            if (_age >= _life) Release();
        }
    }
}
