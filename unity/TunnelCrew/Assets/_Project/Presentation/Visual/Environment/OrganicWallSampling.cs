using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>Bounded, world-addressed boundary samples. Only the four incident cells are read,
    /// so independently rebuilt chunks produce identical seam vertices. Simulation is never modified.</summary>
    public static class OrganicWallSampling
    {
        public const int Subdivision = 4;
        public const float MaxOffset = .18f;

        public static Vector2 Point(ISolidField field, float x, float y)
        {
            var p = new Vector2(x, y);
            bool vx = Mathf.Abs(x - Mathf.Round(x)) < .001f;
            bool vy = Mathf.Abs(y - Mathf.Round(y)) < .001f;
            if (!vx && !vy) return p;
            int left = Mathf.FloorToInt(x - .001f), right = Mathf.FloorToInt(x + .001f);
            int bottom = Mathf.FloorToInt(y - .001f), top = Mathf.FloorToInt(y + .001f);
            // Boss walls keep their authored rectangular silhouette and overlays.
            if (field.IsBossWallAt(left, bottom) || field.IsBossWallAt(right, bottom)
                || field.IsBossWallAt(left, top) || field.IsBossWallAt(right, top)) return p;
            bool sw = field.IsSolid(left, bottom), se = field.IsSolid(right, bottom);
            bool nw = field.IsSolid(left, top), ne = field.IsSolid(right, top);
            int count = (sw ? 1 : 0) + (se ? 1 : 0) + (nw ? 1 : 0) + (ne ? 1 : 0);
            if (count == 0 || count == 4) return p;
            var inward = new Vector2((se ? 1 : 0) + (ne ? 1 : 0) - (sw ? 1 : 0) - (nw ? 1 : 0),
                (nw ? 1 : 0) + (ne ? 1 : 0) - (sw ? 1 : 0) - (se ? 1 : 0));
            // Diagonally touching islands share an unchanged saddle point.
            if (inward.sqrMagnitude < .001f) return p;
            float noise = Mathf.Sin(x * 2.73f + y * 1.37f) * .075f
                        + Mathf.Sin(x * 5.19f - y * 3.41f) * .035f;
            Vector2 rounding = vx && vy && count != 2
                ? inward * (count == 1 ? .1125f : -.1125f) : Vector2.zero;
            return p + Vector2.ClampMagnitude(rounding + inward.normalized * noise, MaxOffset);
        }
    }
}
