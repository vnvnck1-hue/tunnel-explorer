using System;
using System.Collections.Generic;
using TunnelCrew.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using GUI = TunnelCrew.Presentation.CRT.CrtGui;
using SimInput = TunnelCrew.Sim.PlayerInput;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 관전 모드 — 원본 <c>ai/observer.js</c> 포팅. 4직업 완전 자동: 선택한 직업이 AI 리더(<see cref="ObserverPilot"/>), 나머지 3직업이 AI 크루.
    ///
    /// 조작(원본 v7.8.1 규약): Tab 시점 순환 · Esc = 지금 보는 캐릭터 조종(리더면 관전 해제, AI 크루면 그 크루와 위치·직업 교대 후 해제)
    /// · F9/Tab = 관전 복귀(전원 AI) · F8 = 런 중 토글. 관전 중 실제 게임 입력은 막힌다.
    /// 진행 자동화(0.3초 틱): 특성 카드 1.5초 뒤 무작위 선택 · 심층 보상 2.1초 뒤 선택 → 3.9초 뒤 하강 · 결과 4.5초 뒤 같은 편성으로 재출격.
    /// 카메라: 리더 시점은 본편 카메라 그대로, 크루 시점은 <see cref="CameraRig.FollowOverride"/> 로 k=dt·5 추종.
    /// </summary>
    public sealed class ObserverMode : MonoBehaviour, CRT.ICrtScreen
    {
        RunBootstrap _run;
        ObserverPilot _pilot;
        TunnelSim _pilotSim;
        WorldGrid _pilotWorld;

        public bool Active { get; private set; }
        /// <summary>한 번이라도 관전을 켠 런 — 그 뒤로는 Tab/F9 가 관전 복귀로 동작한다 (관전 미사용 런에선 Tab 통과).</summary>
        public bool EverUsed { get; private set; }
        /// <summary>0 = 리더 · 1..n = AI 크루.</summary>
        public int Focus { get; private set; }

        Vector2? _cam;
        float _tick, _tLevel, _tRest, _tResult, _idle;
        Texture2D _white; GUIStyle _badge; float _k = 1;

        TunnelSim Sim => _run != null ? _run.Sim : null;
        bool Playing => Sim != null && Sim.World != null && _run.RunActive && Sim.Phase == GamePhase.Playing;

        void Awake()
        {
            CRT.CrtSurface.Register(this, 20, true);
            _run = GetComponent<RunBootstrap>();
            _white = Texture2D.whiteTexture;
        }

        ObserverPilot Pilot()
        {
            if (_pilot == null || _pilotSim != Sim) { _pilot = new ObserverPilot(Sim); _pilotSim = Sim; _pilotWorld = null; }
            if (_pilotWorld != Sim.World) { _pilotWorld = Sim.World; _pilot.Reset(); }   // 층이 바뀌면 봉인·목표 초기화 (원본 resetBrain)
            return _pilot;
        }

        // ───────────────────────────── 시작 / 해제 (원본 OBS.enter / resume / exit)

        /// <summary>메뉴·직업 선택에서 — 리더 직업을 뺀 나머지 3직업을 AI 크루로 편성하고 관전을 켠다. 출격은 호출자(MetaScreens.Launch)가 한다.</summary>
        public void Arm(RoleId leader)
        {
            var crew = Sim.Crew;
            crew.Clear();
            foreach (RoleId r in Enum.GetValues(typeof(RoleId))) if (r != leader) crew.Add(r);
            crew.Enabled = true;
            Active = true; EverUsed = true; Focus = 0; _cam = null;
            _tLevel = _tRest = _tResult = _idle = 0;
            _pilot = null;
            Say("관전 모드 — Tab 시점 전환 · Esc 해제 · F8 재개");
        }

        /// <summary>런 도중 재개 — 편성을 건드리지 않고 오토파일럿만 켠다 (F8/F9/Tab).</summary>
        public void Resume()
        {
            Active = true; EverUsed = true; Focus = 0; _cam = null;
            if (Sim != null) Sim.Crew.Enabled = true;   // 관전 = 전원 AI — 꺼져 있던 크루도 되살린다
            if (Sim != null && Sim.World != null) Pilot().Reset();
            Say("관전 모드 재개 — Tab 시점 전환 · Esc 해제");
        }

        public void Exit(string why = null)
        {
            if (!Active) return;
            Active = false; _cam = null; Focus = 0;
            Say("관전 모드 해제" + (string.IsNullOrEmpty(why) ? "" : " (" + why + ")") + " — 직접 조작 · Tab/F9 관전 복귀");
        }

        void Say(string s) { _run?.Log(s); }

        // ───────────────────────────── 프레임 훅 1 — 리더 오토파일럿 (RunBootstrap.ReadInput 직후)
        public SimInput Drive(SimInput real, float dt)
        {
            if (!Active || !Playing) return real;
            return Pilot().Build(dt);   // 실제 입력은 완전히 대체된다
        }

        /// <summary>RunBootstrap 이 Esc 를 일시정지로 넘기기 전에 묻는다. 관전 중 Esc = 이 캐릭터 조종.</summary>
        public bool HandleEscape()
        {
            if (!Active || Sim == null || !_run.RunActive) return false;
            if (Focus == 0) Exit("리더 직접 조종 · Tab/F9 관전 복귀");
            else Swap(Focus);
            return true;
        }

        /// <summary>Esc 교대 — 보고 있던 크루와 몸(위치·직업)을 통째로 교환하고 관전을 해제한다.
        /// 그 직업을 골라 시작한 것처럼 무기·스킬·HUD 전부가 본편 시스템으로 맞춰진다 (원본 enterPossess v7.8.1).</summary>
        void Swap(int focusIdx)
        {
            var crew = Sim.Crew;
            if (focusIdx - 1 >= crew.Members.Count) return;
            var m = crew.Members[focusIdx - 1];
            if (m.Down) { Say("다운된 크루는 조종할 수 없습니다 — 구조를 기다리세요"); return; }
            if (Sim.Player.Downed) { Say("리더가 기절 상태라 교대할 수 없습니다"); return; }
            var p = Sim.Player;
            RoleId newRole = m.Role, oldRole = Sim.Build.Role;
            // 위치 교환 — 카메라가 보던 자리에 그대로 리더가 들어간다
            var px = p.Position; double pa = p.Aim;
            p.Position = m.Position; p.Aim = m.Aim; p.Velocity = Vec2.Zero; p.DashActive = false; p.DashTimeLeft = 0;
            m.Position = px; m.Aim = pa; m.Velocity = Vec2.Zero;
            // 직업 교환 — 크루는 리더의 직업으로, 플레이어는 크루의 직업으로
            crew.SetRole(m, oldRole);
            Sim.SwitchRoleMidRun(newRole);
            _run.ApplyRoleSwap(newRole);
            Pilot().Reset();
            Exit(AiCrewSystem.NameOf(newRole) + " 교대 — 직접 플레이");
        }

        // ───────────────────────────── 단축키 · 카메라 · 진행 자동화
        void Update()
        {
            if (CRT.CRTDisplayController.Instance != null && CRT.CRTDisplayController.Instance.ConsumesInput) return;
            if (Sim == null || Sim.World == null) return;
            var kb = Keyboard.current;
            bool inRun = _run.RunActive;
            if (kb != null && inRun && !(_run.Team != null && _run.Team.ChatOpen))
            {
                // F8 — 런 도중 관전 토글 (편성은 그대로)
                if (kb.f8Key.wasPressedThisFrame)
                {
                    if (Active) Exit(); else if (Sim.Phase == GamePhase.Playing) Resume();
                }
                // F9 또는 Tab — 직접 조종 중이면 관전 모드로 복귀 (전원 AI 대체). 관전 미사용 런에선 Tab 통과(카드 다시 뽑기)
                else if ((kb.f9Key.wasPressedThisFrame || (kb.tabKey.wasPressedThisFrame && !Sim.Traits.HasOffer)) && !Active && EverUsed && Sim.Phase == GamePhase.Playing) Resume();
                else if (Active && kb.tabKey.wasPressedThisFrame) CycleFocus();
            }

            // 카메라 — 리더 시점은 본편 카메라, 크루 시점은 추종 오버라이드
            var rig = _run.Rig;
            if (rig != null) rig.FollowOverride = Active && Focus > 0 && inRun ? FollowPoint : (Func<Vector2?>)null;

            // 진행 자동화 — 0.3초 틱 (원본 autoUI)
            _tick += Time.unscaledDeltaTime;
            if (_tick >= .3f) { _tick -= .3f; AutoUI(); }
        }

        Vector2? FollowPoint()
        {
            var list = CamTargets();
            if (Focus >= list.Count) { Focus = 0; return null; }
            if (Focus == 0) { _cam = null; return null; }
            var t = list[Focus].at;
            if (_cam == null) _cam = new Vector2(_run.Rig.transform.position.x, _run.Rig.transform.position.y);
            float k = Mathf.Min(1f, Time.deltaTime * 5f);
            _cam = _cam.Value + (t - _cam.Value) * k;
            return _cam;
        }

        List<(Vector2 at, string label)> CamTargets()
        {
            var list = new List<(Vector2, string)> { (V(Sim.Player.Position), AiCrewSystem.NameOf(Sim.Build.Role) + " (리더)") };
            foreach (var m in Sim.Crew.Members) list.Add((V(m.Position), "AI " + AiCrewSystem.NameOf(m.Role) + " Lv" + m.Level));
            return list;
        }
        static Vector2 V(Vec2 v) => IsometricProjection.ToRender(v);

        void CycleFocus()
        {
            var list = CamTargets();
            Focus = (Focus + 1) % list.Count;
            if (Focus == 0) _cam = null;
            Say("관전 시점 · " + list[Focus].label);
            AudioDirector.Instance?.Tick();
        }

        void AutoUI()
        {
            if (!Active) return;
            // 메인 메뉴로 나가면 관전을 스스로 푼다 — 다음 수동 런을 오염시키지 않는다
            if (!_run.RunActive) { _idle += .3f; if (_idle > 1.5f) Exit("메뉴 복귀"); return; }
            _idle = 0;
            var sim = Sim;
            // 레벨업 특성 카드 · 심층 보상(전설) — 잠깐 보여주고 자동 선택 (다시 뽑기는 쓰지 않는다)
            if (sim.Traits.HasOffer)
            {
                _tLevel += .3f;
                float wait = sim.Traits.OfferIsLegend ? 2.1f : 1.5f;
                if (_tLevel >= wait)
                {
                    _tLevel = -1.2f;
                    int n = sim.Traits.Offer.Length;
                    if (n > 0 && sim.PickTrait(UnityEngine.Random.Range(0, n))) Say(sim.Traits.OfferIsLegend ? "관전 · 심층 보상 자동 선택" : "관전 · 특성 자동 선택");
                }
            }
            else _tLevel = 0;
            // 보스 격파 휴식 — 보상을 고른 뒤 다음 지층으로 내려간다
            if (sim.Phase == GamePhase.Rest)
            {
                _tRest += .3f;
                if (sim.RestChosen && _tRest >= 3.9f) { _tRest = 0; _run.DoDescend(); Pilot().Reset(); Say("관전 · 다음 지층으로"); }
            }
            else _tRest = 0;
            // 런 종료(전멸·생환) — 결과를 보여주고 자동 재출격. 편성은 유지된다.
            if (sim.Phase == GamePhase.Result)
            {
                _tResult += .3f;
                if (_tResult >= 4.5f) { _tResult = 0; Say("관전 · 자동 재출격"); _run.SwitchRole(_run.CurrentRole); Pilot().Reset(); Focus = 0; _cam = null; }
            }
            else _tResult = 0;
        }

        // ───────────────────────────── 배지 (원본 #obsBadge — 상단 중앙 알약)
        public void DrawCrt()
        {
            if (!Active || Sim == null || Sim.World == null || !_run.RunActive || _run.CinematicActive) return;
            _k = Screen.height / 1080f;
            if (_badge == null) _badge = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, fontStyle = FontStyle.Bold };
            _badge.fontSize = Mathf.RoundToInt(15 * _k);
            var list = CamTargets();
            var cur = list[Mathf.Min(Focus, list.Count - 1)];
            string text = $"<color=#7febd0>●</color>  관전 모드 · 시점: {cur.label}   <color=#7ea99f>Tab 전환 · Esc 이 캐릭터 조종 · F8 해제 · 리더: {(_pilot != null ? _pilot.GoalLabel : "-")}</color>";
            float w = 980 * _k, h = 40 * _k, x = (Screen.width - w) * .5f, y = 108 * _k;
            GUI.color = new Color(.03f, .055f, .05f, .84f); GUI.DrawTexture(new Rect(x, y, w, h), _white); GUI.color = Color.white;
            GUI.color = new Color(.5f, .92f, .82f, .45f); GUI.DrawTexture(new Rect(x, y, w, 2 * _k), _white); GUI.color = Color.white;
            _badge.normal.textColor = new Color(.75f, 1f, .94f);
            GUI.Label(new Rect(x, y, w, h), text, _badge);
        }
    }
}
