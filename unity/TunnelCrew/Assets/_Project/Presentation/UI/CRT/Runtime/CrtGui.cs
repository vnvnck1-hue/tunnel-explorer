using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TunnelCrew.Presentation.CRT
{
    /// <summary>
    /// Migration draw vocabulary for existing presenters. All output is pooled camera-space UGUI;
    /// no UnityEngine.GUI calls, OnGUI events, frame readbacks, or overlay canvases are used here.
    /// </summary>
    public static class CrtGui
    {
        public static Color color = Color.white;
        public static bool OriginalPalette;
        public static Matrix4x4 matrix = Matrix4x4.identity;
        public static InputField LastInput;
        static GUISkin _skin;
        static string _controlName;
        static readonly Stack<Matrix4x4> Matrices = new Stack<Matrix4x4>();
        static readonly Stack<Vector2> Origins = new Stack<Vector2>();
        public static Vector2 GroupOrigin;
        static Matrix4x4 _inputMatrix;
        public static GUISkin skin
        {
            get
            {
                if (_skin != null) return _skin;
                _skin = ScriptableObject.CreateInstance<GUISkin>(); _skin.hideFlags = HideFlags.DontSave;
                _skin.font = Fonts.UI;
                _skin.label = new GUIStyle { font = Fonts.UI, fontSize = 22, richText = true };
                _skin.label.normal.textColor = UiThemeProfile.Amber;
                _skin.textField = new GUIStyle(_skin.label);
                return _skin;
            }
        }
        public static void Begin(bool inset)
        {
            OriginalPalette=false;color = Color.white; matrix = Matrix4x4.identity; Matrices.Clear(); Origins.Clear(); GroupOrigin = Vector2.zero;
            if (inset)
            {
                var c = CRTDisplayController.Instance;
                var area = CrtSafeArea.Calculate(Screen.width, Screen.height, Screen.safeArea, c != null ? c.Effective.safeAreaInset : .055f);
                matrix = CrtSafeArea.ContentMatrix(Screen.width,Screen.height,area);
            }
            _inputMatrix = matrix;
        }
        public static Vector2 GlassToContentScreen(Vector2 screen)
        {
            // Only UI hit testing follows glass UVs. World aiming and collision coordinates are untouched.
            var c = CRTDisplayController.Instance;
            if (c != null && c.DisplayEnabled && CRTDisplayFeature.GlassActive)
            {
                var p = screen / new Vector2(Screen.width, Screen.height) * 2 - Vector2.one;
                float aspect = Screen.width / (float)Screen.height;
                Vector2 shape = p * new Vector2(Mathf.Min(aspect / (16f/9f),1.3f),1);
                p *= Vector2.one + new Vector2(shape.y*shape.y, shape.x*shape.x) * c.Effective.curvature;
                screen = (p + Vector2.one) * .5f * new Vector2(Screen.width, Screen.height);
            }
            return screen;
        }
        public static Vector2 Pointer(Vector2 screen)
        {
            screen=GlassToContentScreen(screen);
            var gui = new Vector3(screen.x, Screen.height - screen.y, 0);
            return (Vector2)_inputMatrix.inverse.MultiplyPoint3x4(gui) - GroupOrigin;
        }
        static Rect Transform(Rect r)
        {
            var origin = matrix.MultiplyPoint3x4(new Vector3(r.x,r.y,0));
            return new Rect(origin.x, origin.y, r.width * matrix.GetColumn(0).magnitude, r.height * matrix.GetColumn(1).magnitude);
        }
        public static void DrawTexture(Rect r, Texture tex, ScaleMode mode = ScaleMode.StretchToFill)
        {
            if (tex == null) return;
            var uv = new Rect(0,0,1,1);
            float target = r.width / Mathf.Max(1,r.height), source = tex.width / (float)tex.height;
            if (mode == ScaleMode.ScaleToFit)
            {
                if (source > target) { float h = r.width/source; r.y += (r.height-h)*.5f; r.height=h; }
                else { float w=r.height*source; r.x+=(r.width-w)*.5f; r.width=w; }
            }
            else if (mode == ScaleMode.ScaleAndCrop)
            {
                if (source > target) { uv.width=target/source; uv.x=(1-uv.width)*.5f; }
                else { uv.height=source/target; uv.y=(1-uv.height)*.5f; }
            }
            DrawTextureWithTexCoords(r,tex,uv);
        }
        public static void DrawTextureWithTexCoords(Rect r, Texture tex, Rect uv)
        {
            var transformed = Transform(r);
            bool solid=tex!=null&&(tex==Texture2D.whiteTexture||(tex.width<=2&&tex.height<=2));
            var tint = solid ? UiThemeProfile.Map(color) : color;
            float angle=Mathf.Atan2(matrix.m10,matrix.m00)*Mathf.Rad2Deg;
            if (!OriginalPalette && solid && r.width > 100 && r.height > 36 && Mathf.Max(color.r,color.g,color.b) < .3f && color.a > .35f)
            {
                tint.a=Mathf.Max(tint.a,UiThemeProfile.Panel.a);
                CrtSurface.Current?.Panel(transformed,tint,new Color(.55f,.31f,.07f,color.a*.65f),1.2f*Screen.height/1080f);
            }
            else CrtSurface.Current?.Image(transformed,tex,tint,uv,angle);
        }
        public static void Label(Rect r, string text, GUIStyle style)
        {
            var c=UiThemeProfile.Map(style.normal.textColor,true)*color;
            CrtSurface.Current?.Label(Transform(r),text,style,c);
        }
        public static void Panel(Rect r, Color fill, Color line, float width=1.5f, bool cut=true)
            => CrtSurface.Current?.Panel(Transform(r),fill,line,width*Screen.height/1080f*(UiThemeProfile.Active!=null?UiThemeProfile.Active.borderWidth/1.5f:1),cut);
        public static void BeginGroup(Rect r)
        {
            CrtSurface.Current.BeginGroup(Transform(r));
            Matrices.Push(matrix); Origins.Push(GroupOrigin); GroupOrigin += r.position;
            matrix.m03=matrix.m13=0;
        }
        public static void EndGroup()
        {
            CrtSurface.Current.EndGroup(); matrix=Matrices.Pop(); GroupOrigin=Origins.Pop();
        }
        public static float HorizontalSlider(Rect r,float value,float min,float max)
        {
            var ev=CrtPointerEvent.current;
            if (CrtSurface.Current.Interactive && r.Contains(ev.mousePosition) && Mouse.current != null && Mouse.current.leftButton.isPressed)
                value=Mathf.Lerp(min,max,Mathf.Clamp01((ev.mousePosition.x-r.x)/r.width));
            Panel(new Rect(r.x,r.center.y-2,r.width,4),UiThemeProfile.Dim,Color.clear,0,false);
            float frac=Mathf.InverseLerp(min,max,value);
            Panel(new Rect(r.x,r.center.y-2,r.width*frac,4),UiThemeProfile.Amber,Color.clear,0,false);
            Panel(new Rect(r.x+frac*r.width-5,r.y,10,r.height),UiThemeProfile.Amber,UiThemeProfile.Amber,1,false);
            return value;
        }
        public static void SetNextControlName(string name)=>_controlName=name;
        public static bool TryConsumeTextSubmit(string name,out string value)
        {
            value=null;
            return LastInput!=null&&LastInput is CrtInputField field&&field.name==name&&field.TryConsumeSubmit(out value);
        }
        public static string GetNameOfFocusedControl()=>LastInput!=null&&LastInput.isFocused?LastInput.name:"";
        public static void FocusControl(string name)
        {
            if(LastInput==null) return;
            if(string.IsNullOrEmpty(name)) LastInput.DeactivateInputField();
            else if(LastInput.name==name) LastInput.ActivateInputField();
        }
        public static string TextField(Rect r,string value,int maxLength,GUIStyle style)
            => CrtSurface.Current.TextField(Transform(r),value,maxLength,style,_controlName);
    }
    public static class CrtGuiUtility
    {
        public static void RotateAroundPivot(float degrees,Vector2 pivot)
            => CrtGui.matrix *= Matrix4x4.Translate(pivot) * Matrix4x4.Rotate(Quaternion.Euler(0,0,degrees)) * Matrix4x4.Translate(-pivot);
        public static void ScaleAroundPivot(Vector2 scale,Vector2 pivot)
            => CrtGui.matrix *= Matrix4x4.Translate(pivot) * Matrix4x4.Scale(new Vector3(scale.x,scale.y,1)) * Matrix4x4.Translate(-pivot);
    }
    public static class CrtLayout
    {
        struct Area { public float width,y; }
        static readonly Stack<Area> Areas=new Stack<Area>();
        public static void BeginArea(Rect r) { CrtGui.BeginGroup(r); Areas.Push(new Area {width=r.width}); }
        public static void EndArea() { Areas.Pop(); CrtGui.EndGroup(); }
        public static void Space(float px) { var a=Areas.Pop(); a.y+=px; Areas.Push(a); }
        public static void Label(string text,GUIStyle style)
        {
            var a=Areas.Pop(); if(style.font==null)style.font=Fonts.UI;
            float h=Mathf.Max(style.fontSize*1.35f,style.CalcHeight(new GUIContent(text),a.width));
            CrtGui.Label(new Rect(0,a.y,a.width,h),text,style); a.y+=h+4*Screen.height/1080f; Areas.Push(a);
        }
    }
    public sealed class CrtPointerEvent
    {
        static readonly CrtPointerEvent Instance=new CrtPointerEvent();
        public static CrtPointerEvent current=>Instance;
        public EventType type; public KeyCode keyCode; public int button,clickCount;
        public Vector2 delta;
        public Vector2 mousePosition=>CrtGui.Pointer(Mouse.current!=null?Mouse.current.position.ReadValue():Vector2.zero);
        public void Use()=>type=EventType.Used;
        public static void Begin(bool interactive = true)
        {
            var e=Instance; var m=Mouse.current; var k=Keyboard.current;
            e.type=EventType.Repaint; e.button=0; e.keyCode=KeyCode.None;
            if (!interactive) { e.type=EventType.Used; e.delta=Vector2.zero; return; }
            if(m!=null)
            {
                e.clickCount=m.clickCount.ReadValue(); e.delta=m.scroll.ReadValue()/120;
                if(m.leftButton.wasPressedThisFrame)e.type=EventType.MouseDown;
                else if(m.leftButton.wasReleasedThisFrame)e.type=EventType.MouseUp;
                else if(m.leftButton.isPressed&&m.delta.ReadValue()!=Vector2.zero)e.type=EventType.MouseDrag;
                else if(e.delta!=Vector2.zero)e.type=EventType.ScrollWheel;
            }
            if(k!=null)
            {
                if(k.enterKey.wasPressedThisFrame||k.numpadEnterKey.wasPressedThisFrame){e.type=EventType.KeyDown;e.keyCode=KeyCode.Return;}
                else if(k.escapeKey.wasPressedThisFrame){e.type=EventType.KeyDown;e.keyCode=KeyCode.Escape;}
            }
        }
    }
}
