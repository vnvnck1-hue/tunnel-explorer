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
            if(move!=0){_settingsFocus=(_settingsFocus+move+23)%23;_navigationFocus=true;}
            int adjust=0;
            if(Key(UnityEngine.InputSystem.Key.LeftArrow)||(pad!=null&&pad.dpad.left.wasPressedThisFrame))adjust=-1;
            if(Key(UnityEngine.InputSystem.Key.RightArrow)||(pad!=null&&pad.dpad.right.wasPressedThisFrame))adjust=1;
            if(adjust!=0)
            {
                _navigationFocus=true;
                if(_settingsFocus>=11&&_settingsFocus<=16)
                {
                    float delta=adjust*.05f;
                    switch(_settingsFocus){case 11:Strength=Mathf.Clamp01(Strength+delta);break;case 12:Scanlines=Mathf.Clamp01(Scanlines+delta);break;
                        case 13:Curvature=Mathf.Clamp01(Curvature+delta);break;case 14:Aberration=Mathf.Clamp01(Aberration+delta);break;
                        case 15:Noise=Mathf.Clamp01(Noise+delta);break;case 16:Vignette=Mathf.Clamp01(Vignette+delta);break;}
                    Revision++;
                }
                else _settingsFocus=(_settingsFocus+adjust+23)%23;
            }
            if(Key(UnityEngine.InputSystem.Key.Enter)||(pad!=null&&pad.buttonSouth.wasPressedThisFrame))
            {
                _navigationFocus=true;
                if(_settingsFocus<6)SetProfile(_settingsFocus);
                else if(_settingsFocus<11)SetCurvatureProfile(_settingsFocus-6);
                else if(_settingsFocus==17)SetEnabled(!DisplayEnabled);
                else if(_settingsFocus==18){Strength=Scanlines=Curvature=Aberration=Noise=Vignette=1;Revision++;}
                else if(_settingsFocus<22&&_settingsFocus>=19)SetAccessibility((CrtAccessibility)(_settingsFocus-19));
                else if(_settingsFocus==22)CloseSettings();
            }
            if(Mouse.current!=null&&Mouse.current.delta.ReadValue().sqrMagnitude>1)_navigationFocus=false;
        }
        void FocusFrame(float x,float y,float w,float h,int index)
        {
            if(_navigationFocus&&_settingsFocus==index)GUI.Panel(R(x-4,y-4,w+8,h+8),Color.clear,UiThemeProfile.Amber,2,false);
        }
        static readonly string[] MonitorCodes={"RGB","RF","COMP","PHOS","GLOW","MIX"};
        static readonly string[] GlassLabels={"관제관","정밀관","오락실","수신기","심층관"};
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
                    (DisplayEnabled?MonitorCodes[ProfileIndex]+" / G"+(CurvatureIndex+1):"CRT OFF")+(UsingGamepad?" · Select+Start":"   ·   F10"))&&!TextFocus)SettingsOpen=true;
                return;
            }
            float x=(W-1100)*.5f,y=100;
            GUI.Panel(R(x,y,1100,866),new Color(.032f,.02f,.009f,.98f),UiThemeProfile.Frame,2);
            Label(x+32,y+20,940,54,"DISPLAY / 효과 × 곡률",_heading);
            Label(x+32,y+74,1036,32,"↑↓ / 패드 십자키 선택 · ←→ 값 조절 · Enter / A 확인 · Esc / B 닫기",_small);
            for(int i=0;i<EffectCount;i++)
            {
                float py=y+123+i*72;
                FocusFrame(x+32,py,510,64,i);
                if(Button(x+32,py,510,64,"",ProfileIndex==i))SetProfile(i);
                Label(x+46,py+3,480,32,$"0{i+1}   {Profiles[i].title}");
                Label(x+90,py+35,435,23,Profiles[i].subtitle,_small);
            }
            Label(x+32,y+571,1036,28,"별도 유리 곡률 · 효과 F6 / 곡률 Ctrl+F6 · Shift 역순",_small);
            for(int i=0;i<CurvatureCount;i++)
            {
                float gx=x+32+i*208;
                FocusFrame(gx,y+608,198,50,6+i);
                if(Button(gx,y+608,198,50,$"G{i+1} {GlassLabels[i]}",CurvatureIndex==i))SetCurvatureProfile(i);
            }
            float dx=x+596,dy=y+138;
            Label(dx,dy,434,68,Profile.description,_description);dy+=87;
            Strength=Slider(dx,dy,"신호 효과 강도",Strength,11);dy+=56;
            Scanlines=Slider(dx,dy,"주사선",Scanlines,12);dy+=56;
            Curvature=Slider(dx,dy,"선택 곡률 배율",Curvature,13);dy+=56;
            Aberration=Slider(dx,dy,"RGB / 색 번짐",Aberration,14);dy+=56;
            Noise=Slider(dx,dy,"수신 잡음 / 지터",Noise,15);dy+=56;
            Vignette=Slider(dx,dy,"외곽 암부",Vignette,16);
            FocusFrame(x+32,y+686,240,49,17);FocusFrame(x+292,y+686,250,49,18);
            if(Button(x+32,y+686,240,49,DisplayEnabled?"● CRT 켜짐":"○ CRT 꺼짐",DisplayEnabled))SetEnabled(!DisplayEnabled);
            if(Button(x+292,y+686,250,49,"조절값 초기화")) {Strength=Scanlines=Curvature=Aberration=Noise=Vignette=1;Revision++;}
            for(int i=0;i<3;i++){FocusFrame(x+32+i*174,y+750,162,45,19+i);if(Button(x+32+i*174,y+750,162,45,AccessibilityLabels[i],(int)Accessibility==i))SetAccessibility((CrtAccessibility)i);}
            Label(x+32,y+815,750,30,Accessibility==CrtAccessibility.Photosensitive?"시간 변화형 신호 효과가 꺼집니다.":"효과 6종 × 곡률 5종 = 30개 조합 · 선택은 각각 저장됩니다.",_small);
            FocusFrame(x+828,y+791,240,47,22);
            if(Button(x+828,y+791,240,47,"저장하고 돌아가기",true))CloseSettings();
        }
    }
}
