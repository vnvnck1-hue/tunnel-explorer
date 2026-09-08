using UnityEngine;
using UnityEngine.InputSystem;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 플레이 중에 투영 프리셋을 바꿔 손맛·입체감을 눈으로 비교하는 도구.
    /// F9 다음 / F8 이전 / F7 라벨 숨기기. 고른 값은 PlayerPrefs 에 남아 다음 실행에도 유지된다.
    ///
    /// 시뮬레이션에는 손대지 않는다 — 바뀌는 것은 화면 좌표뿐이라
    /// 이동·충돌·길찾기·리플레이는 프리셋과 무관하게 같은 결과를 낸다.
    /// </summary>
    public sealed class ProjectionSwitcher : MonoBehaviour
    {
        const string PrefKey = "tc.projectionPreset";

        bool _showLabel = true;
        GUIStyle _style;

        void Awake()
        {
            // 저장된 프리셋 복원. 열거형 범위를 벗어난 값은 기본값으로 떨군다.
            int saved = PlayerPrefs.GetInt(PrefKey, (int)ProjectionPreset.Dimetric2To1);
            if (System.Enum.IsDefined(typeof(ProjectionPreset), saved))
                IsometricProjection.SetPreset((ProjectionPreset)saved);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f9Key.wasPressedThisFrame) Apply(IsometricProjection.Next(1));
            else if (kb.f8Key.wasPressedThisFrame) Apply(IsometricProjection.Next(-1));
            else if (kb.f7Key.wasPressedThisFrame) _showLabel = !_showLabel;
        }

        void Apply(ProjectionPreset p)
        {
            IsometricProjection.SetPreset(p);
            PlayerPrefs.SetInt(PrefKey, (int)p);
            _showLabel = true;
        }

        void OnGUI()
        {
            if (!_showLabel) return;

            float k = Screen.height / 1080f;
            _style ??= new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.UpperRight };
            _style.fontSize = Mathf.RoundToInt(16 * k);

            var r = new Rect(Screen.width - 700f * k, 12f * k, 688f * k, 60f * k);
            GUI.color = new Color(.05f, .04f, .09f, .55f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x, r.y + 4f * k, r.width - 10f * k, 26f * k),
                $"<b>투영 {(int)IsometricProjection.Preset + 1}/{IsometricProjection.PresetCount}</b>  {IsometricProjection.Label(IsometricProjection.Preset)}", _style);
            GUI.Label(new Rect(r.x, r.y + 30f * k, r.width - 10f * k, 26f * k),
                "<color=#aaa>F9 다음 · F8 이전 · F7 이 표시 숨기기</color>", _style);
        }
    }
}
