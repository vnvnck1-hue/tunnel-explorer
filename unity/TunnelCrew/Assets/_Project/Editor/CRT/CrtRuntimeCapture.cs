using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using TunnelCrew.Presentation;
using TunnelCrew.Presentation.CRT;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using TunnelCrew.Sim;

namespace TunnelCrew.EditorTools
{
    /// <summary>Isolated play-mode smoke capture. Run in a QA project with a distinct product name.
    /// Unity -executeMethod TunnelCrew.EditorTools.CrtRuntimeCapture.Run -crtCaptureAutoExit (normal Editor, no -quit).
    /// This captures the real scene and camera-space UI through the actual URP renderer.</summary>
    [InitializeOnLoad]
    public static class CrtRuntimeCapture
    {
        const string Key = "TunnelCrew.CrtCapture";
        static int _step, _errors;
        static double _next, _deadline;
        static string _output;
        static RenderTexture _target;
        static Camera _camera;
        static CRTDisplayFeature _feature;
        static Shader _originalShader;
        static int _width,_height,_profileFrames,_lastProfile;
        static Report _report;
        static int _studyIndex;
        static bool _studyMotion;
        static Vector3 _studyCameraOrigin;
        static readonly Dictionary<string,List<float>> Gpu=new Dictionary<string,List<float>>();
        [Serializable] sealed class CaptureRecord { public string name;public int width,height,amberPixels;public float meanLuma; }
        [Serializable] sealed class TimingRecord { public string monitor;public int samples;public float medianMs,p95Ms; }
        [Serializable] sealed class Report
        {
            public string gpu,unity,renderApi,quality,captureMode,pipelineHdrPrecision,captureFormat,crtSourceFormat,historyFormat;
            public float renderScale;
            public bool cameraHdr,preserveFramebufferAlpha;
            public List<CaptureRecord> captures=new List<CaptureRecord>();
            public List<TimingRecord> timings=new List<TimingRecord>();
            public List<string> errors=new List<string>(),editorIssues=new List<string>();
            public long maxSetupAllocationBytes,maxGraphAllocationBytes,maxHistoryBytes;
        }
        static int Argument(string key,int fallback)
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);
            return i>=0&&i+1<args.Length&&int.TryParse(args[i+1],out int value)?value:fallback;
        }
        static CrtRuntimeCapture()
        {
            EditorApplication.playModeStateChanged += OnPlay;
            if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
        }
        public static void Run()
        {
            if (!PlayerSettings.productName.Contains("CRT Validation"))
                throw new InvalidOperationException("Use the isolated CRT Validation project; never write QA progress to the player's save.");
            BuildCrtAssets.Install();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Run.unity");
            int w=Argument("-crtWidth",1920),h=Argument("-crtHeight",1080);
            SessionState.SetInt(Key+"Width",w);SessionState.SetInt(Key+"Height",h);SetSize(w,h);
            SessionState.SetBool(Key,true);
            EditorApplication.isPlaying=true;
        }
        static void OnPlay(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key,false)) return;
            if (state==PlayModeStateChange.EnteredPlayMode)
            {
                _step=_errors=0; _next=EditorApplication.timeSinceStartup+4; _deadline=_next+240;
                _width=SessionState.GetInt(Key+"Width",1920);_height=SessionState.GetInt(Key+"Height",1080);
                _output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/ui-crt/img/runtime"));
                if(Argument("-crtStudies",0)==1)_output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/ui-crt/img/runtime-six-effects"));
                if(_width!=1920||_height!=1080)_output=Path.Combine(_output,$"{_width}x{_height}");
                Directory.CreateDirectory(_output);
                _report=new Report{gpu=SystemInfo.graphicsDeviceName,unity=Application.unityVersion,renderApi=SystemInfo.graphicsDeviceType.ToString()};Gpu.Clear();_lastProfile=-1;
                int high=Array.IndexOf(QualitySettings.names,"High");
                if(high>=0)QualitySettings.SetQualityLevel(high,true);
                _report.quality=QualitySettings.names[QualitySettings.GetQualityLevel()];
                CRTDisplayFeature.Measure=true;RenderPipelineManager.endContextRendering+=Sample;
                _studyIndex=0;_studyMotion=false;RenderPipelineManager.beginContextRendering+=StudyMotion;
                Application.logMessageReceived+=Log;
                EditorApplication.update-=Tick; EditorApplication.update+=Tick;
            }
            if (state==PlayModeStateChange.EnteredEditMode) Finish();
        }
        static void Log(string message,string stack,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            bool cancelledPackageWindow=message.StartsWith("[Package Manager Window]")&&message.Contains("Operation cancelled");
            if(stack.Contains("UnityEditor.Search.SearchDatabase")||cancelledPackageWindow)
            {_report.editorIssues.Add(message+"\n"+stack);return;}
            _errors++;_report.errors.Add(message+"\n"+stack);
        }
        static void Sample(ScriptableRenderContext context,List<Camera> cameras)
        {
            if(_target==null||(_feature!=null&&(!_feature.isActive||_feature.displayShader==null)))return;
            var c=CRTDisplayController.Instance;if(c==null||!c.DisplayEnabled||c.Accessibility!=CrtAccessibility.Standard)return;
            if(_lastProfile!=c.ProfileIndex){_lastProfile=c.ProfileIndex;_profileFrames=0;}
            if(++_profileFrames<30)return;
            _report.maxSetupAllocationBytes=Math.Max(_report.maxSetupAllocationBytes,CRTDisplayFeature.SetupAllocationBytes);
            _report.maxGraphAllocationBytes=Math.Max(_report.maxGraphAllocationBytes,CRTDisplayFeature.GraphAllocationBytes);
            _report.crtSourceFormat=CRTDisplayFeature.SourceFormat.ToString();
            if(CRTDisplayFeature.HistoryBytes>_report.maxHistoryBytes)
            {
                _report.maxHistoryBytes=CRTDisplayFeature.HistoryBytes;
                _report.historyFormat=CRTDisplayFeature.HistoryFormat.ToString();
            }
            if(CRTDisplayFeature.GpuSamples==0)return;
            string key=c.Profile.effect+(c.CaptureMode?" static":" temporal");
            if(!Gpu.TryGetValue(key,out var values)){values=new List<float>(2048);Gpu.Add(key,values);}
            values.Add(CRTDisplayFeature.GpuMilliseconds);
            if(!c.CaptureMode&&c.Effective.persistence>.001f&&CRTDisplayFeature.HistoryGpuSamples>0)
            {
                string copyKey=c.Profile.effect+" history copy";
                if(!Gpu.TryGetValue(copyKey,out var copyValues)){copyValues=new List<float>(2048);Gpu.Add(copyKey,copyValues);}
                copyValues.Add(CRTDisplayFeature.HistoryGpuMilliseconds);
            }
        }
        static bool FreezeReady(RunBootstrap run)
        {
            // PNG encoding/editor stalls are not simulation time. Never freeze an uncomputed LOS buffer.
            if(!run.RunActive||!run.Sim.Los.HasComputed||run.Sim.RunTime<.5){Time.timeScale=1;_step--;return false;}
            Time.timeScale=0;return true;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.isPaused)return;
            if(_deadline==0)return;
            if(EditorApplication.timeSinceStartup>_deadline){Debug.LogError("[CRT QA] Capture timeout");Stop();return;}
            if(EditorApplication.timeSinceStartup<_next)return;
            _next=EditorApplication.timeSinceStartup+1;
            try
            {
                var c=CRTDisplayController.Instance;
                var meta=UnityEngine.Object.FindFirstObjectByType<MetaScreens>();
                var run=UnityEngine.Object.FindFirstObjectByType<RunBootstrap>();
                if(c==null||meta==null||run==null)throw new InvalidOperationException("Runtime bootstrap missing");
                if(Argument("-crtStudies",0)==1&&_step>=4)
                {
                    TickStudies(c);_next=Math.Max(_next,EditorApplication.timeSinceStartup+1);return;
                }
                switch(_step++)
                {
                    case 0:
                        c.SetCaptureMode(true);c.SetAccessibility(CrtAccessibility.Standard);c.SetProfile(0);c.SetCurvatureProfile(0);c.SetEnabled(true);
                        c.Strength=c.Scanlines=c.Curvature=c.Noise=c.Aberration=c.Vignette=1;
                        _camera=Camera.main; _target=CreateCaptureTarget();
                        _report.captureFormat=_target.graphicsFormat.ToString();_report.cameraHdr=_camera.allowHDR;
                        _report.renderScale=(GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset)?.renderScale??1;
                        _camera.targetTexture=_target;
                        Debug.Log($"[CRT QA] scene culling: camera={_camera.overrideSceneCullingMask}, runtime scene={EditorSceneManager.GetSceneCullingMask(_camera.gameObject.scene)}");
                        _camera.overrideSceneCullingMask=ulong.MaxValue;
                        _camera.gameObject.AddComponent<AudioListener>();
                        Debug.Log($"[CRT QA] screen={Screen.width}x{Screen.height}, target={_width}x{_height}");break;
                    case 1: Capture("title-surveyor");meta.Show(MetaScreens.Screen.RoleSelect);break;
                    case 2: Capture("role-select-surveyor");meta.Show(MetaScreens.Screen.Run);break;
                    case 3: FreezeReady(run);break;
                    case 4: Capture("run-01-surveyor");c.SetProfile(1);break;
                    case 5: Capture("run-02-broadcast");c.SetProfile(2);break;
                    case 6: Capture("run-03-arcade");c.SetProfile(3);break;
                    case 7: Capture("run-04-relay");c.SetProfile(4);break;
                    case 8: Capture("run-05-abyss");c.SetEnabled(false);break;
                    case 9: Capture("run-00-off");c.SetEnabled(true);c.SetProfile(0);c.SettingsOpen=true;break;
                    case 10:Capture("crt-settings");c.SettingsOpen=false;c.SetAccessibility(CrtAccessibility.Photosensitive);break;
                    case 11:Capture("run-photosensitive");meta.Show(MetaScreens.Screen.Settings);break;
                    case 12:Capture("game-settings");c.SetAccessibility(CrtAccessibility.Standard);meta.Show(MetaScreens.Screen.Run);break;
                    case 13:if(FreezeReady(run))run.Sim.Craft.Open();break;
                    case 14:Capture("craft-wheel");run.Sim.Craft.Close(true);
                        var team=UnityEngine.Object.FindFirstObjectByType<TeamOverlay>();
                        typeof(TeamOverlay).GetField("_draft",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(team,"심층 관제실, 크루 신호 정상입니다.");
                        typeof(TeamOverlay).GetMethod("OpenChat",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(team,null);break;
                    case 15:Capture("chat-korean");
                        typeof(TeamOverlay).GetMethod("CloseChat",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(UnityEngine.Object.FindFirstObjectByType<TeamOverlay>(),new object[]{true});
                        run.Sim.Traits.OpenLevel(new TraitContext{Build=run.Sim.Build,Player=run.Sim.Player,Roles=run.Sim.Roles},4);break;
                    case 16:Capture("trait-choice");run.Sim.PickTrait(0);run.Sim.Bosses.Spawn(run.Sim.Player,run.Sim.Depth);break;
                    case 17:Capture("boss-intro");UnityEngine.Object.FindFirstObjectByType<BossIntroCinematic>()?.Stop();break;
                    case 18:Capture("boss-rail");run.Sim.EndRun(false,"CRT 검증 원정 종료");break;
                    case 19:Capture("run-result");meta.Show(MetaScreens.Screen.Settlement);break;
                    case 20:Capture("settlement");meta.Show(MetaScreens.Screen.Run);break;
                    case 21:run.LaunchRun(RoleId.Gunner);break;
                    case 22:if(!FreezeReady(run))break;Capture("role-gunner");run.LaunchRun(RoleId.Scout);break;
                    case 23:if(!FreezeReady(run))break;Capture("role-scout");run.LaunchRun(RoleId.Engineer);break;
                    case 24:if(!FreezeReady(run))break;Capture("role-engineer");run.Sim.Player.Hp=run.Sim.Player.HpMax*.15;run.Sim.Roles.UseQ(run.Sim.Player,run.Sim.Build,run.Sim.Depth);run.Sim.Roles.UseE(run.Sim.Player,run.Sim.Build);break;
                    case 25:Capture("critical-cooldowns");c.SetProfile(4);c.SetCaptureMode(false);_next+=3;break;
                    case 26:Capture("abyss-temporal");
                        if(CRTDisplayFeature.HistoryBytes<=0)throw new InvalidOperationException("Temporal Abyss did not allocate its history buffer");
                        c.SetEnabled(false);c.SetCaptureMode(true);c.SetProfile(0);meta.LaunchObserver(RoleId.Driller);break;
                    case 27:
                        if(CRTDisplayFeature.HistoryBytes!=0)throw new InvalidOperationException("CRT Off retained its history buffer");
                        c.SetEnabled(true);FreezeReady(run);break;
                    case 28:Capture("observer-crew");UnityEngine.Object.FindFirstObjectByType<ObserverMode>()?.Exit();CrtSurface.LegacyHud=true;break;
                    case 29:Capture("legacy-hud-comparison");CrtSurface.LegacyHud=false;
                        if(_width!=1920||_height!=1080){meta.Show(MetaScreens.Screen.Starmap);_step=32;break;}
                        var renderer=AssetDatabase.LoadAssetAtPath<Renderer2DData>("Assets/Settings/Renderer2D.asset");
                        _feature=renderer.rendererFeatures.OfType<CRTDisplayFeature>().Single();
                        _originalShader=_feature.displayShader;_feature.SetActive(false);break;
                    case 30:VerifyFlatPointer();Capture("failopen-feature-disabled");_feature.SetActive(true);_feature.displayShader=null;_feature.Create();break;
                    case 31:VerifyFlatPointer();Capture("failopen-shader-missing");RestoreFeature();meta.Show(MetaScreens.Screen.Starmap);break;
                    case 32:Capture("starmap");meta.Show(MetaScreens.Screen.Settlement,MetaScreens.SettleView.Map);break;
                    case 33:Capture("growth-map");meta.Show(MetaScreens.Screen.Settlement,MetaScreens.SettleView.Relics);break;
                    case 34:Capture("relic-vault");meta.Show(MetaScreens.Screen.Settlement,MetaScreens.SettleView.Summary);break;
                    case 35:Capture("settlement-summary");meta.Show(MetaScreens.Screen.Run);break;
                    case 36:if(FreezeReady(run))run.SetPaused(true);break;
                    case 37:Capture("pause-menu");Stop();break;
                }
                // Start the wait AFTER expensive readback/PNG writes, leaving real rendered frames between stages.
                _next=Math.Max(_next,EditorApplication.timeSinceStartup+1);
            }
            catch(Exception e){Debug.LogException(e);Stop();}
        }
        static void StudyMotion(ScriptableRenderContext context,List<Camera> cameras)
        {
            if(_studyMotion&&_camera!=null&&cameras.Contains(_camera))
                _camera.transform.position=_studyCameraOrigin+Vector3.right*(Mathf.Sin(Time.unscaledTime*3)*.7f);
        }
        static void TickStudies(CRTDisplayController c)
        {
            int i=_studyIndex++;
            if(i==0){c.SetEnabled(false);return;}
            if(i==1){Capture("00-off");c.SetEnabled(true);c.SetProfile(0);c.SetCurvatureProfile(0);return;}
            if(i>=2&&i<32)
            {
                int combination=i-2;
                Capture($"{combination/5+1:00}-{(CrtEffect)(combination/5)}-G{combination%5+1}");
                int next=combination+1;
                if(next<30){c.SetProfile(next/5);c.SetCurvatureProfile(next%5);}
                else {c.SetProfile(5);c.SetCurvatureProfile(0);c.SettingsOpen=true;}
                return;
            }
            switch(i)
            {
                case 32:Capture("settings-six-by-five");c.SettingsOpen=false;c.SetAccessibility(CrtAccessibility.Photosensitive);break;
                case 33:Capture("mix-photosensitive");c.SetAccessibility(CrtAccessibility.Standard);c.SetProfile(4);c.SetCaptureMode(false);
                    _studyCameraOrigin=_camera.transform.position;_studyMotion=true;_next+=3;break;
                case 34:Capture("afterglow-motion");
                    if(CRTDisplayFeature.HistoryBytes<=0)throw new InvalidOperationException("Afterglow history missing");
                    c.SetProfile(5);_next+=3;break;
                case 35:Capture("mix-motion");_studyMotion=false;_camera.transform.position=_studyCameraOrigin;
                    c.SetEnabled(false);break;
                case 36:
                    if(CRTDisplayFeature.HistoryBytes!=0)throw new InvalidOperationException("Off retained history");
                    RenderPipelineManager.beginContextRendering-=StudyMotion;Stop();break;
            }
        }
        static RenderTexture CreateCaptureTarget()
        {
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(pipeline==null)throw new InvalidOperationException("CRT capture requires the project's active URP asset");
            _report.pipelineHdrPrecision=pipeline.hdrColorBufferPrecision.ToString();
            _report.preserveFramebufferAlpha=Graphics.preserveFramebufferAlpha;
            int mode=Argument("-crtHdr",2);
            if(mode==0){_report.captureMode="LDR32 smoke";return new RenderTexture(_width,_height,24,RenderTextureFormat.ARGB32);}
            if(mode==1){_report.captureMode="DefaultHDR stress";return new RenderTexture(_width,_height,24,RenderTextureFormat.DefaultHDR);}
            // An external RT overrides URP's normal internal format. Ask this installed URP
            // version for the exact native choice instead of accidentally forcing 64-bit HDR.
            var choose=typeof(UniversalRenderPipeline).GetMethod("MakeRenderTextureGraphicsFormat",BindingFlags.Static|BindingFlags.NonPublic);
            if(choose==null)throw new MissingMethodException("URP native graphics-format selection is unavailable");
            var format=(GraphicsFormat)choose.Invoke(null,new object[]{_camera.allowHDR&&pipeline.supportsHDR,pipeline.hdrColorBufferPrecision,Graphics.preserveFramebufferAlpha});
            _report.captureMode="Pipeline native";
            var descriptor=new RenderTextureDescriptor(_width,_height){graphicsFormat=format,depthBufferBits=24,msaaSamples=1};
            descriptor.sRGB=QualitySettings.activeColorSpace==ColorSpace.Linear;
            return new RenderTexture(descriptor);
        }
        static void VerifyFlatPointer()
        {
            var point=new Vector2(Screen.width*.08f,Screen.height*.11f);
            if((CrtGui.GlassToContentScreen(point)-point).sqrMagnitude>.001f)
                throw new InvalidOperationException("Fail-open display retained curved UI hit testing");
        }
        static void Capture(string name)
        {
            Canvas.ForceUpdateCanvases();
            if(name=="run-01-surveyor")
            {
                var run=UnityEngine.Object.FindFirstObjectByType<RunBootstrap>();
                Debug.Log($"[CRT QA] world runTime={run.Sim.RunTime}, phase={run.Sim.Phase}, paused={run.Paused}, timeScale={Time.timeScale}, los={run.Sim.Los.HasComputed}, player={run.Sim.Player.Position}, projected={IsometricProjection.ToRender(run.Sim.Player.Position)}");
                foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    if(renderer.name=="Player"||renderer.name=="Darkness")
                    {
                        Debug.Log($"[CRT QA] world renderer {renderer.name}, bounds={renderer.bounds}, layer={renderer.gameObject.layer}, visible={renderer.isVisible}, enabled={renderer.enabled}");
                        if(renderer.name=="Darkness")
                        {
                            var texture=renderer.sharedMaterial.GetTexture("_LosTex") as Texture2D;var values=texture.GetPixels32();int sum=0;foreach(var pixel in values)sum+=pixel.r;
                            Debug.Log($"[CRT QA] LOS visible sum={sum}, texture={texture.width}x{texture.height}");
                        }
                    }
            }
            // Read the completed normal frame. A second render request can omit late camera UI on some URP paths.
            Debug.Log($"[CRT QA] {name}: camera={_camera.transform.position}, mask={_camera.cullingMask}, renderers={UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length}, frame={Time.frameCount}");
            if(name=="title-surveyor")
            {
                foreach(var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    Debug.Log($"[CRT QA] all camera {cam.name}, renderType={cam.GetUniversalAdditionalCameraData().renderType}, mask={cam.cullingMask}, position={cam.transform.position}, projection={cam.projectionMatrix}, target={cam.targetTexture}, enabled={cam.enabled}");
                Debug.Log($"[CRT QA] camera rot={_camera.transform.rotation}, near={_camera.nearClipPlane}, far={_camera.farClipPlane}, ortho={_camera.orthographicSize}, enabled={_camera.enabled}, scene={_camera.scene}");
                var planes=GeometryUtility.CalculateFrustumPlanes(_camera);
                foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    Debug.Log($"[CRT QA] renderer {renderer.name}, enabled={renderer.enabled}, bounds={renderer.bounds}, inView={GeometryUtility.TestPlanesAABB(planes,renderer.bounds)}, material={renderer.sharedMaterial?.shader?.name}");
                foreach(var graphic in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsSortMode.None))
                    Debug.Log($"[CRT QA] graphic {graphic.name}, world={graphic.transform.position}, scale={graphic.transform.lossyScale}, rect={graphic.rectTransform.rect}, enabled={graphic.enabled}, color={graphic.color}, cull={graphic.canvasRenderer.cull}");
            }
            foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                Debug.Log($"[CRT QA] canvas={canvas.name}, camera={canvas.worldCamera?.name}, enabled={canvas.enabled}, sorting={canvas.sortingLayerName}/{canvas.sortingOrder}, override={canvas.overrideSorting}, size={canvas.GetComponent<RectTransform>().rect}, children={canvas.transform.childCount}");
            var old=RenderTexture.active;
            RenderTexture review=null;
            if(Argument("-crtHdr",0)!=0)
            {
                // ReadPixels does not encode linear HDR values for a normal PNG/JPG display.
                review=RenderTexture.GetTemporary(_width,_height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                bool write=GL.sRGBWrite;GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;
                try { Graphics.Blit(_target,review); }finally { GL.sRGBWrite=write; }
            }
            RenderTexture.active=review!=null?review:_target;
            var tex=new Texture2D(_target.width,_target.height,TextureFormat.RGBA32,false);
            try
            {
            tex.ReadPixels(new Rect(0,0,_target.width,_target.height),0,0);tex.Apply();
            WriteCapture(Path.Combine(_output,name+".png"),tex.EncodeToPNG());
            // Lightweight review proxy of the same render; retain the full-resolution PNG for pixel checks.
            WriteCapture(Path.Combine(_output,name+".jpg"),tex.EncodeToJPG(94));
            var pixels=tex.GetPixels32();int amber=0,samples=0,worldOrange=0;double luma=0;
            for(int y=0;y<_height;y+=4)for(int x=0;x<_width;x+=4)
            {
                var p=pixels[y*_width+x];luma+=(.2126*p.r+.7152*p.g+.0722*p.b)/255;samples++;
                if(p.r>100&&p.g>65&&p.b<p.r*.7f)
                {amber++;if(x>_width*.2f&&x<_width*.8f&&y>_height*.3f&&y<_height*.75f)worldOrange++;}
            }
            _report.captures.Add(new CaptureRecord{name=name,width=_width,height=_height,amberPixels=amber,meanLuma=(float)(luma/samples)});
            if(amber<20)throw new InvalidOperationException($"{name}: amber HUD missing ({amber} sampled pixels)");
            if(name=="run-01-surveyor"&&worldOrange<20)throw new InvalidOperationException($"{name}: orange player missing from world ({worldOrange} sampled pixels)");
            Debug.Log($"[CRT QA] Captured {name}, canvases={UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);RenderTexture.active=old;
                if(review!=null)RenderTexture.ReleaseTemporary(review);
            }
        }
        static void Stop()
        {
            _studyMotion=false;RenderPipelineManager.beginContextRendering-=StudyMotion;
            RestoreFeature();
            EditorApplication.update-=Tick;
            RenderPipelineManager.endContextRendering-=Sample;CRTDisplayFeature.Measure=false;
            if(_camera!=null)_camera.targetTexture=null;
            if(CrtCameraStack.UiCamera!=null)CrtCameraStack.UiCamera.targetTexture=null;
            if(_target!=null)UnityEngine.Object.DestroyImmediate(_target);
            Application.logMessageReceived-=Log;
            foreach(var pair in Gpu){pair.Value.Sort();int n=pair.Value.Count;_report.timings.Add(new TimingRecord{monitor=pair.Key,samples=n,medianMs=pair.Value[n/2],p95Ms=pair.Value[Math.Min(n-1,(int)(n*.95))]});}
            File.WriteAllText(Path.Combine(_output,"capture-report.json"),JsonUtility.ToJson(_report,true));
            SessionState.SetInt(Key+"Errors",_errors);
            EditorApplication.isPlaying=false;
        }
        static void Finish()
        {
            SessionState.SetBool(Key,false);
            int errors=SessionState.GetInt(Key+"Errors",0);
            Debug.Log($"[CRT QA] Complete; runtime errors={errors}");
            if(Application.isBatchMode||Array.IndexOf(Environment.GetCommandLineArgs(),"-crtCaptureAutoExit")>=0)EditorApplication.Exit(errors==0?0:1);
        }
        static void RestoreFeature()
        {
            if(_feature==null)return;
            _feature.displayShader=_originalShader;_feature.SetActive(true);_feature.Create();_feature=null;
        }
        static void WriteCapture(string path,byte[] bytes)
        {
            // Image previewers can memory-map the old frame. Replace the directory entry instead of
            // truncating its mapped stream; never keep a backup/version file beside the requested frame.
            string pending=path+".pending";
            try
            {
                File.WriteAllBytes(pending,bytes);
                for(int attempt=0;;attempt++)
                {
                    try
                    {
                        if(File.Exists(path))File.Replace(pending,path,null);
                        else File.Move(pending,path);
                        break;
                    }
                    // Scanners/readers can briefly deny replacement. Keep the old complete file
                    // until replacement succeeds, and still fail the capture on a persistent lock.
                    catch(IOException) when(attempt<4) {System.Threading.Thread.Sleep(50*(attempt+1));}
                }
            }
            finally { if(File.Exists(pending))File.Delete(pending); }
        }
        static void SetSize(int width,int height)
        {
            var asm=typeof(Editor).Assembly;
            var sizesType=asm.GetType("UnityEditor.GameViewSizes");
            var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes=singleton.GetProperty("instance").GetValue(null);
            var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{GameViewSizeGroupType.Standalone});
            var sizeType=asm.GetType("UnityEditor.GameViewSize");
            var kind=asm.GetType("UnityEditor.GameViewSizeType");
            var size=Activator.CreateInstance(sizeType,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,
                new object[]{Enum.ToObject(kind,1),width,height,"CRT QA"},null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
            int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
            var view=EditorWindow.GetWindow(asm.GetType("UnityEditor.GameView"));
            view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,count-1);
        }
    }
}
