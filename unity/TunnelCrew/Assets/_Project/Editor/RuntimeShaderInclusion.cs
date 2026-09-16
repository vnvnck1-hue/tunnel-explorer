using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TunnelCrew.EditorTools
{
    /// <summary>
    /// 런타임에 <see cref="Shader.Find"/> 로만 찾는 셰이더를 <b>Always Included Shaders</b> 에 넣는다.
    ///
    /// 왜 필요한가: 이 셰이더들은 머티리얼 <i>에셋</i> 이 참조하지 않으므로 빌드가 "아무도 안 쓴다" 로
    /// 판단해 스트립한다. 그러면 에디터에서는 멀쩡한데 플레이어에서만
    /// <c>new Material(null)</c> → <c>ArgumentNullException</c> 으로 죽는다.
    ///
    /// 2026-09-16 스탠드얼론 빌드 검증(3d-perspective-production-plan §4 8단계)에서 실제로 잡혔다 —
    /// <c>OrganicEnvironmentRenderer.Initialize</c> 가 <c>Tunnel Crew/WorldLit</c> 을 못 찾아
    /// <c>RunBootstrap.Start</c> 에서 런 시작이 통째로 실패했다. 원근 작업과 무관한 기존 결함이다.
    ///
    /// 멱등이다 — 이미 들어 있으면 아무것도 하지 않는다.
    /// </summary>
    [InitializeOnLoad]
    public static class RuntimeShaderInclusion
    {
        /// <summary>
        /// 런타임 <c>Shader.Find</c> 대상 전부. 새 셰이더를 그렇게 찾기 시작하면 여기에 더한다.
        /// </summary>
        static readonly string[] Required =
        {
            // 월드·캐릭터 재질 (SurfaceMaterialSet · OrganicEnvironmentRenderer · 타일맵)
            "Tunnel Crew/WorldLit",
            "Tunnel Crew/CharacterLit",
            "Tunnel Crew/Tilemap-Lit-Normal",
            "Tunnel Crew/Organic Rock",
            // 전투 연출
            "Tunnel Crew/Projectile-Energy",
            "Tunnel Crew/Projectile-Trail",
            "Tunnel Crew/Projectile-Smoke-Trail",
            // 화면 효과
            "TunnelCrew/Darkness",
            "TunnelCrew/OccludedSilhouette",
            "TunnelCrew/Atmosphere",
            // 원근 월드
            "Tunnel Crew/PerspectiveWorld",
            "Tunnel Crew/PerspectiveBillboard",
            "Tunnel Crew/PerspectiveSilhouette",
            "Tunnel Crew/PerspectiveOverlay",
            // URP 기본 — 오버레이·스프라이트 경로가 이름으로 찾는다
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/2D/Sprite-Lit-Default",
            "Universal Render Pipeline/2D/Sprite-Unlit-Default",
        };

        static RuntimeShaderInclusion() => EditorApplication.delayCall += Ensure;

        [MenuItem("Tunnel Crew/런타임 셰이더 빌드 포함 확인")]
        public static void Ensure()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            Object asset = settings;
            if (asset == null)
            {
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset"))
                    if (o != null && o.GetType().Name == "GraphicsSettings") asset = o;
            }
            if (asset == null) return;

            var serialized = new SerializedObject(asset);
            var list = serialized.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return;

            var present = new HashSet<string>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var shader = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (shader != null) present.Add(shader.name);
            }

            var added = new List<string>();
            foreach (var name in Required)
            {
                if (present.Contains(name)) continue;
                var shader = Shader.Find(name);
                if (shader == null) continue;   // 아직 임포트 전이면 다음 로드에서 다시 본다
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                added.Add(name);
            }

            if (added.Count == 0) return;
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log($"[빌드] Always Included Shaders 에 추가: {string.Join(", ", added)}");
        }
    }
}
