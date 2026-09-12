using System;
using System.Collections.Generic;
using TunnelCrew.Sim;
using UnityEngine;
using L = TunnelCrew.Presentation.AudioSynth.Layer;
using W = TunnelCrew.Presentation.AudioSynth.Wave;
using F = TunnelCrew.Presentation.AudioSynth.Filter;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 오디오 — 원본 <c>AU</c>(버스·볼륨) + <c>SFX</c>(절차 합성 25종) + <c>SMP</c>(Kenney 샘플 우선·절차 폴백) +
    /// <c>DRILL_SMP</c>(드릴 start/loop/release 3-파트, 열 → 디튠/떨림) + <c>BGM_ROUTE/AMBI/BOSS_BGM</c>(로비·땅굴 2겹·보스 곡 페이드 규칙).
    ///
    /// 버스: master .9 × 설정 · sfx(설정 SfxVolume) · music .66 × 설정 BgmVolume. 카테고리 게인 vol{dig 2.2, brk 1.2, combat 1.45, ui 1.85, loot 1.2, alert 1.35}.
    /// 원본 SFX 버스는 pre 1/3 → 소프트 리미터였고 catMul 에 sfxMix 3.0 이 곱해져 서로 상쇄된다 — 여기서는 g × vol[cat] 만 굽고 렌더 끝에 리미터를 건다.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        const float Master = .9f, MusicBase = .66f;
        static readonly Dictionary<string, float> Cat = new Dictionary<string, float> { ["dig"] = 2.2f, ["brk"] = 1.2f, ["combat"] = 1.45f, ["ui"] = 1.85f, ["loot"] = 1.2f, ["alert"] = 1.35f, ["ping"] = 1f, ["sfx"] = 1f };
        static float CatMul(string cat) => cat != null && Cat.TryGetValue(cat, out var v) ? v : 1f;

        public float SfxVolume = .9f, BgmVolume = .8f; public bool Muted;
        float SfxBus => Muted ? 0 : Master * SfxVolume;
        float MusicBus => Muted ? 0 : Master * MusicBase * BgmVolume;

        // ── 원샷 풀
        readonly List<AudioSource> _pool = new List<AudioSource>();
        readonly Dictionary<string, AudioClip> _proc = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, (string cat, float g, float jit, AudioClip[] clips)> _bank = new Dictionary<string, (string, float, float, AudioClip[])>();
        readonly Dictionary<string, double> _thr = new Dictionary<string, double>();
        System.Random _rng = new System.Random(3);
        AudioClip _roar;

        void Awake()
        {
            Instance = this;
            for (int i = 0; i < 12; i++) { var go = new GameObject("sfx" + i); go.transform.SetParent(transform, false); var s = go.AddComponent<AudioSource>(); s.playOnAwake = false; s.spatialBlend = 0; _pool.Add(s); }
            LoadBank(); BuildDrill(); BuildMusic();
            _roar = Resources.Load<AudioClip>("Audio/sfx/dragon-boss-roar");
        }

        [Serializable] class BankRow { public string name, cat; public float g, jit; public int n; }
        [Serializable] class BankFile { public BankRow[] bank; }
        void LoadBank()
        {
            var ta = Resources.Load<TextAsset>("Audio/sfx/bank");
            if (ta == null) return;
            var bf = JsonUtility.FromJson<BankFile>(ta.text);
            foreach (var r in bf.bank)
            {
                var clips = new List<AudioClip>();
                for (int i = 0; i < r.n; i++) { var c = Resources.Load<AudioClip>($"Audio/sfx/{r.name}_{i}"); if (c != null) clips.Add(c); }
                if (clips.Count > 0) _bank[r.name] = (string.IsNullOrEmpty(r.cat) ? null : r.cat, r.g, r.jit, clips.ToArray());
            }
        }

        bool Thr(string k, double ms)
        {
            double now = Time.unscaledTimeAsDouble * 1000;
            if (_thr.TryGetValue(k, out var last) && now - last < ms) return false;
            _thr[k] = now; return true;
        }
        AudioSource Free()
        {
            foreach (var s in _pool) if (!s.isPlaying) return s;
            var oldest = _pool[0]; foreach (var s in _pool) if (s.time > oldest.time) oldest = s;
            return oldest;
        }
        float Rnd(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        /// <summary>샘플 뱅크 재생 (원본 SMP.play). 없으면 false → 호출자가 절차 합성으로 폴백.</summary>
        public bool Sample(string name, float gMul = 1, float at = 0, float rate = 0)
        {
            if (!_bank.TryGetValue(name, out var b) || Muted) return false;
            var clip = b.clips[_rng.Next(b.clips.Length)];
            var s = Free(); s.clip = clip; s.pitch = rate > 0 ? rate : 1 + Rnd(-b.jit, b.jit);
            s.volume = Mathf.Clamp01(b.g * CatMul(b.cat) * gMul * SfxBus);
            if (at > 0) s.PlayDelayed(at); else s.Play();
            return true;
        }
        /// <summary>절차 합성 — 이름별로 한 번 렌더해 캐시.</summary>
        void Proc(string name, Func<L[]> make, float volMul = 1)
        {
            if (Muted) return;
            if (!_proc.TryGetValue(name, out var clip)) _proc[name] = clip = AudioSynth.Render(name, make());
            var s = Free(); s.clip = clip; s.pitch = 1; s.volume = Mathf.Clamp01(SfxBus * volMul); s.Play();
        }
        static L T(float f, float dur, float g, string cat = null, W type = W.Triangle, float slide = 0, float lp = 0, float atk = .006f, float at = 0) => AudioSynth.Tone(f, dur, g * CatMul(cat), type, slide, lp, atk, at);
        static L H(float dur, float f0, float f1, float g, string cat = null, float q = 1.1f, float rate = 1, F ft = F.Bandpass, float at = 0) => AudioSynth.Hit(dur, f0, f1, g * CatMul(cat), q, rate, ft, at);

        // ═════════════════════ SFX 뱅크 (원본 const SFX — 수치 그대로)
        public void Ui() { if (Sample("ui")) return; Proc("ui", () => new[] { T(220, .08f, .07f, "ui", W.Sine, 0, 900), H(.04f, 280, 90, .04f, "ui", .5f, 1, F.Lowpass) }); }
        public void Hover() { Sample("hover"); }
        public void MenuClick() { if (Sample("menuclick")) return; Ui(); }
        public void Back() { if (Sample("back")) return; Proc("back", () => new[] { T(160, .11f, .07f, "ui", W.Triangle, 95, 700) }); }
        public void Dig()
        {
            if (!Thr("dig", 70)) return;
            if (Sample("dig")) return;
            Proc("dig", () => new[] { H(.09f, 260, 70, .13f, "dig", .45f, .45f, F.Lowpass), H(.07f, 1500, 420, .045f, "dig", .9f, 1.05f, F.Bandpass), T(48, .07f, .07f, "dig", W.Sine, 0, 160) });
        }
        public void Brk()
        {
            if (Sample("brk")) { Proc("brk_low", () => new[] { T(92, .22f, .18f, "brk", W.Sine, 42, 280) }); return; }   // 기존 저역 sine 보강
            Proc("brk", () => new[] { H(.28f, 420, 70, .28f, "brk", .55f, .55f, F.Lowpass), T(92, .22f, .18f, "brk", W.Sine, 42, 280) });
        }
        public void Ore() => Proc("ore", () => new[] { H(.26f, 640, 110, .22f, null, .6f, .6f, F.Bandpass), T(196, .34f, .12f, null, W.Triangle, 0, 700), T(233, .38f, .08f, null, W.Sine, 0, 600, .006f, .04f), T(78, .28f, .16f, null, W.Sine, 48, 220) });
        public void OreBreak()
        {
            if (Sample("orebrk")) { Proc("orebrk_low", () => new[] { T(78, .26f, .13f, "brk", W.Sine, 48, 220), T(196, .20f, .05f, "brk", W.Triangle, 0, 700) }); return; }
            Ore();
        }
        public void Res() { if (!Thr("res", 60)) return; if (Sample("res")) return; Proc("res", () => new[] { T(310, .09f, .055f, null, W.Sine, 0, 900), T(155, .11f, .035f, null, W.Triangle, 0, 600, .006f, .02f) }); }
        public void Shard() { if (Sample("shard")) { Proc("shard_low", () => new[] { T(110, .18f, .05f, "loot", W.Sine, 0, 300) }); return; } Tick(); }
        public void Deploy() { if (Sample("deploy")) return; Proc("deploy", () => new[] { T(180, .11f, .09f, null, W.Triangle, 0, 900), T(240, .13f, .07f, null, W.Sine, 0, 800, .006f, .05f) }); }
        public void Pick() { if (Sample("pick")) return; Proc("pick", () => new[] { T(240, .10f, .09f, null, W.Triangle, 0, 1100), T(180, .14f, .06f, null, W.Sine, 0, 700, .006f, .04f) }); }
        public void Rescue() => Proc("rescue", () => new[] { T(196, .24f, .10f, null, W.Sine, 0, 900), T(233, .24f, .10f, null, W.Sine, 0, 900, .006f, .09f), T(277, .24f, .10f, null, W.Sine, 0, 900, .006f, .18f), H(.22f, 700, 140, .08f, null, .7f, 1, F.Lowpass) });
        public void Cache() => Proc("cache", () => new[] { T(196, .30f, .09f, null, W.Triangle, 0, 1000), T(247, .30f, .09f, null, W.Triangle, 0, 1000, .006f, .07f), T(294, .30f, .09f, null, W.Triangle, 0, 1000, .006f, .14f), T(98, .40f, .14f, null, W.Sine, 55, 260), H(.28f, 900, 160, .09f, null, .8f, 1, F.Lowpass) });
        public void Exit() => Proc("exit", () => new[] { T(110, .85f, .11f, null, W.Sine, 0, 700), T(131, .85f, .11f, null, W.Sine, 0, 700, .006f, .08f), T(98, .85f, .11f, null, W.Sine, 0, 700, .006f, .16f), T(82, .85f, .11f, null, W.Sine, 0, 700, .006f, .24f), H(.55f, 280, 60, .14f, null, .5f, 1, F.Lowpass) });
        public void Descend() => Proc("descend", () => new[] { H(.65f, 480, 55, .22f, null, .55f, .5f, F.Lowpass), T(140, .75f, .14f, null, W.Sine, 48, 320) });
        public void Tick() => Proc("tick", () => new[] { T(380, .07f, .09f, null, W.Triangle, 0, 900), H(.045f, 1700, 520, .05f, null, 1.3f, 1, F.Bandpass) });
        public void Shot() { if (Sample("shot")) return; Tick(); }
        public void Reload(bool manual)
        {
            if (!Thr("reload", 120)) return;
            if (Sample("reload")) { if (manual) Sample("reload", .8f, .13f); return; }
            Proc(manual ? "reload_m" : "reload", () => new List<L>
            {
                H(.045f, 1900, 720, .12f, "combat", 2.1f, 1.55f, F.Bandpass), T(185, .06f, .075f, "combat", W.Triangle, 112, 950),
                H(.075f, 780, 170, .105f, "combat", .75f, .82f, F.Lowpass, .055f), T(104, .085f, .07f, "combat", W.Sine, 68, 260, .006f, .065f),
            }.Concat(manual ? new[] { H(.055f, 1250, 410, .07f, "combat", 1.5f, 1.2f, F.Bandpass, .13f) } : new L[0]).ToArray());
        }
        public void ReloadDone()
        {
            if (Sample("reloadDone")) return;
            Proc("reloadDone", () => new[] { H(.038f, 2400, 920, .13f, "combat", 2.4f, 1.8f, F.Bandpass), T(235, .052f, .065f, "combat", W.Square, 118, 1200), H(.11f, 520, 82, .12f, "combat", .62f, .68f, F.Lowpass, .025f), T(82, .12f, .08f, "combat", W.Sine, 49, 220, .006f, .025f) });
        }
        public void Warn() => Proc("warn", () => new[] { T(130, .18f, .08f, "alert", W.Sawtooth, 90, 600) });
        public void Ready() => Proc("ready", () => new[] { T(220, .12f, .08f, null, W.Sine, 0, 900), T(330, .18f, .07f, null, W.Triangle, 0, 1100, .006f, .04f), T(440, .22f, .05f, null, W.Sine, 0, 1200, .006f, .08f) });
        public void Fail() => Proc("fail", () => new[] { T(110, .35f, .1f, "alert", W.Sawtooth, 55, 400), H(.4f, 200, 45, .12f, "alert", .45f, 1, F.Lowpass) });
        public void RunStart() => Proc("start", () => new[] { T(98, .28f, .1f, null, W.Sine, 0, 400), T(147, .34f, .07f, null, W.Triangle, 0, 700, .006f, .05f), H(.22f, 320, 80, .08f, null, .5f, 1, F.Lowpass) });
        public void Timeout() => Proc("timeout", () => new[] { T(90, .9f, .12f, "alert", W.Sawtooth, 40, 450), H(.55f, 220, 50, .14f, "alert", .5f, 1, F.Lowpass) });
        public void Buy() => Proc("buy", () => new[] { T(165, .26f, .10f, null, W.Sine, 0, 800), T(196, .26f, .10f, null, W.Sine, 0, 800, .006f, .08f), T(220, .26f, .10f, null, W.Sine, 0, 800, .006f, .16f) });
        public void Dawn() => Proc("dawn", () => new[] { T(98, 1f, .09f, null, W.Sine, 0, 700), T(131, 1f, .09f, null, W.Sine, 0, 700, .006f, .14f), T(165, 1f, .09f, null, W.Sine, 0, 700, .006f, .28f), T(196, 1f, .09f, null, W.Sine, 0, 700, .006f, .42f) });
        public void Kill()
        {
            if (Sample("kill")) { Sample("killwet", .8f, .015f, 1.25f); Proc("kill_low", () => new[] { T(64, .16f, .09f, "combat", W.Sine, 38, 180) }); return; }
            Ore();
        }
        public void Growl(float vol)
        {
            if (!Thr("growl", 420)) return;
            float g = Mathf.Clamp(vol, .02f, .22f);
            if (Sample("growl", g / .15f, 0, Rnd(.9f, 1.1f))) return;
            float f0 = Rnd(70, 125);
            Proc("growl" + _rng.Next(3), () => new[] { H(.45f, Rnd(180, 300), 40, g * .9f, null, .4f, Rnd(.28f, .48f), F.Lowpass), T(f0, .55f, g * .55f, null, W.Sawtooth, f0 * .55f, 280, .04f), T(f0 * 1.35f, .4f, g * .28f, null, W.Triangle, f0 * .7f, 360, .08f) });
        }
        public void CardFlip() { if (Sample("cardflip")) return; Tick(); }
        public void CardPick() { if (Sample("cardpick")) return; Buy(); }
        public void Step() { if (!Sample("step", 1, 0, Rnd(.95f, 1.07f))) return; if (_rng.NextDouble() < .18) Sample("cloth", .65f, .02f); }
        public void StepCrew() { if (!Thr("crewStep", 215 + _rng.NextDouble() * 130)) return; Sample("stepcrew", 1, 0, Rnd(.94f, 1.09f)); }
        public void Dash() { if (Sample("gear")) Sample("cloth", .8f, .03f); }
        public void DrillOverload()
        {
            Sample("ovl"); Sample("ovl2", 1, .05f);
            Proc("drillOverload", () => new[] { T(430, .5f, .085f, "dig", W.Sawtooth, 58, 1100, .008f), T(215, .56f, .075f, "dig", W.Triangle, 40, 520), H(.42f, 1800, 210, .085f, "dig", .8f, .75f, F.Bandpass), H(.3f, 2600, 900, .05f, "dig", .5f, 1.5f, F.Highpass, .1f), T(88, .34f, .12f, "dig", W.Sine, 44, 220, .006f, .02f) });
        }
        /// <summary>보스 등장 저주파 울림 (원본 tcBossFx rumbleSfx).</summary>
        public void Rumble() => Proc("rumble", () => new[] { T(41, 1.5f, .13f, "sfx", W.Sine, 33, 190), T(63, 1.2f, .07f, "sfx", W.Triangle, 47, 300) });
        public void Roar() { if (_roar == null || Muted) return; var s = Free(); s.clip = _roar; s.pitch = 1; s.volume = Mathf.Clamp01(SfxBus); s.Play(); }

        /// <summary>
        /// FTUE 컷만화 전용 음형. 의미를 담은 음성이 아니라 발신자의 리듬·음역만
        /// 구분하는 비언어 무전음이다. 각 컷의 첫 프레임에 한 번만 호출한다.
        /// </summary>
        public void FtueCue(string cue)
        {
            switch (cue)
            {
                case "panel":
                    Proc("ftue_panel", () => new[] { H(.09f, 1700, 430, .075f, "ui", 1.2f, 1.15f, F.Bandpass), T(92, .12f, .045f, "ui", W.Triangle, 58, 420) });
                    break;
                case "impact":
                    Proc("ftue_impact", () => new[] { H(.75f, 520, 38, .32f, "sfx", .42f, .36f, F.Lowpass), T(39, .95f, .22f, "sfx", W.Sine, 28, 150), H(.24f, 3200, 620, .11f, "sfx", 1.4f, 1.25f, F.Bandpass, .035f) });
                    break;
                case "control":
                    Proc("ftue_control", () => new[] { H(.14f, 3300, 640, .035f, "ui", 1.5f, 1, F.Bandpass), T(690, .105f, .06f, "ui", W.Sine, 0, 2500, .004f, .025f), T(870, .11f, .05f, "ui", W.Sine, 0, 2900, .004f, .16f) });
                    break;
                case "morae":
                    Proc("ftue_morae", () => new[] { H(.12f, 2400, 390, .03f, "ui", 1.1f, 1, F.Bandpass), T(238, .09f, .065f, "ui", W.Triangle, 0, 1200, .006f, .018f), T(204, .085f, .055f, "ui", W.Triangle, 0, 1100, .006f, .13f), T(264, .12f, .05f, "ui", W.Triangle, 0, 1300, .006f, .24f) });
                    break;
                case "captain":
                    Proc("ftue_captain", () => new[] { H(.24f, 1850, 240, .055f, "ui", .72f, .8f, F.Bandpass), T(146, .18f, .07f, "ui", W.Sawtooth, 112, 720, .015f, .025f), T(174, .16f, .05f, "ui", W.Triangle, 126, 820, .012f, .19f) });
                    break;
                case "mimic":
                    Proc("ftue_mimic", () => new[] { H(.52f, 2200, 120, .08f, "alert", .5f, .55f, F.Bandpass), T(132, .5f, .065f, "alert", W.Sawtooth, 238, 680, .08f), T(198, .65f, .045f, "alert", W.Triangle, 109, 760, .12f, .11f), T(49, .8f, .1f, "sfx", W.Sine, 37, 170) });
                    break;
                case "beacon":
                    Proc("ftue_beacon", () => new[] { T(740, .19f, .075f, "alert", W.Sine, 0, 2600), T(740, .13f, .065f, "alert", W.Sine, 0, 2600, .006f, .32f), T(920, .28f, .075f, "alert", W.Sine, 0, 3000, .006f, .53f), H(.82f, 2800, 500, .025f, "alert", 1.2f, 1, F.Bandpass) });
                    break;
                case "knock":
                    Proc("ftue_knock", () => new[] { H(.13f, 310, 54, .13f, "brk", .5f, .48f, F.Lowpass), T(68, .18f, .08f, "brk", W.Sine, 45, 220), H(.12f, 290, 48, .1f, "brk", .45f, .43f, F.Lowpass, .34f), T(63, .16f, .065f, "brk", W.Sine, 42, 210, .006f, .34f) });
                    break;
                case "scan":
                    Proc("ftue_scan", () => new[] { T(420, .28f, .055f, "ui", W.Sine, 930, 2400), T(210, .32f, .04f, "ui", W.Triangle, 465, 1300, .006f, .08f), H(.3f, 2600, 700, .035f, "ui", 1.8f, 1, F.Bandpass) });
                    break;
                case "collapse":
                    Proc("ftue_collapse", () => new[] { H(1.25f, 460, 32, .25f, "sfx", .36f, .3f, F.Lowpass), T(34, 1.4f, .17f, "sfx", W.Sine, 25, 130), H(.55f, 2400, 180, .075f, "sfx", .65f, .68f, F.Bandpass, .08f) });
                    break;
                case "door":
                    Proc("ftue_door", () => new[] { H(.46f, 820, 65, .19f, "sfx", .55f, .55f, F.Lowpass), T(72, .52f, .13f, "sfx", W.Sine, 38, 240), H(.1f, 2500, 500, .08f, "sfx", 1.5f, 1.2f, F.Bandpass, .4f) });
                    break;
                case "title":
                    Proc("ftue_title", () => new[] { T(55, 1.35f, .13f, "sfx", W.Sine, 0, 260), T(110, 1.1f, .08f, "sfx", W.Triangle, 0, 620, .035f, .18f), T(165, 1f, .065f, "sfx", W.Sine, 0, 900, .04f, .34f), T(220, .9f, .05f, "sfx", W.Triangle, 0, 1200, .04f, .5f), H(.7f, 1800, 180, .07f, "sfx", .8f, .8f, F.Bandpass, .2f) });
                    break;
                default:
                    FtueCue("panel");
                    break;
            }
        }

        /// <summary>팀 핑 음형 (원본 tc-ping sound()) — 거리와 무관한 2D UI 음, g .14.</summary>
        public void Ping(PingType type)
        {
            const float g = .14f; const string c = "ping";
            switch (type)
            {
                case PingType.Here: Proc("ping_here", () => new[] { T(520, .14f, g, c, W.Sine, 0, 2400) }); break;
                case PingType.Go: Proc("ping_go", () => new[] { T(440, .12f, g, c, W.Sine, 0, 2400), T(660, .18f, g, c, W.Sine, 0, 2400, .006f, .11f) }); break;
                case PingType.Attack: Proc("ping_attack", () => new[] { T(330, .1f, g, c, W.Square, 0, 1400), H(.08f, 500, 180, g * .5f, c, .7f, 1, F.Lowpass), T(330, .14f, g, c, W.Square, 0, 1400, .006f, .12f), H(.08f, 500, 180, g * .5f, c, .7f, 1, F.Lowpass, .12f) }); break;
                case PingType.Find: Proc("ping_find", () => new[] { T(880, .3f, g, c, W.Sine, 1240, 2400) }); break;
                case PingType.Mine: Proc("ping_mine", () => new[] { H(.12f, 320, 90, g * .9f, c, .7f, 1, F.Lowpass), T(240, .16f, g, c, W.Triangle, 0, 2400, .006f, .02f) }); break;
                case PingType.Retreat: Proc("ping_retreat", () => new[] { T(220, .14f, g, c, W.Triangle, 0, 900), T(175, .26f, g, c, W.Triangle, 0, 900, .006f, .15f) }); break;
                case PingType.Defend: Proc("ping_defend", () => new[] { T(392, .2f, g, c, W.Triangle, 0, 2400), T(392, .3f, g, c, W.Triangle, 0, 2400, .006f, .16f) }); break;
                case PingType.Help: Proc("ping_help", () => new[] { T(523, .12f, g, c, W.Sine, 0, 2400), T(784, .3f, g, c, W.Sine, 0, 2400, .006f, .12f) }); break;
                case PingType.Danger: Proc("ping_danger", () => new[] { T(196, .16f, g * .8f, c, W.Sawtooth, 0, 1000), H(.1f, 500, 180, g * .5f, c, .7f, 1, F.Lowpass), T(196, .3f, g * .8f, c, W.Sawtooth, 0, 1000, .006f, .18f), H(.1f, 500, 180, g * .5f, c, .7f, 1, F.Lowpass, .18f) }); break;
            }
        }
        /// <summary>퀵크래프트 sfx(name) — ui / tick / cache / brk.</summary>
        public void Named(string name)
        {
            switch (name) { case "ui": Ui(); break; case "tick": Tick(); break; case "cache": Cache(); break; case "brk": Brk(); break; }
        }

        // ═════════════════════ 드릴 — start → loop → release (원본 DRILL_SMP)
        AudioSource _dStart, _dLoop, _dRel; AudioClip _cStart, _cLoop, _cRel;
        enum DrillSt { Idle, Play, Stopping } DrillSt _dSt; float _dHeat, _dLoopFade, _dRelFade, _dLoopAt; bool _dLoopStarted;
        const float DrillGain = .32f, RoomTail = .9f, Join = .141f, RelXf = .249f, Wob = 28f, WobHz = 12f, HeatWob = 137f;
        void BuildDrill()
        {
            _cStart = Resources.Load<AudioClip>("Audio/drill/drill_start"); _cLoop = Resources.Load<AudioClip>("Audio/drill/drill_loop"); _cRel = Resources.Load<AudioClip>("Audio/drill/drill_rel");
            AudioSource Mk(string n, bool loop) { var go = new GameObject(n); go.transform.SetParent(transform, false); var s = go.AddComponent<AudioSource>(); s.playOnAwake = false; s.loop = loop; s.spatialBlend = 0; return s; }
            _dStart = Mk("drillStart", false); _dLoop = Mk("drillLoop", true); _dRel = Mk("drillRel", false);
        }
        public bool DrillUsable => _cStart != null && _cLoop != null && _cRel != null;
        float DrillLevel => Mathf.Clamp01(DrillGain * CatMul("dig") * SfxBus);
        /// <summary>열(0~1) → 디튠(cent): 1200·log2(1 + .45·h^1.25).</summary>
        static float HeatCents(float h) => 1200f * Mathf.Log(1 + .45f * Mathf.Pow(Mathf.Clamp01(h), 1.25f), 2f);
        public void Drill(bool on) { if (on) DrillPlay(); else DrillRelease(); }
        void DrillPlay()
        {
            if (!DrillUsable || _dSt == DrillSt.Play) return;
            if (_dSt == DrillSt.Stopping) { _dRel.Stop(); }
            float pitch = Mathf.Pow(2, HeatCents(_dHeat) / 1200f);
            _dStart.clip = _cStart; _dStart.pitch = pitch; _dStart.volume = DrillLevel; _dStart.Play();
            float aLen = Mathf.Max(.01f, _cStart.length - RoomTail);
            _dLoopAt = Time.unscaledTime + Mathf.Max(0, aLen - Join); _dLoopStarted = false; _dLoopFade = 0;
            _dLoop.clip = _cLoop; _dLoop.pitch = pitch; _dLoop.volume = 0;
            _dSt = DrillSt.Play;
        }
        void DrillRelease()
        {
            if (_dSt != DrillSt.Play) return;
            _dSt = DrillSt.Stopping; _dRelFade = 0;
            _dRel.clip = _cRel; _dRel.pitch = Mathf.Pow(2, HeatCents(_dHeat) / 1200f); _dRel.volume = 0; _dRel.Play();
        }
        public void DrillHeat(float h) { _dHeat = Mathf.Clamp01(h); }
        void TickDrill(float dt)
        {
            float cents = HeatCents(_dHeat);
            float wob = Mathf.Sin(Time.unscaledTime * (WobHz + 14 * _dHeat) * Mathf.PI * 2) * (Wob + HeatWob * _dHeat * _dHeat) + Mathf.Sin(Time.unscaledTime * 1.1f * Mathf.PI * 2) * 29f;   // 떨림 + 드리프트
            float pitch = Mathf.Pow(2, (cents + wob) / 1200f);
            if (_dSt == DrillSt.Play)
            {
                _dStart.pitch = pitch; _dLoop.pitch = pitch;
                if (!_dLoopStarted && Time.unscaledTime >= _dLoopAt) { _dLoopStarted = true; _dLoop.Play(); }
                if (_dLoopStarted) { _dLoopFade = Mathf.Min(1, _dLoopFade + dt / Join); _dLoop.volume = DrillLevel * _dLoopFade; _dStart.volume = DrillLevel * (1 - _dLoopFade); }
                else _dStart.volume = DrillLevel;
            }
            else if (_dSt == DrillSt.Stopping)
            {
                _dRelFade = Mathf.Min(1, _dRelFade + dt / RelXf);
                _dStart.volume = DrillLevel * (1 - _dRelFade) * (_dLoopStarted ? 0 : 1); _dLoop.volume = DrillLevel * (1 - _dRelFade); _dRel.volume = DrillLevel * _dRelFade;
                if (_dRelFade >= 1) { _dStart.Stop(); _dLoop.Stop(); }
                if (!_dRel.isPlaying && _dRelFade >= 1) _dSt = DrillSt.Idle;
            }
        }
        public void DrillKill() { _dStart.Stop(); _dLoop.Stop(); _dRel.Stop(); _dSt = DrillSt.Idle; }

        // ═════════════════════ BGM 라우터 — 로비 / 땅굴(2겹) / 보스 (원본 BGM_ROUTE + AMBI + BOSS_BGM)
        public enum Route : byte { None, Lobby, Tunnel, Boss }
        public Route Current { get; private set; } = Route.None;
        sealed class Track { public AudioSource Src; public float Gain, Target, Level, FadeIn, FadeOut; }
        Track _lobby, _tun1, _tun2, _boss; Route _prevRoute; float _bossBackTimer = -1;
        float _narrativeDuck = 1f, _narrativeDuckTarget = 1f;
        const float AmbFadeIn = 1.4f, AmbFadeOut = .9f, BossFadeIn = .55f, BossFadeOut = 2.8f;

        void BuildMusic()
        {
            Track Mk(string n, string res, float gain, float fi, float fo, float pre = 1)
            {
                var clip = Resources.Load<AudioClip>("Audio/music/" + res);
                if (clip != null && pre != 1) clip = PreGain(clip, pre);
                var go = new GameObject(n); go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>(); s.clip = clip; s.loop = true; s.playOnAwake = false; s.spatialBlend = 0; s.volume = 0;
                return new Track { Src = s, Gain = gain, FadeIn = fi, FadeOut = fo };
            }
            // 로비: 원본 lobby-cave.webm(g 1.00) 을 Vorbis 로 변환한 lobby-cave.ogg (PyAV, 2026-09-07). 파일이 없으면 이전 대체(땅굴 던전 한 겹 .8) 로 돌아간다
            _lobby = Resources.Load<AudioClip>("Audio/music/lobby-cave") != null
                ? Mk("bgmLobby", "lobby-cave", 1f, AmbFadeIn, AmbFadeOut)
                : Mk("bgmLobby", "tunnel-dungeon", .8f, AmbFadeIn, AmbFadeOut);
            _tun1 = Mk("ambTunnelDungeon", "tunnel-dungeon", 1.19f, AmbFadeIn, AmbFadeOut);
            _tun2 = Mk("ambTunnelCave", "tunnel-cave-stereo", 1f, AmbFadeIn, AmbFadeOut, 4.48f);   // 원본 녹음 레벨이 매우 낮아 4.48 배 — 클립에 미리 굽는다
            _boss = Mk("bgmBoss", "boss-blood-ascendant", .78f, BossFadeIn, BossFadeOut);
        }
        static AudioClip PreGain(AudioClip clip, float mul)
        {
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i] * mul, -1, 1);
            var c = AudioClip.Create(clip.name + "_x" + mul, clip.samples, clip.channels, clip.frequency, false);
            c.SetData(data, 0);
            return c;
        }
        void Want(Track t, bool on)
        {
            t.Target = on ? 1 : 0;
            if (on && !t.Src.isPlaying && t.Src.clip != null) { t.Src.time = (float)(_rng.NextDouble() * Mathf.Max(0, t.Src.clip.length - 1)); t.Src.Play(); }
        }
        void Apply(Route r)
        {
            Want(_lobby, r == Route.Lobby); Want(_tun1, r == Route.Tunnel); Want(_tun2, r == Route.Tunnel); Want(_boss, r == Route.Boss);
        }
        public void UseLobby() { if (Current == Route.Lobby) return; Current = Route.Lobby; _bossBackTimer = -1; Apply(Current); }
        public void UseTunnel() { if (Current == Route.Tunnel) return; Current = Route.Tunnel; _bossBackTimer = -1; Apply(Current); }
        /// <summary>보스 등장 — 땅굴 앰비언스 0.9s 아웃, 보스 곡 0.55s 인.</summary>
        public void UseBoss()
        {
            if (Current == Route.Boss) { _bossBackTimer = -1; Want(_boss, true); return; }
            _prevRoute = Current == Route.None ? Route.Tunnel : Current; Current = Route.Boss; Apply(Current);
        }
        /// <summary>보스 종료 — fade 초 동안 곡을 죽이고 resumeAfter 뒤 이전 경로로 (원본 endBoss: 처치 3.0/2.6 · 런 종료 1.4 · 층 전환 1.0).</summary>
        public void EndBoss(float fade, float resumeAfter = -1)
        {
            if (Current != Route.Boss) return;
            _boss.FadeOut = fade; Want(_boss, false);
            _bossBackTimer = resumeAfter >= 0 ? resumeAfter : Mathf.Max(0, fade - .6f);
            Current = Route.None;
        }
        public void StopAll() { Current = Route.None; _bossBackTimer = -1; Apply(Route.None); }

        /// <summary>FTUE 말풍선·블랙박스 자막이 나올 때 BGM 만 -6 dB 억제한다.</summary>
        public void NarrativeDuck(bool on) => _narrativeDuckTarget = on ? .5f : 1f;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            SfxVolume = MetaScreens.SfxVolume; BgmVolume = MetaScreens.BgmVolume;
            _narrativeDuck = Mathf.MoveTowards(_narrativeDuck, _narrativeDuckTarget, dt / (_narrativeDuckTarget < _narrativeDuck ? .12f : .35f));
            if (_bossBackTimer >= 0) { _bossBackTimer -= dt; if (_bossBackTimer < 0) { Current = _prevRoute; Apply(Current); } }
            foreach (var t in new[] { _lobby, _tun1, _tun2, _boss })
            {
                if (t == null) continue;
                float rate = t.Target > t.Level ? dt / Mathf.Max(.05f, t.FadeIn) : dt / Mathf.Max(.05f, t.FadeOut);
                t.Level = Mathf.MoveTowards(t.Level, t.Target, rate);
                t.Src.volume = Mathf.Clamp01(t.Level * t.Gain * MusicBus * _narrativeDuck);
                if (t.Level <= 0 && t.Target <= 0 && t.Src.isPlaying) t.Src.Stop();
            }
            TickDrill(dt);
        }
    }

    static class ArrayExt
    {
        public static IEnumerable<T> Concat<T>(this List<T> a, T[] b) { foreach (var x in a) yield return x; foreach (var x in b) yield return x; }
        public static T[] ToArray<T>(this IEnumerable<T> e) { var l = new List<T>(e); return l.ToArray(); }
    }
}
