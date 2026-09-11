using UnityEngine;

namespace TunnelCrew.Presentation.CRT
{
    public static class CrtSafeArea
    {
        public static Matrix4x4 ContentMatrix(int width,int height,Rect area)
        {
            float scale=Mathf.Min(area.width/width,area.height/height);
            return Matrix4x4.TRS(new Vector3(area.center.x-width*scale*.5f,
                height-area.center.y-height*scale*.5f,0),Quaternion.identity,new Vector3(scale,scale,1));
        }
        /// <summary>Pixel rectangle in bottom-left screen coordinates. No inverse-warped HUD.</summary>
        public static Rect Calculate(int width, int height, Rect osArea, float inset)
        {
            float padding = Mathf.Min(width, height) * Mathf.Clamp(inset, .028f, .09f);
            float x0 = Mathf.Max(padding, osArea.xMin), y0 = Mathf.Max(padding, osArea.yMin);
            float x1 = Mathf.Min(width - padding, osArea.xMax), y1 = Mathf.Min(height - padding, osArea.yMax);
            return Rect.MinMaxRect(x0, y0, Mathf.Max(x0, x1), Mathf.Max(y0, y1));
        }
    }
}
