using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Presentation.CRT
{
    /// <summary>World lighting/LOS first, camera-space UI second, one CRT on the final stack color.
    /// The UI camera is a URP camera-stack overlay, NEVER a ScreenSpaceOverlay Canvas.</summary>
    [DefaultExecutionOrder(800)]
    public sealed class CrtCameraStack : MonoBehaviour
    {
        const int UiMask=1<<5;
        Camera _world,_ui;
        UniversalAdditionalCameraData _worldData;
        bool _restoreUiBit;
        public static Camera UiCamera { get; private set; }
        void OnEnable()=>RenderPipelineManager.beginCameraRendering+=BeforeCamera;
        void OnDisable()=>RenderPipelineManager.beginCameraRendering-=BeforeCamera;
        void BeforeCamera(ScriptableRenderContext context,Camera camera)
        {
            // URP emits camera-space Canvas geometry automatically for the main game camera only.
            // This explicit emission is required for our separate UI camera, including offscreen capture.
            if(camera==_ui)ScriptableRenderContext.EmitGeometryForCamera(camera);
        }
        void LateUpdate()
        {
            var world=Camera.main;
            if(world==null)return;
            if(_world!=world)
            {
                Detach();_world=world;_worldData=world.GetUniversalAdditionalCameraData();
                _restoreUiBit=(world.cullingMask&UiMask)!=0;
                _world.cullingMask&=~UiMask;
                var go=new GameObject("CRT · Camera-space interface",typeof(Camera));go.transform.SetParent(transform,false);
                _ui=go.GetComponent<Camera>();
                var data=_ui.GetUniversalAdditionalCameraData();data.renderType=CameraRenderType.Overlay;
                data.renderPostProcessing=false; // Overlay clearDepth defaults to true (read-only in URP 17).
                data.volumeLayerMask=0;
                _worldData.cameraStack.Add(_ui);UiCamera=_ui;
            }
            _ui.CopyFrom(_world);
            _ui.overrideSceneCullingMask=_world.overrideSceneCullingMask;
            _ui.transform.SetPositionAndRotation(_world.transform.position,_world.transform.rotation);
            _ui.cullingMask=UiMask;_ui.targetTexture=null;_ui.clearFlags=CameraClearFlags.Depth;
            _ui.enabled=true;
        }
        void Detach()
        {
            if(_worldData!=null&&_ui!=null)_worldData.cameraStack.Remove(_ui);
            if(_world!=null&&_restoreUiBit)_world.cullingMask|=UiMask;
            if(_ui!=null){_ui.targetTexture=null;Destroy(_ui.gameObject);}
            _ui=null;_world=null;_worldData=null;UiCamera=null;
        }
        void OnDestroy()=>Detach();
    }
}
