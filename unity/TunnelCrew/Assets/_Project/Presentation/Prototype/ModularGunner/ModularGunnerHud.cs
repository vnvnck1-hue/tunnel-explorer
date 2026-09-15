using UnityEngine;

namespace TunnelCrew.Presentation.Prototype
{
    public sealed class ModularGunnerHud : MonoBehaviour
    {
        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _button;
        ModularGunnerArena _arena;

        void OnGUI()
        {
            _title ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.9f, 0.35f) }
            };
            _body ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.86f, 0.86f, 0.86f) }
            };
            _button ??= new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                hover = { textColor = new Color(1f, 0.88f, 0.32f) },
            };

            GUI.Box(new Rect(14, 14, 310, 70), GUIContent.none);
            GUI.Label(new Rect(26, 20, 280, 24), "MODULAR GUNNER PROTOTYPE", _title);
            GUI.Label(new Rect(26, 48, 285, 22), "WASD / Arrows: Move   Mouse: Aim   LMB / Space: Fire", _body);

            if (_arena == null) _arena = FindFirstObjectByType<ModularGunnerArena>();
            if (_arena != null)
            {
                string label = _arena.ContinuousSpawn ? "■ AUTO SPAWN" : "▶ AUTO SPAWN";
                if (GUI.Button(new Rect(14, 92, 132, 28), label, _button)) _arena.ToggleContinuousSpawn();
                GUI.Label(new Rect(154, 96, 150, 22), $"ENEMIES {_arena.AliveEnemies}", _body);
            }
        }
    }
}
