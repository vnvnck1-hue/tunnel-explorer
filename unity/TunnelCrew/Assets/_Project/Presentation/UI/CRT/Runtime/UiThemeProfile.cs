using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TunnelCrew.Presentation.CRT
{
    [CreateAssetMenu(menuName = "Tunnel Crew/CRT/UI theme")]
    public sealed class UiThemeProfile : ScriptableObject
    {
        public Color phosphor=new Color(1,.8392f,.3529f),frame=new Color(.91f,.663f,.18f),dim=new Color(.478f,.290f,.0745f);
        public Color panel=new Color(.0706f,.0471f,.0275f,.88f),critical=new Color(1,.416f,.239f),cool=new Color(.439f,.843f,.765f);
        public static Color Amber=>Active!=null?Active.phosphor:new Color(1,.8392f,.3529f);
        public static Color Frame=>Active!=null?Active.frame:new Color(.91f,.663f,.18f);
        public static Color Dim=>Active!=null?Active.dim:new Color(.478f,.290f,.0745f);
        public static Color Panel=>Active!=null?Active.panel:new Color(.0706f,.0471f,.0275f,.88f);
        public static Color Critical=>Active!=null?Active.critical:new Color(1,.416f,.239f);
        public static Color Cool=>Active!=null?Active.cool:new Color(.439f,.843f,.765f);
        public float bodySize = 18, numericSize = 24, borderWidth = 1.5f, padding = 16;
        [Range(.1f,.16f)] public float revealSeconds=.14f;
        public Shader iconShader;
        static UiThemeProfile _active;
        public static UiThemeProfile Active => _active!=null?_active:(_active=Resources.Load<UiThemeProfile>("CRT/UiTheme"));
        static Material _iconMaterial;
        static Color _materialTint;
        static readonly Dictionary<Texture,bool> IconKinds=new Dictionary<Texture,bool>();
        public static Material IconMaterial
        {
            get
            {
                if(_iconMaterial!=null)
                {
                    if(_materialTint!=Amber){_materialTint=Amber;_iconMaterial.SetColor("_Color",_materialTint);}
                    return _iconMaterial;
                }
                var theme=Active;
                if(theme==null||theme.iconShader==null||!theme.iconShader.isSupported)return null;
                _iconMaterial=new Material(theme.iconShader){hideFlags=HideFlags.HideAndDontSave};
                _materialTint=Amber;_iconMaterial.SetColor("_Color",_materialTint);
                return _iconMaterial;
            }
        }
        public static bool IsIcon(Texture texture)
        {
            if(texture==null)return false;
            if(IconKinds.TryGetValue(texture,out bool cached))return cached;
            string n=texture.name;
            bool result=n.StartsWith("badge-")||n.StartsWith("portrait-")||n.StartsWith("trait-icon-")||
                n.StartsWith("icon-")||n.StartsWith("currency-")||n.StartsWith("dom-");
            IconKinds[texture]=result;return result;
        }
        public static void ReleaseRuntimeMaterial()
        {
            if(_iconMaterial!=null)Object.Destroy(_iconMaterial);
            _iconMaterial=null;
            IconKinds.Clear();Cache.Clear();
        }
        static readonly Regex Colors = new Regex("<color=#[0-9a-fA-F]{3,8}>", RegexOptions.Compiled);
        static readonly Dictionary<string, string> Cache = new Dictionary<string, string>();
        public static string ThemeText(string text)
        {
            if(CrtGui.OriginalPalette)return text;
            if (!text.Contains("<color=")) return text;
            if (Cache.TryGetValue(text, out var result)) return result;
            result = Colors.Replace(text, match => {
                string hex = match.Value.Substring(8, match.Value.Length - 9);
                if (hex.Length == 3) hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
                ColorUtility.TryParseHtmlString("#" + hex, out var col);
                return "<color=#" + ColorUtility.ToHtmlStringRGB(Map(col, true)) + ">";
            });
            if (Cache.Count > 512) Cache.Clear(); Cache[text] = result; return result;
        }
        public static Color Map(Color c, bool text = false)
        {
            if(CrtGui.OriginalPalette)return c;
            float value = Mathf.Max(c.r, c.g, c.b);
            Color result;
            if (value < .035f) result = Color.black;
            else if (!text && value < .3f) result = Color.Lerp(new Color(.025f,.017f,.009f), Panel, value / .3f);
            else if (c.r > c.g * 1.6f && c.r > c.b * 1.15f && c.r > .65f) result = Critical;
            else if (c.g > c.r * 1.3f && c.b > c.r * 1.2f) result = Cool;
            else result = Color.Lerp(text ? Frame : Dim, Amber, Mathf.InverseLerp(.2f, 1, value));
            result.a = c.a; return result;
        }
    }
}
