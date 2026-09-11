using UnityEngine;
using UnityEngine.InputSystem;
using GUI = TunnelCrew.Presentation.CRT.CrtGui;

namespace TunnelCrew.Presentation.CRT
{
    public sealed partial class CRTDisplayController
    {
        float _uiScale;
        GUIStyle _label,_small,_heading;
        GUIStyle _description;
        const float MonitorButtonWidth=316,MonitorButtonHeight=36,MonitorButtonTop=12,MonitorButtonRight=24;
        bool MonitorButtonHit(Vector2 point)
        {
            point=CrtGui.GlassToContentScreen(point);
            var safe=CrtSafeArea.Calculate(Screen.width,Screen.height,Screen.safeArea,Effective.safeAreaInset);
            var matrix=CrtSafeArea.ContentMatrix(Screen.width,Screen.height,safe);
            var local=matrix.inverse.MultiplyPoint3x4(new Vector3(point.x,Screen.height-point.y,0));
            float scale=Screen.height/1080f;
            return new Rect(Screen.width-(MonitorButtonWidth+MonitorButtonRight)*scale,MonitorButtonTop*scale,
                MonitorButtonWidth*scale,MonitorButtonHeight*scale).Contains(local);
        }
        int _settingsFocus;
        bool _navigationFocus;
        void UpdateSettingsNavigation(Keyboard keyboard,Gamepad pad)
        {
            bool Key(Key key)=>keyboard!=null&&keyboard[key].wasPressedThisFrame;
            int move=0;
            if(Key(UnityEngine.InputSystem.Key.DownArrow)||Key(UnityEngine.InputSystem.Key.Tab)||(pad!=null&&pad.dpad.down.wasPressedThisFrame))move=1;
            if(Key(UnityEngine.InputSystem.Key.UpArrow)||(Key(UnityEngine.InputSystem.Key.Tab)&&keyboard.shiftKey.isPressed)||(pad!=null&&pad.dpad.up.wasPressedThisFrame))move=-1;
            if(move!=0){_settingsFocus=(_settingsFocus+move+17)%17;_navigationFocus=true;}
            int adjust=0;
            if(Key(UnityEngine.InputSystem.Key.LeftArrow)||(pad!=null&&pad.dpad.left.wasPressedThisFrame))adjust=-1;
            if(Key(UnityEngine.InputSystem.Key.RightArrow)||(pad!=null&&pad.dpad.right.wasPressedThisFrame))adjust=1;
            if(adjust!=0)
            {
                _navigationFocus=true;
                if(_settingsFocus>=5&&_settingsFocus<=10)
                {
                    float delta=adjust*.05f;
                    switch(_settingsFocus){case 5:Strength=Mathf.Clamp01(Strength+delta);break;case 6:Scanlines=Mathf.Clamp01(Scanlines+delta);break;
                        case 7:Curvature=Mathf.Clamp01(Curvature+delta);break;case 8:Aberration=Mathf.Clamp01(Aberration+delta);break;
                        case 9:Noise=Mathf.Clamp01(Noise+delta);break;case 10:Vignette=Mathf.Clamp01(Vignette+delta);break;}
                    Revision++;
                }
                else _settingsFocus=(_settingsFocus+adjust+17)%17;
            }
            if(Key(UnityEngine.InputSystem.Key.Enter)||(pad!=null&&pad.buttonSouth.wasPressedThisFrame))
            {
                _navigationFocus=true;
                if(_settingsFocus<5)SetProfile(_settingsFocus);
                else if(_settingsFocus==11)SetEnabled(!DisplayEnabled);
                else if(_settingsFocus==12){Strength=Scanlines=Curvature=Aberration=Noise=Vignette=1;Revision++;}
                else if(_settingsFocus<16&&_settingsFocus>=13)SetAccessibility((CrtAccessibility)(_settingsFocus-13));
                else if(_settingsFocus==16)CloseSettings();
            }
            if(Mouse.current!=null&&Mouse.current.delta.ReadValue().sqrMagnitude>1)_navigationFocus=false;
        }
        void FocusFrame(float x,float y,float w,float h,int index)
        {
            if(_navigationFocus&&_settingsFocus==index)GUI.Panel(R(x-4,y-4,w+8,h+8),Color.clear,UiThemeProfile.Amber,2,false);
        }
        static readonly string[] MonitorCodes={"TC-01","TC-02","TC-03","TC-04","TC-05"};
        static readonly string[] AccessibilityLabels={"표준","편안하게","광과민 배려"};
        Rect R(float x,float y,float w,float h)=>new Rect(x*_uiScale,y*_uiScale,w*_uiScale,h*_uiScale);
        void Label(float x,float y,float w,float h,string value,GUIStyle style=null)=>GUI.Label(R(x,y,w,h),value,style??_label);
        bool Button(float x,float y,float w,float h,string text,bool selected=false)
        {
            var r=R(x,y,w,h);bool hover=r.Contains(CrtPointerEvent.current.mousePosition);
            GUI.Panel(r,selected?new Color(.17f,.09f,.017f,.95f):UiThemeProfile.Panel,hover||selected?UiThemeProfile.Amber:UiThemeProfile.Dim);
            Label(x+14,y,w-28,h,text,_label);
            return hover&&CrtPointerEvent.current.type==EventType.MouseDown;
        }
        float Slider(float x,float y,string name,float value,int focus)
        {
            FocusFrame(x,y,408,54,focus);
            Label(x,y,215,28,name,_small);Label(x+340,y,68,28,$"{value:P0}",_small);
            float next=GUI.HorizontalSlider(R(x,y+33,404,20),value,0,1);
            if(next!=value)Revision++;
            return next;
        }
        public void DrawCrt()
        {
            _uiScale=Screen.height/1080f;float W=Screen.width/_uiScale;
            int size=Mathf.RoundToInt(22*_uiScale);
            if(_label==null||_label.fontSize!=size)
            {
                _label=new GUIStyle(GUI.skin.label){fontSize=size,alignment=TextAnchor.MiddleLeft};
                _small=new GUIStyle(_label){fontSize=Mathf.RoundToInt(18*_uiScale)};
                _heading=new GUIStyle(_label){fontSize=Mathf.RoundToInt(32*_uiScale),fontStyle=FontStyle.Bold};
                _description=new GUIStyle(_small){wordWrap=true};
            }
            if(!SettingsOpen)
            {
                if(Button(W-MonitorButtonWidth-MonitorButtonRight,MonitorButtonTop,MonitorButtonWidth,MonitorButtonHeight,
                    (DisplayEnabled?MonitorCodes[ProfileIndex]:"CRT OFF")+(UsingGamepad?" · Select+Start":"   ·   F10"))&&!TextFocus)SettingsOpen=true;
                return;
            }
            float x=(W-1100)*.5f,y=125;
            GUI.Panel(R(x,y,1100,814),new Color(.032f,.02f,.009f,.98f),UiThemeProfile.Frame,2);
            Label(x+32,y+20,940,54,"DISPLAY CONTROL / 관제 모니터",_heading);
            Label(x+32,y+74,1036,32,"↑↓ / 패드 십자키 선택 · ←→ 값 조절 · Enter / A 확인 · Esc / B 닫기",_small);
            for(int i=0;i<5;i++)
            {
                float py=y+135+i*89;
                FocusFrame(x+32,py,510,76,i);
                if(Button(x+32,py,510,76,"",ProfileIndex==i))SetProfile(i);
                Label(x+46,py+7,480,32,$"0{i+1}   {Profiles[i].title}");
                Label(x+90,py+43,435,23,Profiles[i].subtitle,_small);
            }
            float dx=x+596,dy=y+138;
            Label(dx,dy,434,68,Profile.description,_description);dy+=87;
            Strength=Slider(dx,dy,"전체 강도",Strength,5);dy+=66;
            Scanlines=Slider(dx,dy,"주사선",Scanlines,6);dy+=66;
            Curvature=Slider(dx,dy,"유리 곡률",Curvature,7);dy+=66;
            Aberration=Slider(dx,dy,"RGB 분리",Aberration,8);dy+=66;
            Noise=Slider(dx,dy,"신호 노이즈 / 지터",Noise,9);dy+=66;
            Vignette=Slider(dx,dy,"외곽 암부",Vignette,10);
            FocusFrame(x+32,y+620,240,49,11);FocusFrame(x+292,y+620,250,49,12);
            if(Button(x+32,y+620,240,49,DisplayEnabled?"● CRT 켜짐":"○ CRT 꺼짐",DisplayEnabled))SetEnabled(!DisplayEnabled);
            if(Button(x+292,y+620,250,49,"선택 모니터 초기값")) {Strength=Scanlines=Curvature=Aberration=Noise=Vignette=1;Revision++;}
            for(int i=0;i<3;i++){FocusFrame(x+32+i*174,y+686,162,45,13+i);if(Button(x+32+i*174,y+686,162,45,AccessibilityLabels[i],(int)Accessibility==i))SetAccessibility((CrtAccessibility)i);}
            Label(x+32,y+747,750,30,Accessibility==CrtAccessibility.Photosensitive?"화면 지터·노이즈·교차 주사·잔상·이벤트 글리치가 꺼집니다.":"모니터 종류와 접근성은 별도로 저장됩니다.",_small);
            FocusFrame(x+828,y+743,240,47,16);
            if(Button(x+828,y+743,240,47,"저장하고 돌아가기",true))CloseSettings();
        }
    }
}
