using System.Collections.Generic;
using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 2D 본선의 <see cref="ParticleSystem"/> 연출을 원근 월드에 옮긴다
    /// (3d-perspective-production-plan §4 5단계 "폭발·연기·파편").
    ///
    /// 파티클 시스템 자체를 3D 로 다시 만들지 않는다 — 방출 규칙·수명·색·크기는 여전히
    /// <c>FxSystem</c> 이 단일 출처이고, 여기서는 살아 있는 파티클의 위치·크기·색만 읽어
    /// 카메라를 마주보는 쿼드로 다시 그린다. 원근 월드를 꺼도 2D 연출은 그대로다.
    ///
    /// 좌표: 파티클은 렌더 XY 평면에서 논다. 그 평면을 바닥 평면으로 눕히고
    /// <see cref="Height"/> 만큼 띄워 캐릭터 몸높이에서 터지게 한다.
    /// </summary>
    public sealed class PerspectiveParticleMirror : MonoBehaviour
    {
        /// <summary>파티클을 띄우는 높이(셀). 0 이면 바닥에 눕는다.</summary>
        public float Height = 0.45f;
        /// <summary>구도 중심(시뮬 좌표). 이 주변만 그린다.</summary>
        public Vector2 Focus { get; set; }
        /// <summary>초점에서 이 거리 밖의 시스템은 건너뛴다(셀).</summary>
        public float Range = 26f;

        const float RescanInterval = 0.5f;
        const int MaxParticlesPerSystem = 256;

        int _layer = 0;
        float _nextScan;

        readonly List<ParticleSystem> _sources = new List<ParticleSystem>(32);
        readonly Dictionary<ParticleSystem, Entry> _entries = new Dictionary<ParticleSystem, Entry>();
        readonly Dictionary<Texture, Material> _materials = new Dictionary<Texture, Material>();
        ParticleSystem.Particle[] _buffer = new ParticleSystem.Particle[MaxParticlesPerSystem];
        // 재스캔 임시 컬렉션 — 0.5초마다 새로 만들면 주기적 GC 가 된다(§4 7단계).
        readonly HashSet<ParticleSystem> _live = new HashSet<ParticleSystem>();
        readonly List<ParticleSystem> _stale = new List<ParticleSystem>();

        readonly List<Vector3> _vertices = new List<Vector3>(1024);
        readonly List<Vector2> _uv = new List<Vector2>(1024);
        readonly List<Color32> _colors = new List<Color32>(1024);
        readonly List<int> _triangles = new List<int>(1536);

        sealed class Entry
        {
            public GameObject Go;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Mesh Mesh;
        }

        public void Bind(Transform parent, int layer)
        {
            _layer = layer;
            transform.SetParent(parent, false);
        }

        /// <summary>구도 중심과 카메라 방향을 받아 이번 프레임의 파티클을 다시 만든다.</summary>
        public void Sync(Vector2 focus, Quaternion cameraRotation)
        {
            Focus = focus;
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + RescanInterval;
                Rescan();
            }

            var right = cameraRotation * Vector3.right;
            var up = cameraRotation * Vector3.up;

            for (int i = 0; i < _sources.Count; i++)
            {
                var system = _sources[i];
                if (system == null) continue;
                if (!_entries.TryGetValue(system, out var entry)) continue;

                if (!system.gameObject.activeInHierarchy || system.particleCount == 0)
                {
                    entry.Renderer.enabled = false;
                    continue;
                }

                int count = system.GetParticles(_buffer);
                if (count == 0)
                {
                    entry.Renderer.enabled = false;
                    continue;
                }

                BuildMesh(system, count, right, up, out bool any);
                if (!any)
                {
                    entry.Renderer.enabled = false;
                    continue;
                }

                entry.Mesh.Clear();
                entry.Mesh.SetVertices(_vertices);
                entry.Mesh.SetUVs(0, _uv);
                entry.Mesh.SetColors(_colors);
                entry.Mesh.SetTriangles(_triangles, 0);
                entry.Mesh.RecalculateBounds();
                entry.Renderer.enabled = true;
            }
        }

        void BuildMesh(ParticleSystem system, int count, Vector3 right, Vector3 up, out bool any)
        {
            _vertices.Clear();
            _uv.Clear();
            _colors.Clear();
            _triangles.Clear();
            any = false;

            bool local = system.main.simulationSpace == ParticleSystemSimulationSpace.Local;
            var toWorld = system.transform.localToWorldMatrix;

            for (int i = 0; i < count; i++)
            {
                var particle = _buffer[i];
                var render = local ? toWorld.MultiplyPoint3x4(particle.position) : particle.position;
                var sim = IsometricProjection.ToWorld(new Vector2(render.x, render.y));
                if (Mathf.Abs(sim.x - Focus.x) > Range || Mathf.Abs(sim.y - Focus.y) > Range) continue;

                float size = particle.GetCurrentSize(system) * 0.5f;
                if (size <= 0.0005f) continue;

                var center = new Vector3(sim.x, Height, sim.y);
                var a = center - right * size - up * size;
                var b = center + right * size - up * size;
                var c = center + right * size + up * size;
                var d = center - right * size + up * size;

                int start = _vertices.Count;
                _vertices.Add(a); _vertices.Add(b); _vertices.Add(c); _vertices.Add(d);
                _uv.Add(Vector2.zero); _uv.Add(Vector2.right); _uv.Add(Vector2.one); _uv.Add(Vector2.up);

                var color = (Color32)particle.GetCurrentColor(system);
                _colors.Add(color); _colors.Add(color); _colors.Add(color); _colors.Add(color);

                _triangles.Add(start); _triangles.Add(start + 2); _triangles.Add(start + 1);
                _triangles.Add(start); _triangles.Add(start + 3); _triangles.Add(start + 2);
                any = true;
            }
        }

        void Rescan()
        {
            var found = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _sources.Clear();
            var live = _live;
            live.Clear();

            foreach (var system in found)
            {
                if (system.transform.IsChildOf(transform)) continue;
                _sources.Add(system);
                live.Add(system);
                if (_entries.ContainsKey(system)) continue;

                var go = new GameObject($"Particles · {system.name}");
                go.transform.SetParent(transform, false);
                go.layer = _layer;
                var entry = new Entry
                {
                    Go = go,
                    Filter = go.AddComponent<MeshFilter>(),
                    Renderer = go.AddComponent<MeshRenderer>(),
                    Mesh = new Mesh { name = $"Particles · {system.name}" },
                };
                entry.Mesh.MarkDynamic();
                entry.Filter.sharedMesh = entry.Mesh;
                entry.Renderer.sharedMaterial = MaterialFor(system);
                entry.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                entry.Renderer.receiveShadows = false;
                entry.Renderer.enabled = false;
                _entries.Add(system, entry);
            }

            var stale = _stale;
            stale.Clear();
            foreach (var pair in _entries) if (pair.Key == null || !live.Contains(pair.Key)) stale.Add(pair.Key);
            foreach (var key in stale)
            {
                if (_entries.TryGetValue(key, out var entry))
                {
                    if (entry.Go != null) Destroy(entry.Go);
                    if (entry.Mesh != null) Destroy(entry.Mesh);
                }
                _entries.Remove(key);
            }
        }

        /// <summary>파티클 재질. 원본의 텍스처를 그대로 쓰고 조명은 받지 않는다(연출광은 스스로 낸다).</summary>
        Material MaterialFor(ParticleSystem system)
        {
            var source = system.GetComponent<ParticleSystemRenderer>();
            Texture texture = null;
            if (source != null && source.sharedMaterial != null)
            {
                var m = source.sharedMaterial;
                texture = m.mainTexture;
                if (texture == null && m.HasProperty("_BaseMap")) texture = m.GetTexture("_BaseMap");
                if (texture == null && m.HasProperty("_MainTex")) texture = m.GetTexture("_MainTex");
            }
            // 텍스처가 없으면 흰 사각형이 그대로 보인다 — 부드러운 점으로 대체해야 2D 의 인상이 유지된다.
            var key = texture != null ? texture : SoftDot();

            if (_materials.TryGetValue(key, out var material) && material != null) return material;

            // 파티클 색·알파는 정점 색으로 들어온다 — 그것을 곱하는 오버레이 셰이더를 쓴다.
            var shader = Shader.Find("Tunnel Crew/PerspectiveOverlay")
                ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            material = new Material(shader) { name = $"3D particles · {key.name}" };
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", key);
            material.mainTexture = key;
            // 원본이 가산 합성이면 그대로 따라간다 — 불꽃·폭발이 화면을 태우는 느낌이 유지된다.
            if (source != null && source.sharedMaterial != null
                && source.sharedMaterial.HasProperty("_DstBlend")
                && Mathf.Approximately(source.sharedMaterial.GetFloat("_DstBlend"), (float)UnityEngine.Rendering.BlendMode.One))
            {
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            }
            material.renderQueue = 3100;
            _materials[key] = material;
            return material;
        }

        static Texture2D _softDot;

        /// <summary>텍스처 없는 파티클용 부드러운 점. 2D 기본 파티클 재질의 인상을 흉내낸다.</summary>
        static Texture2D SoftDot()
        {
            if (_softDot != null) return _softDot;
            const int size = 32;
            _softDot = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius, dy = y + 0.5f - radius;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                    _softDot.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - Mathf.SmoothStep(0.35f, 1f, d))));
                }
            _softDot.Apply();
            _softDot.hideFlags = HideFlags.HideAndDontSave;
            return _softDot;
        }

        void OnDestroy()
        {
            foreach (var pair in _entries)
            {
                if (pair.Value.Go != null) Destroy(pair.Value.Go);
                if (pair.Value.Mesh != null) Destroy(pair.Value.Mesh);
            }
            _entries.Clear();
            foreach (var material in _materials.Values) if (material != null) Destroy(material);
            _materials.Clear();
        }
    }
}
