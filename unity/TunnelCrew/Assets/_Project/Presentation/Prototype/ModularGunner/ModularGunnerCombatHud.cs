using UnityEngine;

namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>
    /// 로드맵 4.15 — UI 전투 피드백의 코드로 가능한 부분.
    /// 피해 수치는 반응 세기에 따라 크기·색·이동 곡선이 갈리고, 처치는 별도 문구로 튄다.
    /// 화면 밖 적은 가장자리에 위험 방향 마커로 표시한다.
    /// 탄종 아이콘과 위험 마커는 승인 HUD 아이콘 세트를 쓴다.
    /// </summary>
    public sealed class ModularGunnerCombatHud : MonoBehaviour
    {
        const int PopupCapacity = 48;
        const float PopupLife = 0.72f;
        // 기준 해상도 720p 에서의 크기. 실제 화면 높이에 맞춰 배율을 건다.
        // 4K 게임 뷰에서 그대로 두면 아이콘이 화면의 1% 밖에 안 돼 읽히지 않는다.
        const float ReferenceHeight = 720f;
        const float EdgeMargin = 26f;
        const float MarkerSize = 24f;
        const float AmmoIconSize = 28f;

        struct Popup
        {
            public Vector2 World;
            public Vector2 Drift;
            public float Age;
            public string Text;
            public Color Color;
            public int FontSize;
            public bool Active;
        }

        [SerializeField] Texture2D _dangerMarker;
        [SerializeField] Texture2D _eliteMarker;
        [SerializeField] Texture2D _killMarker;
        [SerializeField] Texture2D[] _ammoIcons;

        public static ModularGunnerCombatHud Instance { get; private set; }

        public void EditorAssign(Texture2D danger, Texture2D elite, Texture2D kill, Texture2D[] ammo)
        {
            _dangerMarker = danger;
            _eliteMarker = elite;
            _killMarker = kill;
            _ammoIcons = ammo;
        }

        Popup[] _popups;
        int _next;
        GUIStyle _popupStyle;
        GUIStyle _markerStyle;
        Camera _camera;
        ModularGunnerEnemy[] _enemies = System.Array.Empty<ModularGunnerEnemy>();
        float _nextScan;
        float _uiScale = 1f;

        void Awake()
        {
            Instance = this;
            _popups = new Popup[PopupCapacity];
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Report(Vector2 world, ModularGunnerEffects.Reaction reaction)
        {
            ref Popup popup = ref _popups[_next];
            _next = (_next + 1) % PopupCapacity;

            popup.World = world + Vector2.up * 0.9f;
            popup.Age = 0f;
            popup.Active = true;
            switch (reaction)
            {
                case ModularGunnerEffects.Reaction.Death:
                    popup.Text = "KILL";
                    popup.Color = new Color(1f, 0.42f, 0.22f);
                    popup.FontSize = 22;
                    // 처치는 거의 수직으로 크게 튄다.
                    popup.Drift = new Vector2(Random.Range(-0.3f, 0.3f), 3.4f);
                    break;
                case ModularGunnerEffects.Reaction.Critical:
                    popup.Text = "2";
                    popup.Color = new Color(1f, 0.86f, 0.3f);
                    popup.FontSize = 19;
                    popup.Drift = new Vector2(Random.Range(-1.1f, 1.1f), 2.9f);
                    break;
                case ModularGunnerEffects.Reaction.Heavy:
                    popup.Text = "1";
                    popup.Color = new Color(0.98f, 0.94f, 0.82f);
                    popup.FontSize = 15;
                    popup.Drift = new Vector2(Random.Range(-1.5f, 1.5f), 2.3f);
                    break;
                default:
                    popup.Text = "1";
                    popup.Color = new Color(0.78f, 0.82f, 0.88f);
                    popup.FontSize = 12;
                    popup.Drift = new Vector2(Random.Range(-1.8f, 1.8f), 1.9f);
                    break;
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _popups.Length; i++)
            {
                if (!_popups[i].Active) continue;
                _popups[i].Age += dt;
                if (_popups[i].Age >= PopupLife) { _popups[i].Active = false; continue; }
                // 위로 튀었다가 중력에 눌리는 곡선.
                _popups[i].World += _popups[i].Drift * dt;
                _popups[i].Drift.y -= 5.2f * dt;
            }

            if (Time.time < _nextScan) return;
            _nextScan = Time.time + 0.2f;
            _enemies = FindObjectsByType<ModularGunnerEnemy>(FindObjectsSortMode.None);
        }

        void OnGUI()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            _uiScale = Mathf.Max(1f, Screen.height / ReferenceHeight);

            _popupStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _markerStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.44f, 0.24f) },
            };

            DrawDangerMarkers();
            DrawPopups();
            DrawAmmoIcon();
        }

        void DrawPopups()
        {
            for (int i = 0; i < _popups.Length; i++)
            {
                if (!_popups[i].Active) continue;
                Vector3 screen = _camera.WorldToScreenPoint(_popups[i].World);
                if (screen.z < 0f) continue;

                float t = _popups[i].Age / PopupLife;
                Color c = _popups[i].Color;
                c.a = 1f - t * t;                    // 끝에서만 빠르게 사라진다.
                _popupStyle.fontSize = Mathf.RoundToInt(_popups[i].FontSize * _uiScale);
                _popupStyle.normal.textColor = c;

                float pw = 80f * _uiScale, ph = 24f * _uiScale;
                var rect = new Rect(screen.x - pw * 0.5f, Screen.height - screen.y - ph * 0.5f, pw, ph);
                if (_popups[i].Text == "KILL" && _killMarker != null)
                {
                    // 처치는 문구 옆에 X 아이콘을 함께 띄워 멀리서도 읽힌다.
                    float ks = 20f * _uiScale;
                    var iconRect = new Rect(screen.x - pw * 0.5f - ks * 0.2f, Screen.height - screen.y - ks * 0.5f, ks, ks);
                    Color saved = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, c.a);
                    GUI.DrawTexture(iconRect, _killMarker);
                    GUI.color = saved;
                }
                GUI.Label(rect, _popups[i].Text, _popupStyle);
            }
        }

        /// <summary>현재 탄종을 좌하단에 아이콘으로 띄운다. 수치는 아직 시스템이 없다.</summary>
        void DrawAmmoIcon()
        {
            if (_ammoIcons == null || _ammoIcons.Length == 0 || _ammoIcons[0] == null) return;
            float size = AmmoIconSize * _uiScale;
            float pad = 18f * _uiScale;
            GUI.DrawTexture(new Rect(pad, Screen.height - size - pad, size, size), _ammoIcons[0]);
        }

        /// <summary>화면 밖 적을 가장자리 마커로 알린다. 방향만 주고 거리는 주지 않는다.</summary>
        void DrawDangerMarkers()
        {
            for (int i = 0; i < _enemies.Length; i++)
            {
                ModularGunnerEnemy enemy = _enemies[i];
                if (enemy == null) continue;
                Vector3 screen = _camera.WorldToScreenPoint(enemy.transform.position);
                if (screen.z < 0f) continue;
                float gx = screen.x;
                float gy = Screen.height - screen.y;
                bool outside = gx < 0f || gx > Screen.width || gy < 0f || gy > Screen.height;
                if (!outside) continue;

                float margin = EdgeMargin * _uiScale;
                float x = Mathf.Clamp(gx, margin, Screen.width - margin);
                float y = Mathf.Clamp(gy, margin, Screen.height - margin);

                bool elite = enemy.GetComponent<ModularGunnerEnemyVisuals>() is { IsElite: true };
                Texture2D icon = elite && _eliteMarker != null ? _eliteMarker : _dangerMarker;
                if (icon == null)
                {
                    GUI.Label(new Rect(x - 10f, y - 10f, 20f, 20f), "◆", _markerStyle);
                    continue;
                }

                float ms = MarkerSize * _uiScale;
                var rect = new Rect(x - ms * 0.5f, y - ms * 0.5f, ms, ms);
                if (elite)
                {
                    // 엘리트 마커는 별 모양이라 회전하지 않는다.
                    GUI.DrawTexture(rect, icon);
                    continue;
                }
                // 위험 화살표는 화면 중앙에서 적 쪽을 가리키도록 돌린다.
                float angle = Mathf.Atan2(gy - Screen.height * 0.5f, gx - Screen.width * 0.5f) * Mathf.Rad2Deg;
                Matrix4x4 saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(angle, new Vector2(x, y));
                GUI.DrawTexture(rect, icon);
                GUI.matrix = saved;
            }
        }
    }
}
