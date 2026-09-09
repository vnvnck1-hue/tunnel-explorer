using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 플레이 중에 라이팅 프리셋을 버튼/숫자키로 갈아 끼운다.
    ///
    /// <b>왜 캡처가 아니라 런타임인가</b> — 캡처 한 장은 씬을 다시 조립해야 나오고, 마음에 안
    /// 들면 전부 다시 뽑아야 한다. 런타임 전환은 누르는 즉시 보이고 조합도 자유롭다. 라이팅
    /// 지식 없이 시작할 때 "수치를 이해하고 고르기"보다 "보고 고른 뒤 수치를 읽기"가 빠르므로,
    /// 확정 버튼이 고른 프리셋의 값을 파일로 남긴다.
    ///
    /// <b>이주 1단계(2026-09-09)</b> — 접촉 그림자는 더 이상 임시 <see cref="LabShadowBlob"/> 이
    /// 아니라 정식 <see cref="ContactShadowRenderer"/> 가 그린다. 프리셋의 <c>blobStrength</c> 는
    /// <see cref="WorldVisualProfile.contactShadowOpacity"/> 로 들어간다 — 본선과 같은 배관이다.
    /// 깊이 정렬·전경 페이드·실루엣도 같은 프로파일 복제본을 본다.
    ///
    /// 이 컴포넌트는 <b>씬에 무엇이 있는지 스스로 찾는다</b> — 전역광, 나머지 광원, 대기 감독,
    /// 깊이·그림자 시스템, <see cref="LabShadowBlob"/>. 그래서 랩 씬뿐 아니라 본선 씬에 얹어도 동작한다.
    ///
    /// 조작: 숫자키 <c>1</c>~<c>9</c>·<c>0</c> 프리셋 선택 · <c>H</c> UI 토글 · <c>Enter</c> 확정 기록
    /// · <c>F1</c> 발점·전경 그룹 디버그 선.
    /// </summary>
    public sealed class LightingPresetSwitcher : MonoBehaviour
    {
        [Tooltip("버튼 순서대로 숫자키 1~9, 0 에 대응한다.")]
        [SerializeField] LightingPreset[] _presets = System.Array.Empty<LightingPreset>();

        [Tooltip("비어 있으면 씬에서 lightType == Global 인 Light2D 를 찾는다.")]
        [SerializeField] Light2D _globalLight;

        [Tooltip("비어 있으면 씬에서 찾는다. 없으면 후처리 축만 동작하지 않는다.")]
        [SerializeField] AtmosphereDirector _atmosphere;

        [Tooltip("후처리 배율의 기준. 이 자산은 수정하지 않고 런타임 복제본에만 배율을 적용한다.")]
        [SerializeField] AtmosphereProfile _baseProfile;

        [Tooltip("깊이·그림자·투영의 기준. 이 자산도 수정하지 않는다 — 복제본에만 쓴다. " +
                 "비어 있으면 ContactShadowRenderer 가 든 것을 쓴다.")]
        [SerializeField] WorldVisualProfile _worldProfile;

        [Tooltip("전역 Volume. 비면 씬에서 찾는다. 프리셋의 volumeProfile 을 런타임 복제본으로 얹는다.")]
        [SerializeField] Volume _volume;

        [Tooltip("프리셋이 volumeProfile 을 비워 두면 쓰는 기본(Volume_Stratum1_Surface). 자산은 수정하지 않는다.")]
        [SerializeField] VolumeProfile _defaultVolumeProfile;

        [SerializeField] int _startIndex;
        [SerializeField] bool _showUi = true;

        // 원본 프로파일 → 런타임 복제본. 디스크 자산의 하위 VolumeComponent 를 직접 건드리면
        // 에디터에서 자산이 dirty 되므로, 컴포넌트까지 복제한 별도 프로파일에만 값을 넣는다.
        readonly Dictionary<VolumeProfile, VolumeProfile> _volumeClones = new();
        readonly Dictionary<AtmosphereProfile, AtmosphereProfile> _atmoClones = new();
        string _volumeLabel = "—";
        string _bloomLabel = "—";

        /// <summary>소켓이 없는 광원(구 경로). 전역광과 어두운 블롭은 따로 다룬다.</summary>
        readonly List<(Light2D light, float baseIntensity)> _sceneLights = new();
        readonly List<LabShadowBlob> _blobs = new();

        // 이주 2단계 — 정식 소켓 파이프라인. LightSocketRenderer 가 매 프레임
        // light.intensity = baseIntensity × 깜빡임 으로 덮어쓰므로, 프리셋의 lightScale 은
        // Light2D 가 아니라 LightSocket.baseIntensity 를 민다. 원래 값은 여기 보관한다.
        readonly List<(LightSocket socket, float baseIntensity)> _sockets = new();
        readonly List<GameObject> _testLights = new();
        LightSocketRenderer _socketRenderer;
        VisualOptionsController _options;
        LightingLabPawn _pawn;
        LabEnvironment _labEnv;
        float _lightScale = 1f;

        // 이주 1단계에서 들어온 정식 시스템. 전부 같은 프로파일 복제본을 본다.
        ContactShadowRenderer _contact;
        FootpointSorter _sorter;
        ForegroundFadeController _fade;
        OccludedSilhouetteRenderer _silhouettes;
        VisualDebugLines _lines;
        bool _debugLines;

        AtmosphereProfile _runtimeProfile;
        WorldVisualProfile _runtimeWorld;
        int _current = -1;
        string _lastWrite;

        public LightingPreset Current =>
            (_current >= 0 && _current < _presets.Length) ? _presets[_current] : null;

        /// <summary>
        /// 빌더가 참조를 직접 넣는다(프로젝트 관례: <c>VisualLabController.EditorAssign</c>).
        /// SerializedObject 경로로 넣었을 때 ScriptableObject 참조 두 개가 null 로 직렬화된 일이
        /// 있어(2026-09-09), 다른 시스템들이 쓰는 직접 할당으로 통일했다.
        /// </summary>
        public void EditorAssign(LightingPreset[] presets, Light2D globalLight, AtmosphereDirector atmosphere,
                                 AtmosphereProfile baseProfile, WorldVisualProfile worldProfile, int startIndex,
                                 Volume volume = null, VolumeProfile defaultVolumeProfile = null)
        {
            _presets = presets ?? System.Array.Empty<LightingPreset>();
            _globalLight = globalLight;
            _atmosphere = atmosphere;
            _baseProfile = baseProfile;
            _worldProfile = worldProfile;
            _startIndex = startIndex;
            _volume = volume;
            _defaultVolumeProfile = defaultVolumeProfile;
            _showUi = true;
        }

        void Awake()
        {
            // IsometricProjection.Preset 은 정적이고 기본값이 2:1 마름모다. 본선 Run 을 돌린
            // 뒤 이 씬을 열면 그 값이 남아 있을 수 있다. VisualHeightAnchor 가 groundPosition 을
            // 이 투영으로 화면에 놓으므로, 여기서 제작 기준(ReferenceTopDown = 항등)으로 맞춘다.
            var wp = _worldProfile != null ? _worldProfile : FindAnyObjectByType<ContactShadowRenderer>()?.Profile;
            IsometricProjection.SetPreset(wp != null ? wp.authoredProjection : ProjectionPreset.ReferenceTopDown);
        }

        void Start()
        {
            Collect();
            Apply(Mathf.Clamp(_startIndex, 0, Mathf.Max(0, _presets.Length - 1)));
        }

        void Collect()
        {
            _sceneLights.Clear();
            _blobs.Clear();

            foreach (var blob in FindObjectsByType<LabShadowBlob>(FindObjectsSortMode.None))
                _blobs.Add(blob);

            _sockets.Clear();
            _socketRenderer = FindAnyObjectByType<LightSocketRenderer>();
            _options = FindAnyObjectByType<VisualOptionsController>();
            _pawn = FindAnyObjectByType<LightingLabPawn>();

            foreach (var light in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            {
                if (light.lightType == Light2D.LightType.Global)
                {
                    if (_globalLight == null) _globalLight = light;
                    continue;
                }

                // 어두운 블롭은 프리셋의 그림자 축이 따로 몬다.
                if (light.GetComponent<LabShadowBlob>() != null) continue;

                // 소켓이 있으면 정식 경로 — baseIntensity 를 민다. 없으면 구 경로.
                if (light.TryGetComponent<LightSocket>(out var socket))
                    _sockets.Add((socket, socket.baseIntensity));
                else
                    _sceneLights.Add((light, light.intensity));
            }

            if (_atmosphere == null) _atmosphere = FindAnyObjectByType<AtmosphereDirector>();
            if (_baseProfile == null && _atmosphere != null) _baseProfile = _atmosphere.Profile;

            if (_volume == null) _volume = FindAnyObjectByType<Volume>();
            if (_defaultVolumeProfile == null && _volume != null) _defaultVolumeProfile = _volume.sharedProfile;

            // ── 정식 깊이·그림자 시스템(이주 1단계). 하나의 복제본으로 묶는다 — 접촉 그림자와
            // 실루엣이 서로 다른 프로파일을 보면 같은 프리셋에서 값이 어긋난다.
            _contact = FindAnyObjectByType<ContactShadowRenderer>();
            _sorter = FindAnyObjectByType<FootpointSorter>();
            _fade = FindAnyObjectByType<ForegroundFadeController>();
            _silhouettes = FindAnyObjectByType<OccludedSilhouetteRenderer>();
            _lines = FindAnyObjectByType<VisualDebugLines>();

            if (_worldProfile == null && _contact != null) _worldProfile = _contact.Profile;
            if (_worldProfile != null)
            {
                _runtimeWorld = Instantiate(_worldProfile);
                _runtimeWorld.name = _worldProfile.name + " (runtime)";
                if (_contact != null) _contact.Profile = _runtimeWorld;
                if (_sorter != null) _sorter.Profile = _runtimeWorld;
                if (_fade != null) _fade.Profile = _runtimeWorld;
                if (_silhouettes != null) _silhouettes.Profile = _runtimeWorld;
            }
        }

        public void Apply(int index)
        {
            if (_presets == null || _presets.Length == 0) return;
            index = Mathf.Clamp(index, 0, _presets.Length - 1);
            var preset = _presets[index];
            if (preset == null) return;

            _current = index;

            if (_globalLight != null)
            {
                _globalLight.color = preset.ambientColor;
                _globalLight.intensity = preset.ambientIntensity;
            }

            _lightScale = preset.lightScale;
            for (int i = 0; i < _sceneLights.Count; i++)
            {
                var (light, baseIntensity) = _sceneLights[i];
                if (light == null) continue;
                light.intensity = baseIntensity * preset.lightScale;
            }
            for (int i = 0; i < _sockets.Count; i++)
            {
                var (socket, baseIntensity) = _sockets[i];
                if (socket == null) continue;
                socket.baseIntensity = baseIntensity * preset.lightScale;
            }

            // 접촉 그림자 — 정식 경로. 프리셋의 blobStrength 가 프로파일 opacity 가 된다.
            // ContactShadow.opacity 가 0 인 개체는 이 값을 읽는다(0 = "프로파일 값").
            bool contactOn = preset.shadowMode != LabShadowMode.None;
            if (_runtimeWorld != null) _runtimeWorld.contactShadowOpacity = preset.blobStrength;
            if (_contact != null) _contact.Enabled = contactOn;

            // 네거티브 라이팅은 아직 정식 시스템이 없다(§B-2). 임시 블롭이 계속 맡는다.
            // Contact 종류 블롭이 씬에 남아 있어도(구 씬) 정식 그림자와 겹치지 않게 끈다.
            float negative = preset.shadowMode == LabShadowMode.BlobAndNegative ? preset.negativeStrength : 0f;
            for (int i = 0; i < _blobs.Count; i++)
            {
                var blob = _blobs[i];
                if (blob == null) continue;
                blob.ApplyStrength(blob.BlobKind == LabShadowBlob.Kind.Negative ? negative : 0f);
            }

            ApplyPost(preset);
            ApplyVolume(preset);
        }

        /// <summary>
        /// 대기 프로파일의 <b>세기만</b> 배율한다. 색(안개·근/원 틴트·비네트)은 프로파일이 정한다 —
        /// 프리셋이 다른 지층 프로파일을 고르면 그 색이 곧 팔레트다(Rain World 식).
        /// </summary>
        void ApplyPost(LightingPreset preset)
        {
            if (_atmosphere == null) return;
            var source = preset.atmosphereProfile != null ? preset.atmosphereProfile : _baseProfile;
            if (source == null) return;

            // 디스크의 프로파일 자산을 절대 수정하지 않는다 — 원본마다 복제본 하나.
            if (!_atmoClones.TryGetValue(source, out var clone) || clone == null)
            {
                clone = Instantiate(source);
                clone.name = source.name + " (runtime)";
                clone.animateGrain = false;
                _atmoClones[source] = clone;
            }
            _runtimeProfile = clone;

            float scale = preset.postScale;
            clone.fogDensity = Mathf.Clamp01(source.fogDensity * scale);
            clone.depthSeparation = Mathf.Clamp(source.depthSeparation * scale, 0f, 0.5f);
            clone.vignetteStrength = Mathf.Clamp01(source.vignetteStrength * scale);
            clone.grainStrength = Mathf.Clamp(source.grainStrength * scale, 0f, 0.2f);

            // Profile 세터가 Apply() 를 부른다 — 같은 인스턴스를 다시 넣어도 값이 반영된다.
            _atmosphere.Profile = clone;
        }

        /// <summary>
        /// URP Volume — 본편 <c>RunBootstrap.BuildVolume</c> 과 같은 지층 프로파일을 얹고, 프리셋의
        /// 블룸 오버라이드가 있으면 복제본의 <see cref="Bloom"/> 에만 넣는다.
        ///
        /// <b>왜 컴포넌트까지 복제하는가</b> — <c>Instantiate(profile)</c> 는 하위 VolumeComponent 참조를
        /// 그대로 공유한다. 거기에 값을 쓰면 디스크 자산의 컴포넌트가 바뀐다(에디터에서 dirty).
        /// </summary>
        void ApplyVolume(LightingPreset preset)
        {
            if (_volume == null) { _volumeLabel = "Volume 없음"; return; }
            var source = preset.volumeProfile != null ? preset.volumeProfile : _defaultVolumeProfile;
            if (source == null) { _volumeLabel = "프로파일 없음"; return; }

            if (!_volumeClones.TryGetValue(source, out var clone) || clone == null)
            {
                clone = ScriptableObject.CreateInstance<VolumeProfile>();
                clone.name = source.name + " (runtime)";
                foreach (var c in source.components)
                {
                    if (c == null) continue;
                    var cc = Instantiate(c);
                    cc.name = c.name;
                    clone.components.Add(cc);
                }
                _volumeClones[source] = clone;
            }

            if (clone.TryGet(out Bloom bloom))
            {
                // 오버라이드가 없으면 원본 값으로 되돌린다 — 다른 프리셋이 남긴 값이 새지 않게.
                if (source.TryGet(out Bloom srcBloom))
                {
                    bloom.intensity.value = preset.bloomIntensity >= 0f ? preset.bloomIntensity : srcBloom.intensity.value;
                    bloom.threshold.value = preset.bloomThreshold >= 0f ? preset.bloomThreshold : srcBloom.threshold.value;
                    bloom.intensity.overrideState = true;
                    bloom.threshold.overrideState = true;
                }
                _bloomLabel = $"{bloom.intensity.value:0.00} / thr {bloom.threshold.value:0.00}";
            }
            else _bloomLabel = "Bloom 없음";

            _volume.sharedProfile = clone;
            _volume.isGlobal = true;
            _volumeLabel = source.name;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.hKey.wasPressedThisFrame) _showUi = !_showUi;
            if (kb.f1Key.wasPressedThisFrame) _debugLines = !_debugLines;
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) WriteChoice();

            // 2단계 검증용 — 예산 초과까지 작업등을 늘려도 그림자 개수가 지켜지는지 눈으로 본다.
            if (kb.lKey.wasPressedThisFrame) SpawnTestWorklamp();
            if (kb.kKey.wasPressedThisFrame) RemoveTestWorklamp();
            if (kb.tKey.wasPressedThisFrame && _options != null) _options.CycleTier();

            if (kb.digit1Key.wasPressedThisFrame) Apply(0);
            else if (kb.digit2Key.wasPressedThisFrame) Apply(1);
            else if (kb.digit3Key.wasPressedThisFrame) Apply(2);
            else if (kb.digit4Key.wasPressedThisFrame) Apply(3);
            else if (kb.digit5Key.wasPressedThisFrame) Apply(4);
            else if (kb.digit6Key.wasPressedThisFrame) Apply(5);
            else if (kb.digit7Key.wasPressedThisFrame) Apply(6);
            else if (kb.digit8Key.wasPressedThisFrame) Apply(7);
            else if (kb.digit9Key.wasPressedThisFrame) Apply(8);
            else if (kb.digit0Key.wasPressedThisFrame) Apply(9);
        }

        /// <summary>
        /// 폰 발밑에 임시 작업등 소켓을 하나 더 세운다. 그림자 예산(§13)은 전역이라 광원이 늘면
        /// 우선순위 낮은 것부터 그림자가 꺼져야 한다 — 그것을 확인하는 손잡이다.
        /// </summary>
        void SpawnTestWorklamp()
        {
            var at = _pawn != null ? (Vector2)_pawn.transform.position : Vector2.zero;
            var go = new GameObject($"Test Worklamp {_testLights.Count + 1}");
            go.transform.position = new Vector3(at.x + 0.6f, at.y + 0.4f, -0.1f);

            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(1f, 0.62f, 0.28f, 1f);
            light.pointLightInnerRadius = 0.15f;
            light.pointLightOuterRadius = 2.2f;
            light.falloffIntensity = 0.5f;
            light.targetSortingLayers = VisualLayers.LitLayerIds();

            var socket = go.AddComponent<LightSocket>();
            socket.lightClass = LightClass.Worklamp;
            socket.rangeCells = 2.2f;
            socket.phase = 0.17f * (_testLights.Count + 1);
            const float baseIntensity = 2.0f;
            socket.baseIntensity = baseIntensity * _lightScale;

            _sockets.Add((socket, baseIntensity));
            _testLights.Add(go);
        }

        void RemoveTestWorklamp()
        {
            if (_testLights.Count == 0) return;
            var go = _testLights[_testLights.Count - 1];
            _testLights.RemoveAt(_testLights.Count - 1);
            if (go != null && go.TryGetComponent<LightSocket>(out var socket))
                _sockets.RemoveAll(e => e.socket == socket);
            if (go != null) Destroy(go);
        }

        void LateUpdate()
        {
            if (_lines == null) return;
            _lines.Visible = _debugLines;
            if (!_debugLines) return;

            // 폰이 매 프레임 움직이므로 상태 변화가 아니라 프레임마다 다시 그린다 — 랩이라 허용한다.
            _lines.Begin();
            _lines.DrawFootpoints(new Color(0.3f, 1f, 0.5f), new Color(1f, 0.85f, 0.3f));
            _lines.DrawForegroundGroups(new Color(1f, 0.4f, 0.3f), new Color(0.5f, 0.6f, 1f));
            _lines.End();
        }

        /// <summary>
        /// 고른 프리셋의 값을 프로젝트 루트에 남긴다. 화면을 보고 고른 결과를 사람이 다시
        /// 옮겨 적지 않아도 되도록 하는 것이 목적이다.
        /// </summary>
        public void WriteChoice()
        {
            var preset = Current;
            if (preset == null) return;

            var c = preset.ambientColor;
            var inv = CultureInfo.InvariantCulture;
            string json =
                "{\n" +
                $"  \"label\": \"{preset.label}\",\n" +
                $"  \"asset\": \"{preset.name}\",\n" +
                $"  \"ambientColor\": [{c.r.ToString("0.###", inv)}, {c.g.ToString("0.###", inv)}, {c.b.ToString("0.###", inv)}],\n" +
                $"  \"ambientColorHex\": \"#{ColorUtility.ToHtmlStringRGB(c)}\",\n" +
                $"  \"ambientIntensity\": {preset.ambientIntensity.ToString("0.###", inv)},\n" +
                $"  \"lightScale\": {preset.lightScale.ToString("0.###", inv)},\n" +
                $"  \"shadowMode\": \"{preset.shadowMode}\",\n" +
                $"  \"blobStrength\": {preset.blobStrength.ToString("0.###", inv)},\n" +
                $"  \"negativeStrength\": {preset.negativeStrength.ToString("0.###", inv)},\n" +
                $"  \"postScale\": {preset.postScale.ToString("0.###", inv)},\n" +
                $"  \"baseProfile\": \"{(_baseProfile != null ? _baseProfile.name : "(none)")}\",\n" +
                $"  \"worldProfile\": \"{(_worldProfile != null ? _worldProfile.name : "(none)")}\",\n" +
                $"  \"note\": \"{preset.note.Replace("\"", "'").Replace("\n", " ")}\"\n" +
                "}\n";

            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "LightingPresetChoice.json"));
                File.WriteAllText(path, json);
                _lastWrite = path;
                Debug.Log($"[라이팅] 프리셋 확정 기록: {preset.label}\n{path}\n{json}");
            }
            catch (System.Exception e)
            {
                _lastWrite = "기록 실패: " + e.Message;
                Debug.LogWarning($"[라이팅] 프리셋 기록 실패: {e.Message}");
            }
        }

        // ───────────────────────────── UI

        GUIStyle _wrap;

        void OnGUI()
        {
            if (!_showUi || _presets == null || _presets.Length == 0) return;

            _wrap ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 11 };

            const float w = 340f;
            GUILayout.BeginArea(new Rect(12f, 12f, w, Screen.height - 24f), GUI.skin.box);
            GUILayout.Label("<b>라이팅 프리셋</b>  ·  H 숨기기  ·  F1 디버그 선", new GUIStyle(GUI.skin.label) { richText = true });

            // 프로젝트 입력 설정이 PointersAndKeyboardsRespectGameViewFocus 라서, Game 뷰에
            // 포커스가 없으면 숫자키가 이 컴포넌트까지 오지 않는다(PlayUnfocused 라 화면은
            // 계속 그려지므로 "그림은 나오는데 키가 안 먹는" 것으로 보인다). 버튼 클릭은
            // 그 클릭 자체가 포커스를 주므로 항상 동작한다.
            GUILayout.Label("숫자키가 안 먹으면 Game 뷰를 한 번 클릭할 것 (버튼은 항상 동작).", _wrap);
            GUILayout.Space(4f);

            for (int i = 0; i < _presets.Length; i++)
            {
                var preset = _presets[i];
                if (preset == null) continue;

                string key = i < 9 ? (i + 1).ToString() : (i == 9 ? "0" : "-");
                bool selected = i == _current;
                string text = selected ? $"▶ [{key}] {preset.label}" : $"   [{key}] {preset.label}";

                var prev = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = new Color(0.62f, 0.50f, 0.76f);
                if (GUILayout.Button(text, GUILayout.Height(26f))) Apply(i);
                GUI.backgroundColor = prev;
            }

            var current = Current;
            if (current != null)
            {
                GUILayout.Space(8f);
                GUILayout.Label("── 현재 값 ──");
                GUILayout.Label(
                    $"전역광 세기  {current.ambientIntensity:0.00}\n" +
                    $"앰비언트 색  #{ColorUtility.ToHtmlStringRGB(current.ambientColor)}\n" +
                    $"광원 배율    ×{current.lightScale:0.00}\n" +
                    $"그림자       {ShadowLabel(current.shadowMode)}\n" +
                    $"  접촉 opacity {current.blobStrength:0.00} / 암부 {current.negativeStrength:0.00}\n" +
                    $"후처리 배율  ×{current.postScale:0.0}\n" +
                    $"대기 팔레트  {(_runtimeProfile != null ? _runtimeProfile.name.Replace(" (runtime)", "") : "—")}\n" +
                    $"Volume       {_volumeLabel}\n" +
                    $"블룸         {_bloomLabel}", _wrap);

                if (!string.IsNullOrEmpty(current.note))
                {
                    GUILayout.Space(4f);
                    GUILayout.Label(current.note, _wrap);
                }

                GUILayout.Space(8f);
                if (GUILayout.Button("이 프리셋으로 확정 (Enter)", GUILayout.Height(26f))) WriteChoice();
                if (!string.IsNullOrEmpty(_lastWrite))
                    GUILayout.Label("기록: " + _lastWrite, _wrap);
            }

            GUILayout.Space(8f);
            GUILayout.Label(
                $"정식 시스템  접촉그림자 {(_contact != null ? _contact.ActiveCount.ToString() : "—")}" +
                $" · 실루엣 {(_silhouettes != null ? _silhouettes.ActiveCount.ToString() : "—")}" +
                $" · 투영 {IsometricProjection.Preset}", _wrap);

            if (_socketRenderer != null)
            {
                int budget = LightClassRules.ShadowBudget(_socketRenderer.Tier);
                GUILayout.Label(
                    $"광원 소켓 {LightSocketRenderer.SocketCount} · 그림자 {_socketRenderer.ShadowCount}/{budget}" +
                    $" · 품질 {_socketRenderer.Tier}  (L 작업등 추가 · K 제거 · T 품질 단계)", _wrap);
            }
            else
                GUILayout.Label("⚠ LightSocketRenderer 가 없다 — 광원 축이 구 경로로 동작한다.", _wrap);

            _labEnv ??= FindAnyObjectByType<LabEnvironment>();
            if (_labEnv != null && _labEnv.Field != null)
                GUILayout.Label(
                    $"환경 렌더러 {_labEnv.Cols}×{_labEnv.Rows} 셀 · 벽 윤곽 캐스터 {_labEnv.CasterCount}" +
                    $" · 파괴 편집 {_labEnv.Edits}  (X 앞 칸 파괴 · C 복구)", _wrap);

            if (_globalLight == null)
                GUILayout.Label("⚠ 전역광(Light2D Global)을 찾지 못했다 — 어둠 축이 동작하지 않는다.", _wrap);
            if (_atmosphere == null)
                GUILayout.Label("⚠ AtmosphereDirector 가 없다 — 후처리 축이 동작하지 않는다.", _wrap);
            if (_contact == null)
                GUILayout.Label("⚠ ContactShadowRenderer 가 없다 — 접촉 그림자 축이 동작하지 않는다.", _wrap);

            GUILayout.EndArea();
        }

        static string ShadowLabel(LabShadowMode mode) => mode switch
        {
            LabShadowMode.None => "없음",
            LabShadowMode.Blob => "접촉 그림자",
            _ => "접촉 그림자 + 암부",
        };
    }
}
