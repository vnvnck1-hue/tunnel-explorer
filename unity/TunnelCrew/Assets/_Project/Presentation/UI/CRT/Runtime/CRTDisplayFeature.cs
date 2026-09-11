using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Experimental.Rendering;

namespace TunnelCrew.Presentation.CRT
{
    /// <summary>One final glass after world and camera-space UGUI. Off/missing shader enqueues nothing.</summary>
    public sealed class CRTDisplayFeature : ScriptableRendererFeature
    {
        static readonly ProfilingSampler GlassSampler=new ProfilingSampler("TunnelCrew CRT glass");
        static readonly ProfilingSampler HistorySampler=new ProfilingSampler("TunnelCrew CRT history copy");
        public static bool Measure { get; set; }
        public static float GpuMilliseconds=>GlassSampler.gpuElapsedTime;
        public static int GpuSamples=>GlassSampler.gpuSampleCount;
        public static float HistoryGpuMilliseconds=>HistorySampler.gpuElapsedTime;
        public static int HistoryGpuSamples=>HistorySampler.gpuSampleCount;
        public static long SetupAllocationBytes { get; private set; }
        public static long GraphAllocationBytes { get; private set; }
        public static long HistoryBytes { get; private set; }
        public static GraphicsFormat HistoryFormat { get; private set; }
        public static GraphicsFormat SourceFormat { get; private set; }
        static CRTDisplayFeature _lastRenderer;
        static int _lastGlassFrame=-2;
        public static bool GlassActive=>Application.isPlaying&&_lastRenderer!=null&&_lastRenderer.isActive&&
            _lastRenderer._material!=null&&_lastRenderer._material.shader.isSupported&&_lastGlassFrame>=Time.frameCount-1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetGlassState() { _lastRenderer=null;_lastGlassFrame=-2; }
        // Only the fading afterglow is downsampled above 1440p. Current-frame glass, HUD,
        // scanlines and world retain the full output resolution on every quality setting.
        public static Vector2Int HistoryResolution(int width,int height)
        {
            int divisor=(long)width*height>2560L*1440?2:1;
            return new Vector2Int(Mathf.Max(1,(width+divisor-1)/divisor),Mathf.Max(1,(height+divisor-1)/divisor));
        }
        public Shader displayShader;
        Material _material;
        DisplayPass _pass;
        public override void Create()
        {
            _pass?.Dispose();
            CoreUtils.Destroy(_material);
            if (displayShader != null && displayShader.isSupported)
                _material = CoreUtils.CreateEngineMaterial(displayShader);
            _pass = new DisplayPass(_material);
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var controller = CRTDisplayController.Instance;
            if(!Application.isPlaying||controller==null||!controller.DisplayEnabled)_pass?.ReleaseHistory();
            // The CRT owns display grain while enabled. Modify only the evaluated per-camera stack,
            // never a VolumeProfile asset; disabling CRT restores the next normal Volume evaluation.
            if(Application.isPlaying&&controller!=null&&controller.DisplayEnabled&&_material!=null&&_material.shader.isSupported&&renderingData.cameraData.renderType==CameraRenderType.Base)
            {
                var grain=VolumeManager.instance.stack.GetComponent<FilmGrain>();
                if(grain!=null)grain.intensity.value=0;
            }
            if (!Application.isPlaying || controller == null || !controller.DisplayEnabled || _material == null ||
                !_material.shader.isSupported ||
                renderingData.cameraData.cameraType != CameraType.Game || !renderingData.cameraData.resolveFinalTarget) return;
            _lastRenderer=this;
            _pass.Setup(controller, renderingData.cameraData.camera);
            renderer.EnqueuePass(_pass);
        }
        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose(); _pass = null; CoreUtils.Destroy(_material); _material = null;
        }

        sealed class DisplayPass : ScriptableRenderPass
        {
            readonly Material _material;
            // Match URP 17.3 MakeRenderTextureGraphicsFormat's Blend capability check.
            // URP documents the separate Linear/Render checks as UUM-41070; they can reject
            // the very RGB HDR format the native pipeline already uses on this device.
            readonly GraphicsFormat _preferredHistoryFormat=
                SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32,GraphicsFormatUsage.Blend)
                ?GraphicsFormat.B10G11R11_UFloatPack32:GraphicsFormat.None;
            RTHandle _history;
            int _width, _height, _revision = -1, _cameraId;
            bool _historyValid;
            bool _useHistory;
            static readonly int Size = Shader.PropertyToID("_DisplaySize"), Glass = Shader.PropertyToID("_Glass"),
                Signal = Shader.PropertyToID("_Signal"), Beam = Shader.PropertyToID("_Beam"),
                Phosphor = Shader.PropertyToID("_Phosphor"), Transmission = Shader.PropertyToID("_Transmission"),
                Timing = Shader.PropertyToID("_Timing"), History = Shader.PropertyToID("_History");
            static readonly int Tone=Shader.PropertyToID("_Tone"),SignalTiming=Shader.PropertyToID("_SignalTiming");
            static readonly int Effect=Shader.PropertyToID("_Effect"),Spatial=Shader.PropertyToID("_Spatial");
            CrtParameters _p;
            CRTDisplayController _controller;
            public DisplayPass(Material material)
            {
                _material = material;
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }
            public void Setup(CRTDisplayController controller, Camera camera)
            {
                long before=Measure?System.GC.GetAllocatedBytesForCurrentThread():0;
                GlassSampler.enableRecording=Measure;
                HistorySampler.enableRecording=Measure;
                _controller = controller; _p = controller.Effective;
                _useHistory = _p.persistence > .001f && !controller.CaptureMode;
                if (_revision != controller.Revision || _cameraId != camera.GetInstanceID())
                { _historyValid = false; _revision = controller.Revision; _cameraId = camera.GetInstanceID(); }
                _material.SetVector(Size, new Vector4(camera.pixelWidth, camera.pixelHeight, 1f / camera.pixelWidth, 1f / camera.pixelHeight));
                _material.SetVector(Glass, new Vector4(_p.curvature, _p.vignetteStrength, _p.roundness, _p.edgeFeather));
                _material.SetVector(Signal, new Vector4(_p.aberrationPixels, _p.noiseStrength, _p.jitterStrengthPixels, controller.EventAmount));
                _material.SetVector(Beam, new Vector4(_p.scanlinePeriod, _p.scanlineStrength, _p.beamWidth, _p.interlace));
                // Frame-constant decay belongs on the CPU, not in millions of fragment pow calls.
                float decay=_useHistory?Mathf.Pow(_p.persistence,Mathf.Max(.25f,Mathf.Min(Time.unscaledDeltaTime,.1f)*60)):0;
                _material.SetVector(Phosphor, new Vector4(_p.maskStyle, _p.maskStrength, _p.phosphorBloom, decay));
                _material.SetVector(Timing, new Vector4(controller.Clock, _p.rollStrength, _p.rollSpeed, Mathf.Min(Time.unscaledDeltaTime, .1f)));
                _material.SetVector(Tone,new Vector4(_p.brightness,_p.contrast,_p.blackFloor,0));
                _material.SetVector(SignalTiming,new Vector4(_p.noiseSpeed,_p.jitterFrequency,_p.jitterBandCount,_p.scanlineSpeed));
                _material.SetVector(Effect,new Vector4(_p.chromaBleedPixels,_p.rfSnow,_p.rfTearPixels,_p.maskPitch));
                _material.SetVector(Spatial,new Vector4(_p.edgeBias,_p.afterglowSpreadPixels,0,0));
                if(Measure)SetupAllocationBytes=System.GC.GetAllocatedBytesForCurrentThread()-before;
            }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                _lastGlassFrame=Time.frameCount;
                long before=Measure?System.GC.GetAllocatedBytesForCurrentThread():0;
                var source = resources.activeColorTexture;
                var desc = graph.GetTextureDesc(source);
                SourceFormat=desc.colorFormat;
                desc.name = "CRT final glass"; desc.clearBuffer = false;
                var destination = graph.CreateTexture(desc);
                TextureHandle history = TextureHandle.nullHandle;
                if (_useHistory)
                {
                    // History stores RGB light only. The URP 32-bit HDR format preserves its range
                    // without allocating an unused alpha channel from a 64-bit capture target.
                    var format=_preferredHistoryFormat!=GraphicsFormat.None?_preferredHistoryFormat:desc.colorFormat;
                    var historySize=HistoryResolution(desc.width,desc.height);
                    if (_history == null || _width != historySize.x || _height != historySize.y||_history.rt.graphicsFormat!=format)
                    {
                        _history?.Release(); _historyValid = false;
                        _width = historySize.x; _height = historySize.y;
                        _history = RTHandles.Alloc(_width, _height, colorFormat: format,
                            filterMode: FilterMode.Bilinear, wrapMode: TextureWrapMode.Clamp, name: "CRT phosphor history");
                    }
                    history = graph.ImportTexture(_history);
                    HistoryFormat=format;HistoryBytes=(long)_width*_height*GraphicsFormatUtility.GetBlockSize(format);
                }
                else { if (_history != null) { _history.Release(); _history = null; _historyValid = false; } HistoryBytes=0; }
                _material.SetVector(Transmission, new Vector4(_p.horizontalBleed, _p.echoStrength, _p.echoOffsetPixels, _historyValid ? 1 : 0));
                using (var builder = graph.AddRasterRenderPass<PassData>("CRT / final glass", out var data,GlassSampler))
                {
                    data.source = source; data.history = history; data.material = _material;
                    data.historyValid = _historyValid && _useHistory;
                    builder.UseTexture(source, AccessFlags.Read);
                    if (data.historyValid) builder.UseTexture(history, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0);
                    builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                    {
                        if (d.historyValid) d.material.SetTexture(History, d.history);
                        else d.material.SetTexture(History, Texture2D.blackTexture);
                        Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, 0);
                    });
                }
                resources.cameraColor = destination;
                if (_useHistory)
                {
                    using(var builder=graph.AddRasterRenderPass<HistoryData>("CRT / store phosphor",out var data,HistorySampler))
                    {
                        data.source=destination;
                        builder.UseTexture(destination,AccessFlags.Read);
                        builder.SetRenderAttachment(history,0);
                        builder.SetRenderFunc((HistoryData d,RasterGraphContext ctx)=>
                            Blitter.BlitTexture(ctx.cmd,d.source,new Vector4(1,1,0,0),0,false));
                    }
                    _historyValid = true;
                }
                if(Measure)GraphAllocationBytes=System.GC.GetAllocatedBytesForCurrentThread()-before;
            }
            sealed class PassData { public TextureHandle source, history; public Material material; public bool historyValid; }
            sealed class HistoryData { public TextureHandle source; }
            public void ReleaseHistory() { _history?.Release(); _history=null;_historyValid=false;HistoryBytes=0; }
            public void Dispose() { ReleaseHistory(); }
        }
    }
}
