using System.IO;
using TunnelCrew.Presentation.CRT;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.EditorTools
{
    public static class BuildCrtAssets
    {
        [MenuItem("Tunnel Crew/CRT/Install display and five monitor profiles")]
        public static void Install()
        {
            const string dir = "Assets/_Project/Data/Resources/CRT";
            Directory.CreateDirectory(dir); AssetDatabase.Refresh();
            for (int i=0;i<5;i++)
            {
                var monitor=(CrtMonitor)i; string path=dir+"/CRT_"+monitor+".asset";
                var p=AssetDatabase.LoadAssetAtPath<CRTDisplayProfile>(path);
                if(p==null) { p=ScriptableObject.CreateInstance<CRTDisplayProfile>(); CRTDisplayProfile.Populate(p,monitor); AssetDatabase.CreateAsset(p,path); }
            }
            const string themePath=dir+"/UiTheme.asset";
            if(AssetDatabase.LoadAssetAtPath<UiThemeProfile>(themePath)==null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<UiThemeProfile>(),themePath);
            var theme=AssetDatabase.LoadAssetAtPath<UiThemeProfile>(themePath);
            theme.iconShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Presentation/UI/CRT/Shaders/UiPhosphor.shader");
            EditorUtility.SetDirty(theme);
            var renderer=AssetDatabase.LoadAssetAtPath<Renderer2DData>("Assets/Settings/Renderer2D.asset");
            CRTDisplayFeature feature=null;
            foreach(var f in renderer.rendererFeatures) if(f is CRTDisplayFeature crt) feature=crt;
            if(feature==null)
            {
                feature=ScriptableObject.CreateInstance<CRTDisplayFeature>(); feature.name="CRT · One final glass";
                AssetDatabase.AddObjectToAsset(feature,renderer); renderer.rendererFeatures.Add(feature);
            }
            feature.displayShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Presentation/UI/CRT/Shaders/CRTDisplay.shader");
            feature.SetActive(true); feature.Create();
            EditorUtility.SetDirty(feature); EditorUtility.SetDirty(renderer); AssetDatabase.SaveAssets();
            Debug.Log("[CRT] Five monitor profiles, UI theme, and final renderer feature installed.");
        }
    }
}
