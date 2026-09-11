using UnityEngine;
using UnityEngine.InputSystem;

namespace TunnelCrew.Presentation.CRT
{
    [DefaultExecutionOrder(-200)]
    public sealed partial class CRTDisplayController : MonoBehaviour, ICrtScreen
    {
        public static CRTDisplayController Instance { get; private set; }
        public CRTDisplayProfile[] Profiles { get; private set; }
        public CRTDisplayProfile[] CurvatureProfiles { get; private set; }
        public int CurvatureIndex { get; private set; }
        public const int EffectCount=6, CurvatureCount=5;
        public int ProfileIndex { get; private set; }
        public bool DisplayEnabled { get; private set; } = true;
        public CrtAccessibility Accessibility { get; private set; }
        public float Strength = 1;
        public float Scanlines = 1, Curvature = 1, Noise = 1, Aberration = 1, Vignette = 1;
        public bool TextFocus { get; private set; }
        public bool CaptureMode { get; private set; }
        public bool SettingsOpen { get; set; }
        public bool UsingGamepad { get; private set; }
        int _closedSettingsFrame=-1;
        float _closedSettingsUntil;
        public bool ConsumesInput => SettingsOpen || _closedSettingsFrame == Time.frameCount || Time.unscaledTime<_closedSettingsUntil;
        public int Revision { get; private set; }
        float _eventUntil, _eventAmount, _eventLength;
        public CRTDisplayProfile Profile => Profiles[ProfileIndex];
        public float Clock => CaptureMode || Accessibility == CrtAccessibility.Photosensitive ? 17.25f : Time.unscaledTime;
        public float EventAmount => Accessibility != CrtAccessibility.Standard || TextFocus || !Profile.parameters.allowEventGlitch ? 0 :
            _eventAmount * Mathf.Clamp01(float.IsNaN(Strength)?0:Strength)*Mathf.Clamp01(float.IsNaN(Noise)?0:Noise)*
            Mathf.Clamp01((_eventUntil - Time.unscaledTime) / Mathf.Max(.001f, _eventLength));
        public CrtParameters Effective
        {
            get
            {
                var p = Profile.parameters;
                var geometry=CurvatureProfiles[CurvatureIndex].parameters;
                geometry.curvature*=Curvature;
                geometry=geometry.Resolve(Accessibility,false,1);
                p.scanlineStrength *= Scanlines; p.noiseStrength *= Noise;
                p.rfSnow*=Noise;p.rfTearPixels*=Noise;p.horizontalBleed*=Aberration;
                p.jitterStrengthPixels *= Noise; p.aberrationPixels *= Aberration; p.vignetteStrength *= Vignette;
                p=p.Resolve(Accessibility, TextFocus, Strength);
                p.curvature=geometry.curvature;p.roundness=geometry.roundness;
                p.edgeFeather=geometry.edgeFeather;p.safeAreaInset=geometry.safeAreaInset;
                return p;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null;CrtSurface.LegacyHud=false;CrtGui.OriginalPalette=false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance == null) new GameObject("CRT Display").AddComponent<CRTDisplayController>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<CrtCameraStack>();
            CurvatureProfiles = new CRTDisplayProfile[CurvatureCount];
            for (int i = 0; i < CurvatureCount; i++)
            {
                CurvatureProfiles[i] = Resources.Load<CRTDisplayProfile>("CRT/CRT_" + (CrtMonitor)i);
                if (CurvatureProfiles[i] == null)
                {
                    CurvatureProfiles[i] = ScriptableObject.CreateInstance<CRTDisplayProfile>();
                    CurvatureProfiles[i].hideFlags = HideFlags.DontSave;
                    CRTDisplayProfile.Populate(CurvatureProfiles[i], (CrtMonitor)i);
                }
            }
            Profiles=new CRTDisplayProfile[EffectCount];
            for(int i=0;i<EffectCount;i++)
            {
                Profiles[i]=Resources.Load<CRTDisplayProfile>("CRT/FX_"+(CrtEffect)i);
                if(Profiles[i]==null)
                {
                    Profiles[i]=ScriptableObject.CreateInstance<CRTDisplayProfile>();
                    Profiles[i].hideFlags=HideFlags.DontSave;CRTDisplayProfile.PopulateEffect(Profiles[i],(CrtEffect)i);
                }
            }
            // Preserve the old chosen glass, but never reinterpret its index as a new effect.
            CurvatureIndex=Mathf.Clamp(PlayerPrefs.GetInt("tc.crt.glass",PlayerPrefs.GetInt("tc.crt.monitor",0)),0,CurvatureCount-1);
            ProfileIndex = Mathf.Clamp(PlayerPrefs.GetInt("tc.crt.effect", 5), 0, EffectCount-1);
            DisplayEnabled = PlayerPrefs.GetInt("tc.crt.enabled", 1) != 0;
            Accessibility = (CrtAccessibility)Mathf.Clamp(PlayerPrefs.GetInt("tc.crt.accessibility", 0), 0, 2);
            Strength = Load("strength"); Scanlines = Load("scanlines"); Curvature = Load("curvature");
            Noise = Load("noise"); Aberration = Load("aberration"); Vignette = Load("vignette");
            CrtSurface.Register(this, 50, true);
        }
        static float Load(string key)
        {
            float value=PlayerPrefs.GetFloat("tc.crt."+key,1);
            return float.IsNaN(value)||float.IsInfinity(value)?1:Mathf.Clamp01(value);
        }
        public void Save()
        {
            PlayerPrefs.SetInt("tc.crt.effect", ProfileIndex);PlayerPrefs.SetInt("tc.crt.glass",CurvatureIndex);
            PlayerPrefs.SetInt("tc.crt.enabled", DisplayEnabled ? 1 : 0);
            PlayerPrefs.SetInt("tc.crt.accessibility", (int)Accessibility);
            PlayerPrefs.SetFloat("tc.crt.strength", Strength); PlayerPrefs.SetFloat("tc.crt.scanlines", Scanlines);
            PlayerPrefs.SetFloat("tc.crt.curvature", Curvature); PlayerPrefs.SetFloat("tc.crt.noise", Noise);
            PlayerPrefs.SetFloat("tc.crt.aberration", Aberration); PlayerPrefs.SetFloat("tc.crt.vignette", Vignette);
            PlayerPrefs.Save();
        }
        public void SetProfile(int index) { ProfileIndex = (index % EffectCount + EffectCount) % EffectCount; Revision++; _eventUntil = 0; }
        public void SetCurvatureProfile(int index) { CurvatureIndex=(index%CurvatureCount+CurvatureCount)%CurvatureCount;Revision++;_eventUntil=0; }
        public void SetEnabled(bool value) { DisplayEnabled = value; Revision++; }
        public void SetAccessibility(CrtAccessibility mode) { Accessibility = mode; Revision++; _eventUntil = 0; }
        public void SetUiFocus(bool value) => TextFocus = value;
        public void SetCaptureMode(bool value) { CaptureMode = value; Revision++; }
        public void SignalHit(float amount, float duration = .12f)
        {
            if(float.IsNaN(amount)||float.IsInfinity(amount)||float.IsNaN(duration)||float.IsInfinity(duration))return;
            if (Accessibility != CrtAccessibility.Standard || !DisplayEnabled || !Profile.parameters.allowEventGlitch) return;
            _eventAmount = Mathf.Clamp01(amount); _eventLength = Mathf.Clamp(duration, .01f, .12f);
            _eventUntil = Time.unscaledTime + _eventLength;
        }
        public void SignalDropout(float amount, float duration = .12f) => SignalHit(amount, duration);
        void Update()
        {
            var k = Keyboard.current;
            var pad=Gamepad.current;
            var mouse=Mouse.current;
            if((k!=null&&k.anyKey.wasPressedThisFrame)||(mouse!=null&&(mouse.delta.ReadValue().sqrMagnitude>4||mouse.leftButton.wasPressedThisFrame)))UsingGamepad=false;
            if(pad!=null&&(pad.leftStick.ReadValue().sqrMagnitude>.04f||pad.rightStick.ReadValue().sqrMagnitude>.04f||
                pad.buttonSouth.wasPressedThisFrame||pad.buttonEast.wasPressedThisFrame||pad.buttonNorth.wasPressedThisFrame||pad.buttonWest.wasPressedThisFrame||
                pad.dpad.ReadValue().sqrMagnitude>.1f||pad.leftShoulder.wasPressedThisFrame||pad.rightShoulder.wasPressedThisFrame||
                pad.leftTrigger.wasPressedThisFrame||pad.rightTrigger.wasPressedThisFrame||pad.startButton.wasPressedThisFrame||pad.selectButton.wasPressedThisFrame))UsingGamepad=true;
            if (TextFocus) return;
            // Consume the opening mouse press before RunBootstrap.Update can drill/fire or advance.
            // The DrawCrt fallback still owns the visual button, but must not be the first input gate.
            if(!SettingsOpen&&mouse!=null&&mouse.leftButton.wasPressedThisFrame&&MonitorButtonHit(mouse.position.ReadValue()))
            {SettingsOpen=true;return;}
            if(pad!=null&&((pad.selectButton.isPressed&&pad.startButton.wasPressedThisFrame)||(pad.startButton.isPressed&&pad.selectButton.wasPressedThisFrame)))
            {if(SettingsOpen)CloseSettings();else SettingsOpen=true;return;}
            if (SettingsOpen)
            {
                if ((k!=null&&k.escapeKey.wasPressedThisFrame)||(pad!=null&&pad.buttonEast.wasPressedThisFrame)) { CloseSettings(); return; }
                UpdateSettingsNavigation(k,pad);
            }
            if(k==null)return;
            if (k.f6Key.wasPressedThisFrame)
            {
                int step=k.shiftKey.isPressed?-1:1;
                if(k.ctrlKey.isPressed)SetCurvatureProfile(CurvatureIndex+step);else SetProfile(ProfileIndex+step);
                Save();
            }
            if (k.f7Key.wasPressedThisFrame) { SetEnabled(!DisplayEnabled); Save(); }
            if (k.f10Key.wasPressedThisFrame) { if(SettingsOpen) CloseSettings(); else SettingsOpen=true; }
        }
        public void CloseSettings() { SettingsOpen=false; _closedSettingsFrame=Time.frameCount;_closedSettingsUntil=Time.unscaledTime+(UiThemeProfile.Active!=null?UiThemeProfile.Active.revealSeconds:.14f);Save(); }
        [ContextMenu("Development / Compare original HUD layout")]
        void ToggleOriginalHud()
        {
            if(Application.isEditor||Debug.isDebugBuild)CrtSurface.LegacyHud=!CrtSurface.LegacyHud;
        }
        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            UiThemeProfile.ReleaseRuntimeMaterial();
            if (Profiles != null) foreach (var p in Profiles) if (p != null && p.hideFlags == HideFlags.DontSave) Destroy(p);
            if (CurvatureProfiles != null) foreach (var p in CurvatureProfiles) if (p != null && p.hideFlags == HideFlags.DontSave) Destroy(p);
        }
    }
}
