using System;
using TunnelCrew.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 보스 등장 시네마틱 — 원본 <c>tcBossFxJs</c> (FX7.3.1). 월드를 멈추고 렌더만 돌리며
    /// 레터박스·비네트 → 카메라가 보스로 팬(줌 ×1.15) → 이름 플레이트 → 포효(킥 13·링·버스트·플래시·천장 먼지) → 복귀.
    /// 구간: dim .55 · pan 1.05 · hold .35 · roar 1.75 · back .95. 클릭/Esc 스킵. 감속 모드면 연출 없이 진행.
    /// 포효 음성은 실전 fireBreath 15프레임(≈1.4s / 1.15 배속)과 같은 시점에 낸다.
    /// </summary>
    public sealed class BossIntroCinematic : MonoBehaviour
    {
        const float SDim = .55f, SPan = 1.05f, SHold = .35f, SRoar = 1.75f, SBack = .95f;
        const float T1 = SDim, T2 = T1 + SPan, T3 = T2 + SHold, T4 = T3 + SRoar, T5 = T4 + SBack;
        const float RoarImpactT = 1.4f / 1.15f;

        public bool Active { get; private set; }
        TunnelSim _sim; CameraRig _rig; Camera _cam; Feedback _feedback; FxSystem _fx;
        BossState _boss; float _t; bool _roared, _roarPlayed; float _dustAcc;
        Texture2D _white; GUIStyle _tier, _title;

        public void Bind(TunnelSim sim, CameraRig rig, Camera cam, Feedback feedback, FxSystem fx)
        {
            _sim = sim; _rig = rig; _cam = cam; _feedback = feedback; _fx = fx; _white = Texture2D.whiteTexture;
        }

        public void Begin(BossState boss)
        {
            if (Active || boss == null || !boss.Body.Alive) return;
            if (_feedback != null && _feedback.ReducedMotion) return;   // 모션 줄이기 — 연출 없이 진행
            if (_sim.Phase != GamePhase.Playing) return;
            _boss = boss; _t = 0; _roared = false; _roarPlayed = false; _dustAcc = 0;
            Active = true;
            _rig.CineOverride = CineCam;
            AudioDirector.Instance?.Rumble();
        }
        public void Stop()
        {
            if (!Active) return;
            Active = false; _rig.CineOverride = null; _boss = null;
        }

        static float Ease(float p) => p < .5f ? 4 * p * p * p : 1 - Mathf.Pow(-2 * p + 2, 3) / 2;
        static float EaseOut(float p) => 1 - Mathf.Pow(1 - p, 3);
        float Seg(float a, float b) => Mathf.Clamp01((_t - a) / Mathf.Max(.0001f, b - a));

        (Vector2 center, float zoomMul)? CineCam()
        {
            if (!Active || _boss == null) return null;
            float panP = Ease(Seg(T1, T2)), backP = Ease(Seg(T4, T5));
            float k = panP * (1 - backP);
            var pc = new Vector2((float)_sim.Player.Position.X, (float)_sim.Player.Position.Y);
            var bc = new Vector2((float)_boss.Body.Position.X, (float)_boss.Body.Position.Y);
            float outP = Seg(T4 + .15f, T5);
            // 저주파 흔들림 — 지속 진동은 카메라로 직접 만든다 (J.kick 은 순간 충격용)
            float amp = 2.4f * EaseOut(Seg(0, T1)) * (1 - EaseOut(outP)) + (_roared ? 5.5f * Mathf.Exp(-(_t - T3) * 2.6f) : 0);
            float cellPx = Screen.height / (_cam.orthographicSize * 2);
            float wobX = (Mathf.Sin(_t * 7.3f) * .65f + Mathf.Sin(_t * 3.1f) * .35f) * amp / cellPx;
            float wobY = (Mathf.Cos(_t * 5.9f) * .6f + Mathf.Cos(_t * 2.7f) * .4f) * amp * .7f / cellPx;
            return (Vector2.Lerp(pc, bc, k) + new Vector2(wobX, wobY), 1 + .15f * k);
        }

        void Update()
        {
            if (!Active) return;
            float dt = Mathf.Min(.05f, Time.unscaledDeltaTime);
            _t += dt;
            if (_boss == null || !_boss.Body.Alive || _sim.Phase != GamePhase.Playing) { Stop(); return; }
            var kb = Keyboard.current; var mouse = Mouse.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame)) { Stop(); return; }

            var bp = _boss.Body.Position; float r = (float)_boss.Body.Radius;
            if (!_roared && _t >= T3)
            {
                _roared = true;
                _feedback?.Kick(13f, Vector2.zero);
                _fx?.Ring(V(bp), new Color(1f, .33f, .49f), .3f, 4.6f, 7f);
                _fx?.Ring(V(bp), new Color(1f, .83f, .43f), .2f, 2.8f, 4f);
                _fx?.Burst(V(bp), 42, new[] { new Color(1f, .33f, .49f), new Color(1f, .69f, .72f), new Color(.78f, .63f, 1f), Color.white }, 360);
            }
            if (_roared && _t < T4)
            {
                _dustAcc += dt;
                while (_dustAcc >= .085f)
                {
                    _dustAcc -= .085f;
                    _fx?.Burst(V(bp) + new Vector2(UnityEngine.Random.Range(-3.5f, 3.5f), UnityEngine.Random.Range(1.6f, 4f)), 3, new[] { new Color(.54f, .46f, .4f), new Color(.35f, .27f, .22f), new Color(.79f, .7f, .63f) }, 54);
                }
                if (_t - T3 > .34f && UnityEngine.Random.value < dt * 4.2f) _feedback?.Kick(3.4f, Vector2.zero);
                if (!_roarPlayed && _t - T3 >= RoarImpactT) { _roarPlayed = true; AudioDirector.Instance?.Roar(); }
            }
            if (_t >= T5) Stop();
        }
        static Vector2 V(Vec2 v) => new Vector2((float)v.X, (float)v.Y);

        void OnGUI()
        {
            if (!Active || _boss == null) return;
            Fonts.ApplySkin();
            float H = Screen.height, W = Screen.width, k = H / 1080f;
            if (_tier == null)
            {
                _tier = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * k), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
                _title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(40 * k), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            }
            float outP = Seg(T4 + .15f, T5);
            float barP = EaseOut(Seg(0, T1 + .25f));
            float barH = H * .115f * barP * (1 - EaseOut(outP));
            Fill(new Rect(0, 0, W, barH), Color.black); Fill(new Rect(0, H - barH, W, barH), Color.black);
            // 비네트 — 가장자리 어둡게 (4변 그라데이션 대용)
            float vig = barP * .9f * (1 - EaseOut(outP));
            Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, .28f * vig));
            // 플래시 — 포효 순간
            if (_roared) Fill(new Rect(0, 0, W, H), new Color(1f, .93f, .9f, Mathf.Exp(-(_t - T3) * 4.2f) * .75f));
            // 이름 플레이트
            float nameIn = EaseOut(Seg(T2 + .05f, T3 + .3f)), nameOut = EaseOut(Seg(T4 - .1f, T4 + .35f));
            float a = nameIn * (1 - nameOut);
            if (a > 0)
            {
                string tier = _boss.Tier == BossTier.Guardian ? "DEEP GUARDIAN · 중심부의 전조" : _boss.Tier == BossTier.Variant ? "VARIANT · 강화 개체" : "APEX PREDATOR · 중심부 보스";
                float y = H - barH - H * .055f - 90 * k + 16 * k * (1 - nameIn);
                _tier.normal.textColor = new Color(1f, .55f, .66f, a); _title.normal.textColor = new Color(.98f, .95f, 1f, a);
                GUI.Label(new Rect(0, y, W, 24 * k), tier, _tier);
                GUI.Label(new Rect(0, y + 26 * k, W, 56 * k), _boss.Def.Name, _title);
                Fill(new Rect(W * .5f - 90 * k, y + 86 * k, 180 * k, 2 * k), new Color(1f, .55f, .66f, a * .8f));
            }
            float skipA = barP * (1 - EaseOut(outP)) * .75f;
            var s = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(11 * k), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            s.normal.textColor = new Color(.55f, .5f, .6f, skipA);
            GUI.Label(new Rect(W - 240 * k, H - barH * .36f - 20 * k, 220 * k, 20 * k), "CLICK / ESC — SKIP", s);
        }
        void Fill(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, _white); GUI.color = Color.white; }
    }
}
