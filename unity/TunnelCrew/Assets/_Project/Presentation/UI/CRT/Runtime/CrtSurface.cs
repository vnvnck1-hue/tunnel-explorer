using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TunnelCrew.Presentation.CRT
{
    public interface ICrtScreen { void DrawCrt(); }

    /// <summary>A retained UGUI canvas, updated by a presenter. No IMGUI camera bypass or screen capture.</summary>
    [DefaultExecutionOrder(900)]
    public sealed class CrtSurface : MonoBehaviour
    {
        sealed class Element
        {
            public RectTransform rect;
            public RawImage image;
            public Text text;
            public InputField input;
            public CrtShape shape;
            public CrtIcon icon;
            public CanvasGroup opacity;
            public int lastFrame=-2,lastKind=-1;
        }
        readonly List<Element> _elements = new List<Element>();
        readonly Stack<RectTransform> _parents = new Stack<RectTransform>();
        readonly List<RectTransform> _groups = new List<RectTransform>();
        readonly Dictionary<Transform,int> _siblings = new Dictionary<Transform,int>();
        Canvas _canvas;
        MonoBehaviour _owner;
        ICrtScreen _presenter;
        int _used, _groupsUsed;
        int _order;
        RectTransform _root;
        public bool InsetContent;
        public static CrtSurface Current { get; private set; }
        public static bool LegacyHud { get; set; }
        public bool Interactive => _owner is CRTDisplayController || CRTDisplayController.Instance == null || !CRTDisplayController.Instance.ConsumesInput;

        public static CrtSurface Register(MonoBehaviour owner, int order, bool inset = false)
        {
            var go = new GameObject(owner.GetType().Name + " · CRT Canvas", typeof(RectTransform));
            go.layer=5;
            go.transform.SetParent(owner.transform, false);
            var s = go.AddComponent<CrtSurface>(); s._owner = owner; s._presenter = (ICrtScreen)owner;
            s._order=20000+order;
            s._root = go.GetComponent<RectTransform>();
            s._canvas = go.AddComponent<Canvas>();
            s._canvas.renderMode = RenderMode.ScreenSpaceCamera; s._canvas.planeDistance = .5f;
            s._canvas.overrideSorting = true;
            s._canvas.sortingLayerID = SortingLayer.NameToID("UI"); s._canvas.sortingOrder = 20000 + order;
            go.AddComponent<CrtGraphicRaycaster>(); s.InsetContent = inset;
            if (EventSystem.current == null)
                new GameObject("CRT UI Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            return s;
        }
        void LateUpdate()
        {
            if (_owner == null) { Destroy(gameObject); return; }
            var camera = CrtCameraStack.UiCamera!=null?CrtCameraStack.UiCamera:Camera.main;
            bool visible = _owner.isActiveAndEnabled && camera != null;
            if (_canvas.enabled != visible) _canvas.enabled = visible;
            if (!visible) return;
            if (_canvas.worldCamera != camera)
            {
                _canvas.worldCamera = camera;
                // Assigning the first camera resets some Canvas sorting state in Unity 6.
                _canvas.sortingLayerID=SortingLayer.NameToID("UI");
                _canvas.sortingOrder=_order;
            }
            _used = _groupsUsed = 0; _parents.Clear(); _parents.Push(_root); _siblings.Clear();
            Current = this; CrtGui.Begin(InsetContent); CrtPointerEvent.Begin(Interactive);
            try { _presenter.DrawCrt(); }
            finally
            {
                float fade=FadeStep;
                for (int i = _used; i < _elements.Count; i++)
                {
                    var e=_elements[i];if(!e.rect.gameObject.activeSelf)continue;
                    e.opacity.alpha=Mathf.MoveTowards(e.opacity.alpha,0,fade);
                    if(e.opacity.alpha<=0)e.rect.gameObject.SetActive(false);
                }
                for (int i = _groupsUsed; i < _groups.Count; i++) if (_groups[i].gameObject.activeSelf) _groups[i].gameObject.SetActive(false);
                Current = null;
            }
        }
        static float FadeStep=>CRTDisplayController.Instance?.Accessibility==CrtAccessibility.Photosensitive?1:
            Time.unscaledDeltaTime/(UiThemeProfile.Active!=null?UiThemeProfile.Active.revealSeconds:.14f);

        Element Get(int kind)
        {
            if (_used == _elements.Count)
            {
                var go = new GameObject("Element " + _used, typeof(RectTransform));
                go.layer=5;
                _elements.Add(new Element { rect = go.GetComponent<RectTransform>(),opacity=go.AddComponent<CanvasGroup>() });
            }
            var e = _elements[_used++];
            if(e.lastFrame!=Time.frameCount-1||e.lastKind!=kind)e.opacity.alpha=0;
            e.opacity.alpha=Mathf.MoveTowards(e.opacity.alpha,1,FadeStep);e.lastFrame=Time.frameCount;e.lastKind=kind;
            if (!e.rect.gameObject.activeSelf) e.rect.gameObject.SetActive(true);
            if (e.rect.parent != _parents.Peek()) e.rect.SetParent(_parents.Peek(), false);
            Order(e.rect);
            if (e.image != null) e.image.enabled = kind == 0;
            if (e.text != null) e.text.enabled = kind == 1;
            if (e.input != null) { e.input.enabled = kind == 2; e.input.textComponent.gameObject.SetActive(kind == 2); }
            if (e.shape != null) e.shape.enabled = kind == 3;
            if (e.icon != null) e.icon.enabled = kind == 4;
            if (kind == 0 && e.image == null) { e.image = AddGraphic<RawImage>(e.rect); e.image.raycastTarget = false; }
            if (kind == 1 && e.text == null)
            {
                e.text = AddGraphic<Text>(e.rect); e.text.raycastTarget = false;
                e.text.font = Fonts.UI; e.text.supportRichText = true;
                e.text.verticalOverflow = VerticalWrapMode.Overflow;
            }
            if (kind == 3 && e.shape == null) { e.shape = AddGraphic<CrtShape>(e.rect); e.shape.raycastTarget = false; }
            if (kind == 4 && e.icon == null) { e.icon = AddGraphic<CrtIcon>(e.rect); e.icon.raycastTarget = false; }
            return e;
        }
        static T AddGraphic<T>(RectTransform parent) where T : Graphic
        {
            var go = new GameObject(typeof(T).Name, typeof(RectTransform), typeof(CanvasRenderer));
            go.layer=5;
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go.AddComponent<T>();
        }
        void Order(RectTransform rt)
        {
            var parent=rt.parent;
            _siblings.TryGetValue(parent,out int next);
            if(rt.GetSiblingIndex()!=next)rt.SetSiblingIndex(next);
            _siblings[parent]=next+1;
        }
        static void Position(RectTransform rt, Rect r)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(r.x, -r.y); rt.sizeDelta = r.size;
            rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity;
        }
        public void Image(Rect r, Texture tex, Color tint, Rect uv, float rotation = 0)
        {
            if (tex == null || r.width <= 0 || r.height <= 0 || tint.a <= 0) return;
            var e = Get(0); Position(e.rect, r);
            e.rect.localRotation = Quaternion.Euler(0, 0, -rotation);
            e.image.texture = tex; e.image.color = tint; e.image.uvRect = uv;
            e.image.material=!CrtGui.OriginalPalette&&UiThemeProfile.IsIcon(tex)?UiThemeProfile.IconMaterial:null;
        }
        public void Label(Rect r, string value, GUIStyle style, Color tint)
        {
            if (string.IsNullOrEmpty(value) || r.width <= 0 || r.height <= 0 || tint.a <= 0) return;
            var e = Get(1); Position(e.rect, r);
            e.text.text = UiThemeProfile.ThemeText(value);
            bool instrument=!CrtGui.OriginalPalette&&value.Length<=32;
            if(instrument)foreach(char ch in value)if(ch>126||ch=='<'){instrument=false;break;}
            e.text.font = instrument?Fonts.Mono:(style.font ?? Fonts.UI);
            e.text.fontStyle = style.fontStyle;
            float body=UiThemeProfile.Active!=null?UiThemeProfile.Active.bodySize:18;
            e.text.fontSize = CrtGui.OriginalPalette?style.fontSize:Mathf.Max(Mathf.RoundToInt(body * Screen.height / 1080f), style.fontSize);
            e.text.alignment = style.alignment;
            e.text.horizontalOverflow = style.wordWrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            e.text.color = tint;
        }
        public void Panel(Rect r, Color fill, Color line, float width = 1.5f, bool cut = true)
        {
            if (r.width <= 0 || r.height <= 0) return;
            var e = Get(3); Position(e.rect, r); e.shape.Set(fill, line, width, cut);
        }
        public void Icon(Rect r, CrtGlyph glyph, Color tint)
        {
            var e=Get(4);Position(e.rect,r);e.icon.Set(glyph,tint);
        }
        public void BeginGroup(Rect r)
        {
            if (_groupsUsed == _groups.Count)
            {
                var go = new GameObject("Clipped group", typeof(RectTransform), typeof(RectMask2D));
                go.layer=5;
                _groups.Add(go.GetComponent<RectTransform>());
            }
            var rt = _groups[_groupsUsed++]; rt.gameObject.SetActive(true);
            if(rt.parent!=_parents.Peek())rt.SetParent(_parents.Peek(), false);
            Order(rt); Position(rt, r); _parents.Push(rt);
        }
        public void EndGroup() { if (_parents.Count > 1) _parents.Pop(); }

        public string TextField(Rect r, string value, int maxLength, GUIStyle style, string control)
        {
            var e = Get(2); Position(e.rect, r);
            if (e.input == null)
            {
                e.input = e.rect.gameObject.AddComponent<CrtInputField>();
                var go = new GameObject("Editable text", typeof(RectTransform), typeof(Text));
                go.layer=5;
                var tr = go.GetComponent<RectTransform>(); tr.SetParent(e.rect, false);
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
                var text = go.GetComponent<Text>(); text.font = Fonts.UI; text.color = UiThemeProfile.Amber;
                text.supportRichText = false; text.raycastTarget = true;
                e.input.textComponent = text; e.input.customCaretColor = true; e.input.caretColor = UiThemeProfile.Amber;
                e.input.selectionColor = new Color(.9f, .65f, .2f, .25f);
                e.input.lineType = InputField.LineType.SingleLine;
            }
            e.rect.name = control;
            e.input.interactable = Interactive;
            if (!Interactive && e.input.isFocused) e.input.DeactivateInputField();
            e.input.characterLimit = maxLength; e.input.textComponent.fontSize = Mathf.RoundToInt(20 * Screen.height / 1080f);
            if (!e.input.isFocused) e.input.SetTextWithoutNotify(value);
            CrtGui.LastInput = e.input;
            return e.input.text;
        }
    }

    /// <summary>Geometry-native terminal panels stay crisp at any screen size; no raster borders.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CrtShape : MaskableGraphic
    {
        Color _fill, _line; float _width; bool _cut;
        readonly Vector2[] _points=new Vector2[6];
        public void Set(Color fill, Color line, float width, bool cut)
        {
            if (_fill == fill && _line == line && _width == width && _cut == cut) return;
            _fill = fill; _line = line; _width = width; _cut = cut; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Rect r = rectTransform.rect;
            float c = _cut ? Mathf.Min(12 * Screen.height / 1080f, Mathf.Min(r.width, r.height) * .16f) : 0;
            var points=_points;
            points[0]=new Vector2(r.xMin+c,r.yMax);points[1]=new Vector2(r.xMax,r.yMax);
            points[2]=new Vector2(r.xMax,r.yMin+c);points[3]=new Vector2(r.xMax-c,r.yMin);
            points[4]=new Vector2(r.xMin,r.yMin);points[5]=new Vector2(r.xMin,r.yMax-c);
            vh.AddVert(r.center, _fill, Vector2.zero);
            for (int i = 0; i < 6; i++) vh.AddVert(points[i], _fill, Vector2.zero);
            for (int i = 0; i < 6; i++) vh.AddTriangle(0, i + 1, (i + 1) % 6 + 1);
            for (int i = 0; i < 6; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % 6]; Vector2 n = new Vector2(b.y-a.y, a.x-b.x).normalized * _width;
                int k = vh.currentVertCount;
                vh.AddVert(a, _line, Vector2.zero); vh.AddVert(b, _line, Vector2.zero);
                vh.AddVert(b+n, _line, Vector2.zero); vh.AddVert(a+n, _line, Vector2.zero);
                vh.AddTriangle(k,k+1,k+2); vh.AddTriangle(k,k+2,k+3);
            }
        }
    }
}
