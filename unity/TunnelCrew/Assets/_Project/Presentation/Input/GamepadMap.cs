using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 게임패드 버튼 리매핑 (M7 잔여 항목). 기본 매핑은 계획 §M7 표 그대로:
    /// RT 사격 · LT/RB 드릴 · A 대시 · X 재장전 · LB Q · Y E · D↓ 탈출 · D↑ 손전등 · Start 일시정지.
    /// 스틱(이동·조준)과 메뉴 내비(A 확인 · B 뒤로)는 고정이다.
    /// 저장: PlayerPrefs "tc.gp.&lt;action&gt;" = GamepadButton 정수. 없으면 기본값.
    /// </summary>
    public static class GamepadMap
    {
        public enum Action { Fire, Drill, DrillAlt, Dash, Reload, SkillQ, SkillE, Escape, Flashlight, Pause }

        public static readonly Action[] All = (Action[])Enum.GetValues(typeof(Action));

        static readonly GamepadButton[] Defaults =
        {
            GamepadButton.RightTrigger,   // Fire
            GamepadButton.LeftTrigger,    // Drill
            GamepadButton.RightShoulder,  // DrillAlt
            GamepadButton.South,          // Dash
            GamepadButton.West,           // Reload
            GamepadButton.LeftShoulder,   // SkillQ
            GamepadButton.North,          // SkillE
            GamepadButton.DpadDown,       // Escape
            GamepadButton.DpadUp,         // Flashlight
            GamepadButton.Start,          // Pause
        };

        static readonly GamepadButton[] _map = new GamepadButton[Defaults.Length];
        static bool _loaded;

        /// <summary>리매핑 UI 에서 고를 수 있는 버튼들 (스틱 클릭 제외).</summary>
        public static readonly GamepadButton[] Assignable =
        {
            GamepadButton.South, GamepadButton.East, GamepadButton.West, GamepadButton.North,
            GamepadButton.LeftShoulder, GamepadButton.RightShoulder, GamepadButton.LeftTrigger, GamepadButton.RightTrigger,
            GamepadButton.DpadUp, GamepadButton.DpadDown, GamepadButton.DpadLeft, GamepadButton.DpadRight,
            GamepadButton.Start, GamepadButton.Select, GamepadButton.LeftStick, GamepadButton.RightStick,
        };

        public static string Label(Action a) => a switch
        {
            Action.Fire => "사격", Action.Drill => "드릴", Action.DrillAlt => "드릴 (보조)", Action.Dash => "대시",
            Action.Reload => "재장전", Action.SkillQ => "스킬 Q", Action.SkillE => "스킬 E", Action.Escape => "탈출 요청",
            Action.Flashlight => "손전등", Action.Pause => "일시정지", _ => a.ToString(),
        };

        /// <summary>Xbox 표기. South/East/West/North 는 A/B/X/Y.</summary>
        public static string Label(GamepadButton b) => b switch
        {
            GamepadButton.South => "A", GamepadButton.East => "B", GamepadButton.West => "X", GamepadButton.North => "Y",
            GamepadButton.LeftShoulder => "LB", GamepadButton.RightShoulder => "RB", GamepadButton.LeftTrigger => "LT", GamepadButton.RightTrigger => "RT",
            GamepadButton.DpadUp => "D↑", GamepadButton.DpadDown => "D↓", GamepadButton.DpadLeft => "D←", GamepadButton.DpadRight => "D→",
            GamepadButton.Start => "Start", GamepadButton.Select => "Select", GamepadButton.LeftStick => "LS 클릭", GamepadButton.RightStick => "RS 클릭",
            _ => b.ToString(),
        };

        static void EnsureLoaded()
        {
            if (_loaded) return;
            for (int i = 0; i < Defaults.Length; i++)
            {
                int v = PlayerPrefs.GetInt("tc.gp." + All[i], (int)Defaults[i]);
                _map[i] = Enum.IsDefined(typeof(GamepadButton), v) ? (GamepadButton)v : Defaults[i];
            }
            _loaded = true;
        }

        public static GamepadButton Get(Action a) { EnsureLoaded(); return _map[(int)a]; }
        public static GamepadButton Default(Action a) => Defaults[(int)a];
        public static bool IsDefault(Action a) => Get(a) == Default(a);

        public static void Set(Action a, GamepadButton b)
        {
            EnsureLoaded();
            _map[(int)a] = b;
            PlayerPrefs.SetInt("tc.gp." + a, (int)b);
        }

        public static void ResetAll()
        {
            for (int i = 0; i < Defaults.Length; i++) { _map[i] = Defaults[i]; PlayerPrefs.DeleteKey("tc.gp." + All[i]); }
            _loaded = true;
        }

        public static void Save() => PlayerPrefs.Save();

        /// <summary>액션에 묶인 버튼 컨트롤. 트리거는 축이라 ButtonControl 로 읽으면 pressPoint 기준으로 판정된다.</summary>
        public static ButtonControl Control(Gamepad gp, Action a) => gp[Get(a)];

        /// <summary>트리거는 원본처럼 .4 이상을 눌림으로 본다. 나머지 버튼은 isPressed.</summary>
        public static bool Held(Gamepad gp, Action a)
        {
            var b = Get(a); var c = gp[b];
            if (b == GamepadButton.LeftTrigger || b == GamepadButton.RightTrigger) return c.ReadValue() > .4f;
            return c.isPressed;
        }

        public static bool Pressed(Gamepad gp, Action a) => gp[Get(a)].wasPressedThisFrame;

        /// <summary>이번 프레임에 눌린 배정 가능 버튼 하나 (리매핑 캡처용). 없으면 null.</summary>
        public static GamepadButton? AnyPressed(Gamepad gp)
        {
            if (gp == null) return null;
            foreach (var b in Assignable) if (gp[b].wasPressedThisFrame) return b;
            return null;
        }
    }
}
