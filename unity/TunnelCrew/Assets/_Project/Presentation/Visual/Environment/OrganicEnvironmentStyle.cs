using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>Resources asset keeps the runtime shader and dressing included in player builds.</summary>
    public sealed class OrganicEnvironmentStyle : ScriptableObject
    {
        public Shader rockShader;
        public Sprite rock;
        [Range(0f, 1f)] public float macroVariation = 1f;
    }
}
