using System;
using TunnelCrew.Presentation.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.EditorTools
{
    public static class BuildBackgroundUpgradeLab
    {
        public const string ScenePath="Assets/_Project/Scenes/BackgroundUpgradeLab.unity";
        const string Data="Assets/_Project/Data/Visual/";
        const string Art="Assets/Art/Visual/TestRoomV01/";
        static T Load<T>(string path) where T:UnityEngine.Object
        {
            var asset=AssetDatabase.LoadAssetAtPath<T>(path);
            if(asset==null)throw new InvalidOperationException("Background lab asset missing: "+path);
            return asset;
        }
        [MenuItem("Tunnel Crew/Background Upgrade Lab/Create or Open")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before opening the lab.");
            // Never discard another scene's unsaved edits.
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save the currently edited scene before opening the lab.");
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null)
            {EditorSceneManager.OpenScene(ScenePath);return;}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Background Upgrade Lab");
            var lab=root.AddComponent<BackgroundUpgradeLab>();
            lab.kit=Load<EnvironmentKit>(Data+"EnvironmentKit_ReferenceV1.asset");
            lab.floorKit=Load<EnvironmentKit>(Data+"EnvironmentKit_TestRoomV01.asset");
            lab.profile=Load<WorldVisualProfile>(Data+"WorldVisualProfile_Stratum1.asset");
            lab.rules=Load<SurfaceRuleSet>(Data+"SurfaceRuleSet_Stratum1.asset");
            lab.floorSet=Load<SurfaceMaterialSet>(Data+"SurfaceMaterialSet_TestRoom_floor.asset");
            lab.capSet=Load<SurfaceMaterialSet>(Data+"SurfaceMaterialSet_Reference_walltop.asset");
            lab.frontSet=Load<SurfaceMaterialSet>(Data+"SurfaceMaterialSet_Reference_wallfront.asset");
            lab.rockShader=Load<Shader>("Assets/_Project/Shaders/BackgroundLabRock.shader");
            lab.rock=Load<Sprite>(Art+"tr01_dec_rock_a_albedo.png");
            lab.crystal=Load<Sprite>("Assets/Art/Visual/ReferenceCalibrationV1/tr01_reference_crystal_a_albedo.png");
            lab.lamp=Load<Sprite>("Assets/Art/Visual/ReferenceCalibrationV1/tr01_reference_lamp_a_albedo.png");
            lab.pipe=Load<Sprite>(Art+"tr01_pipe_elbow_a_albedo.png");
            lab.beam=Load<Sprite>(Art+"tr01_dec_beams_a_albedo.png");
            lab.rail=Load<Sprite>(Art+"tr01_rail_straight_a_albedo.png");
            lab.foreground=Load<Sprite>(Art+"tr01_foreground_rock_lip_a_albedo.png");
            lab.fog=Load<Sprite>(Art+"tr01_vfx_fog_a_albedo.png");
            lab.crystalMaterial=Load<Material>(Data+"LabMaterials/LabLit_tr01_reference_crystal_a_albedo.mat");
            lab.lampMaterial=Load<Material>(Data+"LabMaterials/LabLit_tr01_reference_lamp_a_albedo.mat");
            var camera=new GameObject("Lab Camera");camera.tag="MainCamera";
            lab.labCamera=camera.AddComponent<Camera>();lab.labCamera.orthographic=true;
            lab.labCamera.orthographicSize=6.5f;lab.labCamera.backgroundColor=new Color(.025f,.028f,.045f);
            lab.labCamera.clearFlags=CameraClearFlags.SolidColor;camera.transform.position=new Vector3(12.5f,9,-20);
            camera.AddComponent<AudioListener>();camera.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=true;
            // Renderer2D uses the global Light2D built by the lab. Keep a conventional main light for scene tooling.
            var sun=new GameObject("Main Light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=.5f;
            var volume=new GameObject("Lab Volume").AddComponent<Volume>();volume.isGlobal=true;
            var volumeProfile=ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom=volumeProfile.Add<Bloom>();bloom.intensity.Override(.28f);bloom.threshold.Override(1.1f);
            AssetDatabase.CreateAsset(volumeProfile,Data+"BackgroundUpgradeLabVolume.asset");volume.sharedProfile=volumeProfile;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Background Lab] Ready: "+ScenePath+". Play, then WASD to explore; 1/2/3 compare.");
        }

        [MenuItem("Tunnel Crew/Background Upgrade Lab/Verify Playable Scene")]
        public static void Verify()
        {
            Run();
            SessionState.SetBool("BackgroundLab.RunSmoke",true);
            EditorApplication.isPlaying=true;
        }
    }
}
