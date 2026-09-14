using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>Resources asset keeps the runtime shader and dressing included in player builds.</summary>
    public sealed class OrganicEnvironmentStyle : ScriptableObject
    {
        public Shader rockShader;
        public Sprite rock;
        [Tooltip("Three wall-integrated support variants. Their bottom-centre pivots sit on the floor contact while the head overlaps the wall rim.")]
        public Sprite[] wallSupports;
        [Tooltip("Three connected conduit rows, each ordered left/middle/right. These span only long south-facing wall runs and terminate at the integrated supports.")]
        public Sprite[] wallConduits;
        [Tooltip("Three conduit families, each ordered left turn/vertical repeat/right turn. They continue a service facade around real rock-to-floor side boundaries.")]
        public Sprite[] wallJunctions;
        [Tooltip("Cyan, magenta, and amber floor-integrated service pylons used by the authored presentation-room lamps.")]
        public Sprite[] servicePylons;
        [Range(0f, 1f)] public float macroVariation = 1f;

        [Header("Primary-match macro surfaces")]
        [Tooltip("World-anchored surface paintings. When assigned, the organic renderer uses the full texture instead of sampling the centre of a legacy tile.")]
        public Texture2D floorMacro;
        public Texture2D wallTopMacro;
        public Texture2D wallFrontMacro;
        public Texture2D wallRimMacro;
        [Header("Primary-match aligned channels")]
        public Texture2D floorNormal;
        public Texture2D floorAo;
        public Texture2D floorEmission;
        public Texture2D wallTopNormal;
        public Texture2D wallTopAo;
        public Texture2D wallTopEmission;
        public Texture2D wallFrontNormal;
        public Texture2D wallFrontAo;
        public Texture2D wallFrontEmission;
        public Texture2D wallRimNormal;
        public Texture2D wallRimAo;
        public Texture2D wallRimEmission;
        [Tooltip("Legacy fallback when a surface-specific size is zero.")]
        [Min(1f)] public float macroSizeCells = 12f;
        [Min(1f)] public float floorMacroSizeCells = 12f;
        [Min(1f)] public float wallTopMacroSizeCells = 9f;
        [Min(1f)] public float wallFrontMacroSizeCells = 6f;
        [Min(1f)] public float wallRimMacroSizeCells = 8f;
        [Range(0f, 1f)] public float floorMinLight = 0.42f;
        [Range(0f, 1f)] public float wallTopMinLight = 0.20f;
        [Range(0f, 1f)] public float wallFrontMinLight = 0.28f;
        [Range(0f, 1f)] public float wallRimMinLight = 0.28f;
        [Range(0f, 2f)] public float floorAccentEmission = 0.42f;
        [Range(0f, 2f)] public float wallAccentEmission = 0.30f;
        [Range(0f, 1f)] public float floorAoStrength = 0.48f;
        [Range(0f, 1f)] public float wallAoStrength = 0.62f;
        [Header("Albedo-derived relief")]
        [Range(0f, 3f)] public float floorDerivedNormalStrength = 0.65f;
        [Range(0f, 3f)] public float wallTopDerivedNormalStrength = 1.00f;
        [Range(0f, 3f)] public float wallFrontDerivedNormalStrength = 1.45f;
        [Range(0f, 3f)] public float wallRimDerivedNormalStrength = 1.10f;
        [Tooltip("Floor v3 is authored for direct repeat; keep this off to avoid mirrored inkblot patterns.")]
        public bool floorMirrorMacro;
        [Tooltip("Mirrors alternating macro blocks so opposite texture edges meet without a visible grid seam.")]
        public bool mirrorMacro = true;
    }
}
