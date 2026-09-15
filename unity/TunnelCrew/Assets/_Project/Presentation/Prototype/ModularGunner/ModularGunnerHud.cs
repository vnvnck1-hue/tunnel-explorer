using UnityEngine;

namespace TunnelCrew.Presentation.Prototype
{
    public sealed class ModularGunnerHud : MonoBehaviour
    {
        GUIStyle _title;
        GUIStyle _body;

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

            GUI.Box(new Rect(14, 14, 310, 70), GUIContent.none);
            GUI.Label(new Rect(26, 20, 280, 24), "MODULAR GUNNER PROTOTYPE", _title);
            GUI.Label(new Rect(26, 48, 285, 22), "WASD / Arrows: Move   Mouse: Aim   LMB / Space: Fire", _body);
        }
    }
}
