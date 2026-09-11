using UnityEngine;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 폰트 — 원본 CSS 와 같은 배정. UI 전반은 Pretendard(원본 `font-family:"Pretendard","Malgun Gothic",sans-serif`),
    /// 피해 숫자는 ARCO(원본 `.dmgText{font-family:'ARCO'}`). 파일은 Resources/Fonts (Pretendard 1.3.9 OFL, ARCO).
    /// IMGUI 는 <see cref="ApplySkin"/> 로 GUI.skin.font 를 바꾸면 라벨·버튼·텍스트필드가 전부 따라온다
    /// (GUIStyle.font 가 비어 있으면 스킨 폰트를 쓴다). TextMesh 는 각자 <see cref="UI"/>/<see cref="Damage"/> 를 넣는다.
    /// </summary>
    public static class Fonts
    {
        static Font _ui, _uiBold, _damage,_mono; static bool _loaded;

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _ui = Resources.Load<Font>("Fonts/Pretendard-Regular");
            _uiBold = Resources.Load<Font>("Fonts/Pretendard-Bold");
            _damage = Resources.Load<Font>("Fonts/ARCO");
            _mono=Resources.Load<Font>("Fonts/IBMPlexMono-Medium");
            var fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_ui == null) _ui = fallback;
            if (_uiBold == null) _uiBold = _ui;
            if (_damage == null) _damage = _uiBold;
        }

        /// <summary>Pretendard Regular — 본문·HUD·메뉴.</summary>
        public static Font UI { get { Load(); return _ui; } }
        /// <summary>Pretendard Bold — TextMesh 처럼 FontStyle 합성이 약한 곳의 굵은 글자.</summary>
        public static Font UIBold { get { Load(); return _uiBold; } }
        /// <summary>ARCO — 피해 숫자 전용 (숫자·라틴만 있다. 한글 라벨에 쓰지 말 것).</summary>
        public static Font Damage { get { Load(); return _damage; } }
        /// <summary>IBM Plex Mono Medium, SIL OFL 1.1. Latin/numeric instrument readouts only.</summary>
        public static Font Mono { get { Load();return _mono!=null?_mono:_ui; } }

        /// <summary>OnGUI 첫 줄에서 호출 — 현재 스킨 폰트를 Pretendard 로.</summary>
        public static void ApplySkin()
        {
            var f = UI;
            if (f != null && GUI.skin.font != f) GUI.skin.font = f;
        }
    }
}
