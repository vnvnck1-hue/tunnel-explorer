using System;
using TunnelCrew.Presentation.CRT;
using TunnelCrew.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using GUI = TunnelCrew.Presentation.CRT.CrtGui;

namespace TunnelCrew.Presentation
{
    public sealed partial class RunBootstrap
    {
        int _crtLastCore=-1,_crtLastPulp,_crtLastBloom;
        string _crtCoreToast,_crtPulpToast,_crtBloomToast;
        Texture2D _crtPulpIcon,_crtBloomIcon;
        float _crtResourceUntil;
        public HudSnapshot CurrentHud => new HudSnapshot(Sim);
        void CrtIcon(float x,float y,float size,CrtGlyph glyph,Color? c=null)
            => CrtSurface.Current.Icon(R(x,y,size,size),glyph,c??UiThemeProfile.Amber);
        void CrtText(float x,float y,float w,float h,string text,int size=24,Color? c=null,TextAnchor anchor=TextAnchor.MiddleLeft)
        {
            var st=Sz(_sSmall,size,anchor); st.normal.textColor=c??UiThemeProfile.Amber;
            GUI.Label(R(x,y,w,h),text,st);
        }
        void CrtGauge(float x,float y,float w,float h,double fraction,Color tint,int ticks=12)
        {
            GUI.Panel(R(x,y,w,h),new Color(.018f,.012f,.006f,.94f),UiThemeProfile.Frame,1,false);
            float f=Mathf.Clamp01((float)fraction),inner=w-8;
            GUI.Panel(R(x+4,y+4,inner*f,h-8),tint,Color.clear,0,false);
            for(int i=1;i<ticks;i++)
                GUI.Panel(R(x+4+inner*i/ticks,y+2,1,h-4),new Color(.08f,.04f,.012f,.8f),Color.clear,0,false);
        }
        void DrawCrtHud(float W,float H)
        {
            var h=CurrentHud;var p=Sim.Player;var b=Sim.Build;var roles=Sim.Roles;
            var amber=UiThemeProfile.Amber;var frame=UiThemeProfile.Frame;var critical=UiThemeProfile.Critical;
            var ctrl=CRTDisplayController.Instance;
            var safe=CrtSafeArea.Calculate(Screen.width,Screen.height,Screen.safeArea,ctrl!=null?ctrl.Effective.safeAreaInset:.055f);
            float left=safe.xMin/_k,right=safe.xMax/_k,top=(Screen.height-safe.yMax)/_k,bottom=(Screen.height-safe.yMin)/_k;

            // Survey telemetry: icon > rail > current layer. No permanent explanatory labels.
            float railWidth=Mathf.Min(530,W*.29f),railX=W*.5f-railWidth*.5f-46,railY=top+4;
            if(_crewRailIcon!=null)GUI.DrawTexture(R(railX-50,railY-7,38,38),_crewRailIcon,ScaleMode.ScaleToFit);
            else CrtIcon(railX-50,railY-7,38,CrtGlyph.Crew);
            CrtGauge(railX,railY,railWidth,22,h.Dominance,amber,10);
            CrtText(railX,railY+22,railWidth,24,h.Objective,15,frame,TextAnchor.MiddleCenter);
            if(!string.IsNullOrEmpty(h.Contract))CrtText(railX,railY+42,railWidth,22,h.Contract,13,new Color(.5f,.92f,.82f),TextAnchor.MiddleCenter);
            CrtIcon(railX+railWidth+15,railY-3,30,CrtGlyph.Depth);
            CrtText(railX+railWidth+53,railY-5,75,32,h.Depth<=3?$"{h.Depth}/3":$"{h.Depth}",24);
            if(_bossIcon!=null)
            {
                GUI.color=critical;GUI.DrawTexture(R(railX+railWidth+136,railY-10,44,44),_bossIcon,ScaleMode.ScaleToFit);GUI.color=Color.white;
            }
            else CrtIcon(railX+railWidth+136,railY-10,44,CrtGlyph.Boss,critical);
            if(Sim.Bosses.Active)
            {
                var boss=Sim.Bosses.Boss;
                CrtGauge(railX,railY+66,railWidth,14,boss.HpRatio,critical,16);
                CrtText(railX,railY+82,railWidth,30,$"{boss.Def.Name}  {boss.Body.Hp:F0}   ▰ {Sim.Bosses.ArmorAlive()}",20,critical,TextAnchor.MiddleCenter);
            }
            else
            {
                CrtIcon(railX+railWidth+15,railY+35,18,CrtGlyph.Warning,frame);
                CrtText(railX+railWidth+41,railY+29,48,30,h.Threat.ToString("F1"),18,frame,TextAnchor.MiddleLeft);
            }

            // Instrument plate. Large numbers are separate from segmented signal bars.
            float vy=bottom-196,vw=530;
            GUI.Panel(R(left,vy,vw,174),UiThemeProfile.Panel,frame);
            GUI.Panel(R(left+14,vy+15,140,144),new Color(.018f,.012f,.006f,.6f),UiThemeProfile.Dim);
            if(_badges.TryGetValue(h.Role,out var badge)&&badge!=null)
                GUI.DrawTexture(R(left+21,vy+24,126,126),badge,ScaleMode.ScaleToFit);
            else CrtIcon(left+36,vy+37,100,RoleGlyph(h.Role,true));
            float bx=left+201,bw=235;
            CrtIcon(left+169,vy+27,26,CrtGlyph.Heart,h.Critical?critical:amber);
            CrtGauge(bx,vy+31,bw,23,h.Hp/Math.Max(1,h.HpMax),h.Critical?critical:amber);
            CrtText(bx+bw+15,vy+22,64,38,h.Hp.ToString("F0"),28,h.Critical?critical:amber);
            if(h.HasGun)
            {
                CrtIcon(left+169,vy+73,26,h.Reloading?CrtGlyph.Reload:CrtGlyph.Ammo,h.Reloading?UiThemeProfile.Cool:amber);
                CrtGauge(bx,vy+77,bw,20,h.AmmoFraction,h.Reloading?UiThemeProfile.Cool:amber);
                CrtText(bx+bw+15,vy+66,64,36,h.Reloading?h.ReloadSeconds.ToString("F1"):h.Ammo.ToString(),26);
            }
            if(h.HasDrill)
            {
                CrtIcon(left+169,vy+117,26,h.Overheated?CrtGlyph.Lock:CrtGlyph.Heat,h.Overheated?critical:frame);
                CrtGauge(bx,vy+124,bw,15,h.Heat,h.Overheated?critical:amber);
                if(h.Overheated) CrtText(bx+bw+15,vy+111,64,36,p.DrillHeatLock.ToString("F1"),24,critical);
            }
            if(h.Critical||h.Downed)
            {
                CrtIcon(left+12,vy-42,28,h.Downed?CrtGlyph.Rescue:CrtGlyph.Warning,critical);
                CrtText(left+49,vy-45,350,36,h.Downed?"구조 신호 송신 중":"생체 신호 위험",20,critical);
            }

            // Three hardware sockets. Driller E remains physically shuttered, not a fictitious skill.
            float slot=104,gap=18,sx=right-(slot*3+gap*2),sy=bottom-180;
            void Slot(int i,CrtGlyph glyph,string key,double cd,double max,bool available)
            {
                float x=sx+i*(slot+gap);var r=R(x,sy,slot,slot);
                GUI.Panel(r,UiThemeProfile.Panel,available?frame:UiThemeProfile.Dim);
                CrtIcon(x+23,sy+18,58,available?glyph:CrtGlyph.Lock,available?amber:UiThemeProfile.Dim);
                if(cd>0&&available)
                {
                    float fill=Mathf.Clamp01((float)(cd/Math.Max(.2,max)));
                    GUI.Panel(R(x+5,sy+5,slot-10,(slot-10)*fill),new Color(.03f,.018f,.007f,.86f),Color.clear,0,false);
                    for(int z=0;z<7;z++)if(z*12<(slot-10)*fill)
                        GUI.Panel(R(x+7,sy+8+z*12,slot-14,1),UiThemeProfile.Dim,Color.clear,0,false);
                    CrtText(x,sy+33,slot,38,cd.ToString("F1"),28,amber,TextAnchor.MiddleCenter);
                }
                else if(available) GUI.Panel(R(x+19,sy+slot-13,slot-38,3),amber,Color.clear,0,false);
                GUI.Panel(R(x+23,sy+slot+9,slot-46,31),new Color(.03f,.02f,.01f,.86f),available?frame:UiThemeProfile.Dim,1,false);
                CrtText(x+23,sy+slot+9,slot-46,31,key,22,available?amber:UiThemeProfile.Dim,TextAnchor.MiddleCenter);
            }
            string Key(GamepadMap.Action action,string keyboard)=>ctrl!=null&&ctrl.UsingGamepad&&Gamepad.current!=null?GamepadMap.Label(GamepadMap.Get(action)):keyboard;
            Slot(0,RoleGlyph(h.Role,true),Key(GamepadMap.Action.SkillQ,"Q"),roles.QCooldown,RoleSystem.QCooldownFor(h.Role),p.CanMove);
            bool hasE=RoleSystem.HasE(h.Role);
            Slot(1,RoleGlyph(h.Role,false),Key(GamepadMap.Action.SkillE,"E"),roles.ECooldown,Math.Max(.2,RoleSystem.ECooldownFor(h.Role)),hasE&&p.CanMove);
            Slot(2,CrtGlyph.Dash,Key(GamepadMap.Action.Dash,"␣"),p.DashCooldown,SimTuning.DashCooldown,p.CanMove);

            float xpW=Mathf.Min(300,W*.17f),xpX=W*.5f-xpW*.5f-20,xpY=bottom-30;
            GUI.Panel(R(xpX-38,xpY-7,31,31),UiThemeProfile.Panel,frame);
            CrtText(xpX-38,xpY-7,31,31,h.Level.ToString(),22,null,TextAnchor.MiddleCenter);
            CrtGauge(xpX,xpY,xpW,14,h.Xp/(double)Math.Max(1,h.XpNeed),amber,10);
            CrtText(xpX+xpW+14,xpY-9,100,32,$"{h.Xp}/{h.XpNeed}",20);

            // AI identity is localized to the small marker. Main readout uses the same amber tokens.
            float cy=top+110;
            foreach(var m in Sim.Crew.Members)
            {
                float x=right-212;
                GUI.Panel(R(x,cy,212,57),new Color(.035f,.022f,.009f,.65f),UiThemeProfile.Dim,1);
                GUI.Panel(R(x+8,cy+17,5,15),Hex(AiCrewSystem.ColorOf(m.Role)),Color.clear,0,false);
                CrtIcon(x+22,cy+8,30,m.Down?CrtGlyph.Rescue:RoleGlyph(m.Role,true),m.Down?critical:amber);
                CrtGauge(x+66,cy+15,93,11,m.Hp/Math.Max(1,m.HpMax),m.Down?critical:amber,6);
                CrtText(x+168,cy+7,38,30,m.Level.ToString(),20);
                CrtText(x+65,cy+28,140,17,m.Down?"구조 필요":m.StateLabel,18,m.Down?critical:frame);
                GUI.Panel(R(x+66,cy+49,93,3),UiThemeProfile.Dim,Color.clear,0,false);
                GUI.Panel(R(x+66,cy+49,93*Mathf.Clamp01((float)m.Xp/Math.Max(1,m.XpNeed)),3),frame,Color.clear,0,false);
                cy+=64;
            }
            if(Sim.Escape.Active)
            {
                string esc=Sim.Escape.Phase switch {
                    EscapePhase.Placing=>"탈출 지점 지정 · 클릭 확정",
                    EscapePhase.Incoming=>$"포트 도착 {Mathf.CeilToInt((float)(Sim.Escape.Need-Sim.Escape.Elapsed))}초",
                    EscapePhase.Ready=>$"탑승 {Sim.Escape.BoardProgress:P0}"+(Sim.Crew.EscapeCount() is var ec&&ec.HasValue?$" · 생존자 {ec.Value.boarded+(Sim.Escape.BoardProgress>=1?1:0)}/{ec.Value.total}":""),
                    _=>"탑승 완료"};
                CrtIcon(left,top+100,30,CrtGlyph.Signal,critical);
                CrtText(left+42,top+94,420,42,esc,22,critical);
            }
            if(_crtLastCore>=0&&(Sim.Loot.Core!=_crtLastCore||Sim.Loot.Pulp!=_crtLastPulp||Sim.Loot.Bloom!=_crtLastBloom))
            {
                _crtCoreToast=$"{Sim.Loot.Core-_crtLastCore:+0;-0;0}";
                _crtPulpToast=$"{Sim.Loot.Pulp-_crtLastPulp:+0;-0;0}";
                _crtBloomToast=$"{Sim.Loot.Bloom-_crtLastBloom:+0;-0;0}";
                _crtResourceUntil=Time.unscaledTime+2.4f;
            }
            _crtLastCore=Sim.Loot.Core;_crtLastPulp=Sim.Loot.Pulp;_crtLastBloom=Sim.Loot.Bloom;
            if(Time.unscaledTime<_crtResourceUntil)
            {
                if(_crtPulpIcon==null)_crtPulpIcon=Resources.Load<Texture2D>("UI/crafting/currency-pulp");
                if(_crtBloomIcon==null)_crtBloomIcon=Resources.Load<Texture2D>("UI/crafting/currency-bloom");
                CrtIcon(left+8,vy-81,25,CrtGlyph.Core);CrtText(left+45,vy-84,100,34,_crtCoreToast,20);
                GUI.DrawTexture(R(left+158,vy-81,25,25),_crtPulpIcon,ScaleMode.ScaleToFit);CrtText(left+195,vy-84,100,34,_crtPulpToast,20);
                GUI.DrawTexture(R(left+308,vy-81,25,25),_crtBloomIcon,ScaleMode.ScaleToFit);CrtText(left+345,vy-84,100,34,_crtBloomToast,20);
            }
        }
        // Original HUD presentation preserved for development A/B; the same live Sim and camera-space Canvas are used.
        void DrawLegacyHud(float W,float H)
        {
            var p=Sim.Player;var b=Sim.Build;var roles=Sim.Roles;
            // ── 상단 중앙: 장악도 레일 (원본 #infDomRail) — 크루 아이콘이 보스 아이콘으로 접근
            {
                float railW = 640, railH = 18, x = W * .5f - railW * .5f, y = 34;
                Panel(R(x - 70, y - 22, railW + 140, 80), .5f);
                float frac = (float)(Sim.Objective?.ProgressFraction(Sim.Run) ?? Sim.Run.Dominance / Sim.Run.DominanceTarget);
                Bar(R(x, y, railW, railH), Mathf.Clamp01(frac), new Color(.2f, .16f, .28f), new Color(.78f, .63f, 1f), new Color(.35f, .3f, .5f));
                // 크루 아이콘 — 원본 dom-crew-icon (비어 있으면 역할 배지로 대체)
                {
                    var ico = _crewRailIcon != null ? _crewRailIcon : (_badges.TryGetValue(b.Role, out var bd) ? bd : null);
                    float px = x + railW * Mathf.Clamp01(frac);
                    if (ico != null) GUI.DrawTexture(R(px - 26, y - 20, 52, 52), ico, ScaleMode.ScaleToFit);
                }
                if (_bossIcon != null)
                {
                    GUI.color = Sim.Bosses.Active ? new Color(1f, .33f, .49f) : new Color(1f, 1f, 1f, .9f);
                    GUI.DrawTexture(R(x + railW - 4, y - 30, 64, 64), _bossIcon, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                }
                string objective = Sim.Objective?.HudText(Sim.Run) ?? $"암반 장악 {Sim.Run.Dominance:P1} / {Sim.Run.DominanceTarget:P0}";
                GUI.Label(R(x - 60, y + 22, railW + 120, 30), $"<b>{objective}</b>   ·   위협 {Sim.Run.Threat:F2}   ·   {Planet.DepthLabel(Sim.Depth)}", new GUIStyle(_sSmall) { alignment = TextAnchor.MiddleCenter });
                if (Sim.Bosses.Active)
                {
                    var bb = Sim.Bosses.Boss;
                    Bar(R(x, y + 58, railW, 14), (float)bb.HpRatio, new Color(.25f, .08f, .12f), new Color(1f, .33f, .49f));
                    GUI.Label(R(x, y + 72, railW, 26), $"<color=#ff557d><b>{bb.Def.Name}</b></color>  {bb.Body.Hp:F0} / {bb.Body.HpMax:F0}   장갑 {Sim.Bosses.ArmorAlive()}", new GUIStyle(_sSmall) { alignment = TextAnchor.MiddleCenter });
                }
            }

            // ── 좌하단: 바이탈 — 초상화 · HP · 탄창 · 드릴 열
            {
                float x = 28, y = H - 200, w = 520, h = 170;
                Panel(R(x, y, w, h));
                // 원본 #infVitalsBadge 는 역할 배지(role-badge-*)를 쓴다 — 없을 때만 초상화
                if (_badges.TryGetValue(b.Role, out var vb)) GUI.DrawTexture(R(x + 12, y + 12, 146, 146), vb, ScaleMode.ScaleToFit);
                else if (_portraits.TryGetValue(b.Role, out var por)) GUI.DrawTexture(R(x + 12, y + 12, 146, 146), por, ScaleMode.ScaleToFit);
                float cx = x + 172, cw = w - 190;
                GUI.Label(R(cx, y + 10, cw, 30), $"<b>{RoleName(b.Role)}</b>  <color=#aaa>Lv {Sim.Xp.Level}</color>{(p.Downed ? "  <color=#ff6060>다운</color>" : p.StunTime > 0 ? "  <color=#ffd36e>기절</color>" : "")}", _sMid);
                Bar(R(cx, y + 46, cw, 22), (float)(p.Hp / p.HpMax), new Color(.25f, .08f, .1f), p.Hp / p.HpMax < .22 ? new Color(1f, .3f, .3f) : new Color(.95f, .45f, .45f));
                GUI.Label(R(cx, y + 44, cw, 26), $"  HP {p.Hp:F0} / {p.HpMax:F0}", _sSmall);
                if (b.RoleHasGun)
                {
                    float af = b.IsReloading ? 1f - (float)(b.ReloadLeft / b.ReloadTime) : (float)b.Ammo / b.MagSize;
                    Bar(R(cx, y + 78, cw, 16), af, new Color(.1f, .18f, .2f), b.IsReloading ? new Color(.5f, .92f, .82f) : new Color(1f, .83f, .43f));
                    GUI.Label(R(cx, y + 74, cw, 24), b.IsReloading ? $"  재장전 {b.ReloadLeft:F1}s" : $"  탄 {b.Ammo} / {b.MagSize}", _sSmall);
                }
                if (b.RoleDigMul > 0)
                {
                    Bar(R(cx, y + 104, cw, 12), (float)p.DrillHeat, new Color(.15f, .12f, .12f), p.DrillHeatLock > 0 ? new Color(1f, .3f, .2f) : Color.Lerp(new Color(.9f, .7f, .3f), new Color(1f, .35f, .2f), (float)p.DrillHeat));
                    GUI.Label(R(cx, y + 118, cw, 24), $"드릴 열 {p.DrillHeat:P0}{(p.DrillHeatLock > 0 ? $"  <color=#ff6060>과열 {p.DrillHeatLock:F1}s</color>" : "")}   예열 {p.DrillWarm:P0}", _sSmall);
                }
                GUI.Label(R(cx, y + 140, cw, 24), $"코어 <b>{Sim.Loot.Core}</b>   PULP {Sim.Loot.Pulp}   BLOOM {Sim.Loot.Bloom}", _sSmall);
            }

            // ── 우하단: 스킬 슬롯 Q / E / Space
            {
                float slot = 96, gap = 14, n = 3;
                float x = W - 28 - (slot * n + gap * (n - 1)), y = H - 200;
                Panel(R(x - 14, y, slot * n + gap * (n - 1) + 28, 170));
                void Slot(int i, string key, string name, double cd, double cdMax, bool available)
                {
                    float sx = x + i * (slot + gap), sy = y + 14;
                    var r = R(sx, sy, slot, slot);
                    GUI.color = available ? new Color(.16f, .14f, .24f) : new Color(.1f, .1f, .12f); GUI.DrawTexture(r, _white);
                    if (cd > 0 && cdMax > 0) { GUI.color = new Color(0, 0, 0, .6f); GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height * (float)Math.Min(1, cd / cdMax)), _white); }
                    GUI.color = Color.white;
                    GUI.Label(R(sx + 6, sy + 2, 40, 28), $"<b>{key}</b>", _sMid);
                    GUI.Label(R(sx, sy + slot - 30, slot, 28), cd > 0 ? $"{cd:F1}s" : available ? "준비" : "—", Sz(_sSmall, 17, TextAnchor.MiddleCenter));
                    GUI.Label(R(sx, sy + slot + 4, slot, 44), name, Sz(_sSmall, 15, TextAnchor.UpperCenter, true));
                }
                Slot(0, "Q", QName(b.Role), roles.QCooldown, RoleSystem.QCooldownFor(b.Role), true);
                Slot(1, "E", EName(b.Role), roles.ECooldown, Math.Max(0.2, RoleSystem.ECooldownFor(b.Role)), RoleSystem.HasE(b.Role));
                Slot(2, "␣", "대시", p.DashCooldown, SimTuning.DashCooldown, true);
            }

            // ── 하단 엣지: XP 바
            {
                Bar(R(0, H - 8, W, 8), (float)Sim.Xp.Xp / Math.Max(1, Sim.Xp.XpNeed), new Color(.1f, .1f, .14f), new Color(1f, .83f, .43f));
                GUI.Label(R(W * .5f - 200, H - 40, 400, 28), $"XP {Sim.Xp.Xp} / {Sim.Xp.XpNeed}", new GUIStyle(_sSmall) { alignment = TextAnchor.MiddleCenter });
            }

            // ── 좌상단: 로그 · 탈출 · 키 가이드
            {
                float x = 28, y = 28;
                Panel(R(x, y, 560, 40 + _log.Count * 26 + (Sim.Escape.Active ? 30 : 0)), .4f);
                GUI.Label(R(x + 12, y + 6, 540, 28), "WASD 이동 · 좌클릭 드릴 · 우클릭 사격 · R 재장전 · Q/E 스킬 · Space 대시 · X 탈출 · F 손전등 · G 핑 · V 위험 · C 제작 · Enter 채팅", new GUIStyle(_sSmall) { fontSize = Mathf.RoundToInt(14 * _k), normal = { textColor = new Color(.7f, .68f, .75f) } });
                float ly = y + 34;
                if (Sim.Escape.Active)
                {
                    string esc = Sim.Escape.Phase switch
                    {
                        EscapePhase.Placing => "탈출 지점 지정 중 — 좌클릭 확정 · 우클릭 취소",
                        EscapePhase.Incoming => $"탈출 포트 도착까지 {Mathf.CeilToInt((float)(Sim.Escape.Need - Sim.Escape.Elapsed))}초 — 지점을 사수하세요",
                        EscapePhase.Ready => $"탈출 포트 도착 — 탑승 {Sim.Escape.BoardProgress:P0}" + (Sim.Crew.EscapeCount() is var ec && ec.HasValue ? $"   생존자 탑승 {ec.Value.boarded + (Sim.Escape.BoardProgress >= 1 ? 1 : 0)} / {ec.Value.total}" : ""),
                        _ => "탑승 완료",
                    };
                    GUI.Label(R(x + 12, ly, 540, 28), $"<color=#ff8da8><b>{esc}</b></color>", _sSmall); ly += 30;
                }
                foreach (var line in _log) { GUI.Label(R(x + 12, ly, 540, 26), "<color=#ffd080>· " + line + "</color>", _sSmall); ly += 26; }
            }

            // ── 우상단: AI 크루 — 각 AI 가 지금 뭘 하는지 (원본 .aiHud). 플레이테스트에서 이게 제일 중요하다
            if (Sim.Crew.Members.Count > 0)
            {
                float rw = 420, rh = 44, x = W - rw - 28, y = 130;
                for (int i = 0; i < Sim.Crew.Members.Count; i++)
                {
                    var m = Sim.Crew.Members[i];
                    var col = Hex(AiCrewSystem.ColorOf(m.Role));
                    var rc = R(x, y + i * (rh + 6), rw, rh);
                    Panel(rc, .62f);
                    GUI.color = col; GUI.DrawTexture(R(x + 12, y + i * (rh + 6) + 16, 12, 12), _white); GUI.color = Color.white;
                    GUI.Label(R(x + 32, y + i * (rh + 6), 110, rh), $"<b><color=#{ColorUtility.ToHtmlStringRGB(col)}>{AiCrewSystem.NameOf(m.Role)}</color></b> <color=#ffd36e>Lv{m.Level}</color>", _sSmall);
                    float hp = Mathf.Clamp01((float)(m.Hp / m.HpMax));
                    Bar(R(x + 150, y + i * (rh + 6) + 12, 70, 10), hp, new Color(.2f, .12f, .16f), m.Down ? new Color(1f, .33f, .49f) : hp > .5f ? new Color(.5f, .92f, .82f) : hp > .25f ? new Color(1f, .83f, .43f) : new Color(1f, .55f, .66f));
                    Bar(R(x + 150, y + i * (rh + 6) + 26, 70, 5), Mathf.Clamp01((float)m.Xp / Mathf.Max(1, m.XpNeed)), new Color(.16f, .14f, .22f), new Color(.78f, .63f, 1f));
                    GUI.Label(R(x + 232, y + i * (rh + 6), rw - 244, rh), m.Down ? "<color=#ff8da8>다운 · 구조 필요</color>" : $"<color=#b8a9d4>{m.StateLabel}</color>", Sz(_sSmall, 15, TextAnchor.MiddleLeft));
                }
            }

        }

        static CrtGlyph RoleGlyph(RoleId role,bool q)=>role switch {
            RoleId.Driller=>q?CrtGlyph.Drill:CrtGlyph.Lock,
            RoleId.Gunner=>q?CrtGlyph.Shield:CrtGlyph.Detonate,
            RoleId.Scout=>q?CrtGlyph.Flare:CrtGlyph.Grapple,
            _=>q?CrtGlyph.Power:CrtGlyph.Turret };
    }
}
