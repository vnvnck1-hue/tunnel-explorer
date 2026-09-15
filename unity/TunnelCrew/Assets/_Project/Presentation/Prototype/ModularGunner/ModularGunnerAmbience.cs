using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>
    /// 로드맵 4.9 — 환경 미세 움직임.
    /// 먼지 입자와 낙진은 1픽셀 사각형이라 절차적으로 만든다. 램프는 미세하게 깜빡인다.
    /// 전부 고정 배열이라 매 프레임 할당이 없다.
    /// </summary>
    public sealed class ModularGunnerAmbience : MonoBehaviour
    {
        const int MoteCount = 90;
        const float MoteSpan = 13f;        // 카메라 주변 이 반경 안에서만 떠돈다.

        [SerializeField] Sprite _moteSprite;
        [SerializeField] Material _unlitMaterial;
        [SerializeField] Light2D[] _lamps;

        struct Mote
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector2 Drift;
            public float Life;
            public float Age;
            public float Alpha;
        }

        Mote[] _motes;
        float[] _lampBase;
        float[] _lampSeed;
        Transform _follow;

        public void EditorAssign(Sprite moteSprite, Material unlitMaterial, Light2D[] lamps)
        {
            _moteSprite = moteSprite;
            _unlitMaterial = unlitMaterial;
            _lamps = lamps;
        }

        void Awake()
        {
            var root = new GameObject("Dust Motes").transform;
            root.SetParent(transform, false);
            _motes = new Mote[MoteCount];
            for (int i = 0; i < MoteCount; i++)
            {
                var go = new GameObject("Mote");
                go.transform.SetParent(root, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = _moteSprite;
                renderer.sharedMaterial = _unlitMaterial;
                renderer.sortingOrder = 380;
                _motes[i] = new Mote { Transform = go.transform, Renderer = renderer };
                Respawn(ref _motes[i], Random.Range(0f, 1f));
            }

            if (_lamps == null) return;
            _lampBase = new float[_lamps.Length];
            _lampSeed = new float[_lamps.Length];
            for (int i = 0; i < _lamps.Length; i++)
            {
                if (_lamps[i] == null) continue;
                _lampBase[i] = _lamps[i].intensity;
                _lampSeed[i] = Random.value * 100f;
            }
        }

        void Start() => _follow = Camera.main != null ? Camera.main.transform : null;

        void Respawn(ref Mote mote, float startProgress)
        {
            Vector2 center = _follow != null ? (Vector2)_follow.position : Vector2.zero;
            mote.Transform.position = center + Random.insideUnitCircle * MoteSpan;
            // 굴착 공간의 공기는 거의 정지해 있다. 아주 느리게 아래로 가라앉는다.
            mote.Drift = new Vector2(Random.Range(-0.22f, 0.22f), Random.Range(-0.38f, -0.06f));
            mote.Life = Random.Range(3.5f, 9f);
            mote.Age = mote.Life * startProgress;
            mote.Alpha = Random.Range(0.12f, 0.34f);
            mote.Transform.localScale = Vector3.one * Random.Range(0.6f, 1.25f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _motes.Length; i++)
            {
                ref Mote mote = ref _motes[i];
                mote.Age += dt;
                if (mote.Age >= mote.Life)
                {
                    Respawn(ref mote, 0f);
                    continue;
                }
                mote.Transform.position += (Vector3)(mote.Drift * dt);
                // 나타나고 사라질 때만 부드럽게. 중간에는 일정한 농도를 유지한다.
                float t = mote.Age / mote.Life;
                float envelope = Mathf.Min(1f, Mathf.Min(t, 1f - t) * 5f);
                _motes[i].Renderer.color = new Color(0.78f, 0.8f, 0.95f, mote.Alpha * envelope);
            }

            if (_lamps == null) return;
            for (int i = 0; i < _lamps.Length; i++)
            {
                Light2D lamp = _lamps[i];
                if (lamp == null) continue;
                // 미세 깜빡임. 밝기 변화를 5% 안으로 묶어 전투 가독성을 건드리지 않는다.
                float n = Mathf.PerlinNoise(_lampSeed[i], Time.time * 2.6f);
                lamp.intensity = _lampBase[i] * (0.965f + n * 0.05f);
            }
        }
    }
}
