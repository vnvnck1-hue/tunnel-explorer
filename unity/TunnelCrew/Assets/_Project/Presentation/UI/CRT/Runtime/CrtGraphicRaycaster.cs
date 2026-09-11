using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TunnelCrew.Presentation.CRT
{
    /// <summary>Native text selection uses the same glass map as presenter buttons, never world aim.</summary>
    public sealed class CrtGraphicRaycaster : GraphicRaycaster
    {
        public override void Raycast(PointerEventData data,List<RaycastResult> results)
        {
            Vector2 original=data.position;
            data.position=CrtGui.GlassToContentScreen(original);
            try { base.Raycast(data,results); }
            finally { data.position=original; }
        }
    }
}
