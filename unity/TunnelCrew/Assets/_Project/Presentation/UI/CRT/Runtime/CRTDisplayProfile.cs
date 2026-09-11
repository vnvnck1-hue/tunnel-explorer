using UnityEngine;

namespace TunnelCrew.Presentation.CRT
{
    public enum CrtMonitor { Surveyor, Broadcast, Arcade, Relay, Abyss }
    public enum CrtAccessibility { Standard, Comfort, Photosensitive }

    /// <summary>Monitor construction and signal characteristics, not a ladder of effect strengths.</summary>
    [CreateAssetMenu(menuName = "Tunnel Crew/CRT/Display profile")]
    public sealed class CRTDisplayProfile : ScriptableObject
    {
        public CrtMonitor monitor;
        public string title, subtitle;
        [TextArea] public string description;
        public CrtParameters parameters;

        public static void Populate(CRTDisplayProfile p, CrtMonitor type)
        {
            p.monitor = type;
            var v = CrtParameters.Default;
            switch (type)
            {
                case CrtMonitor.Surveyor:
                    p.title = "굴착선 관제관"; p.subtitle = "TC-01 / SURVEYOR";
                    p.description = "둥근 유리 · 미세 주사선 · 가장자리 RGB 번짐. 탐사를 중계하는 표준 관제 장치.";
                    break;
                case CrtMonitor.Broadcast:
                    p.title = "방송 정밀관"; p.subtitle = "TC-02 / BROADCAST";
                    p.description = "평평한 관 · 수직 인광 그릴 · 안정적인 신호. 선명한 방송용 모니터.";
                    v.curvature = .018f; v.aberrationPixels = .3f; v.scanlineStrength = .065f;
                    v.scanlinePeriod = 2f; v.maskStyle = 1; v.maskStrength = .16f;
                    v.noiseStrength = .003f; v.jitterStrengthPixels = 0; v.vignetteStrength = .17f;
                    v.phosphorBloom = .045f; v.edgeFeather = .005f; v.roundness = .06f;
                    break;
                case CrtMonitor.Arcade:
                    p.title = "광산 오락실"; p.subtitle = "TC-03 / ARCADE";
                    p.description = "돔 유리 · 점형 섀도 마스크 · 밝기에 반응하는 굵은 빔. 오래된 오락실의 발광감.";
                    v.curvature = .072f; v.aberrationPixels = .8f; v.scanlineStrength = .19f;
                    v.scanlinePeriod = 3.5f; v.maskStyle = 2; v.maskStrength = .19f;
                    v.beamWidth = .55f; v.phosphorBloom = .21f; v.noiseStrength = .009f;
                    v.jitterStrengthPixels = .15f; v.vignetteStrength = .29f; v.roundness = .17f;
                    break;
                case CrtMonitor.Relay:
                    p.title = "장거리 수신기"; p.subtitle = "TC-04 / RELAY";
                    p.description = "낮은 신호 대역폭 · 수평 에코 · 느린 동기 띠. 먼 지하에서 도착한 영상.";
                    v.curvature = .045f; v.aberrationPixels = .65f; v.scanlineStrength = .10f;
                    v.scanlinePeriod = 2.5f; v.noiseStrength = .025f; v.jitterStrengthPixels = .35f;
                    v.horizontalBleed = .28f; v.echoStrength = .09f; v.echoOffsetPixels = 6;
                    v.rollStrength = .028f; v.rollSpeed = .065f; v.phosphorBloom = .07f;
                    v.vignetteStrength = .30f;
                    break;
                case CrtMonitor.Abyss:
                    p.title = "심층 음극관"; p.subtitle = "TC-05 / ABYSS";
                    p.description = "교차 주사 · 긴 인광 감쇠 · 두꺼운 암부 유리. 빛의 흔적이 남는 심층 관측관.";
                    v.curvature = .058f; v.aberrationPixels = .5f; v.scanlineStrength = .14f;
                    v.scanlinePeriod = 3; v.interlace = .12f; v.persistence = .24f;
                    v.noiseStrength = .012f; v.jitterStrengthPixels = .18f; v.phosphorBloom = .13f;
                    v.vignetteStrength = .40f; v.rollStrength = .015f; v.rollSpeed = .03f;
                    v.roundness = .15f;
                    break;
            }
            p.parameters = v;
        }
    }

    [System.Serializable]
    public struct CrtParameters
    {
        [Range(0, .12f)] public float curvature;
        [Range(0, 1.8f)] public float aberrationPixels;
        [Range(1.5f, 5)] public float scanlinePeriod;
        [Range(0, .35f)] public float scanlineStrength;
        [Range(0, .06f)] public float noiseStrength;
        [Range(0, 1.5f)] public float jitterStrengthPixels;
        [Range(0, .5f)] public float vignetteStrength;
        [Range(0, .3f)] public float phosphorBloom;
        [Range(0, .4f)] public float persistence;
        [Range(0, .4f)] public float horizontalBleed, echoStrength;
        [Range(0, 10)] public float echoOffsetPixels;
        [Range(0, .06f)] public float rollStrength;
        [Range(0, .2f)] public float rollSpeed;
        [Range(0, .2f)] public float interlace;
        [Range(0, 2)] public int maskStyle;
        [Range(0, .3f)] public float maskStrength;
        [Range(0, 1)] public float beamWidth;
        [Range(.01f, .2f)] public float roundness;
        [Range(.001f, .02f)] public float edgeFeather;
        [Range(.028f, .09f)] public float safeAreaInset;
        [Range(.75f,1.25f)] public float brightness;
        [Range(.8f,1.2f)] public float contrast;
        [Range(0,.012f)] public float blackFloor;
        [Range(0,60)] public float noiseSpeed;
        [Tooltip("Seconds between sync sweeps (3–8 seconds).")]
        [Range(3,8)] public float jitterFrequency;
        [Range(1,3)] public int jitterBandCount;
        [Range(0,1)] public float scanlineSpeed;
        public bool allowEventGlitch;

        public static CrtParameters Default => new CrtParameters {
            curvature = .052f, aberrationPixels = .95f, scanlinePeriod = 2.5f,
            scanlineStrength = .095f, noiseStrength = .018f, jitterStrengthPixels = .35f,
            vignetteStrength = .36f, phosphorBloom = .10f, beamWidth = .2f,
            roundness = .12f, edgeFeather = .006f, safeAreaInset = .055f,
            brightness=1,contrast=1,noiseSpeed=24,jitterFrequency=5.7f,jitterBandCount=1,allowEventGlitch=true
        };

        static float Clamp(float value,float min,float max)=>float.IsNaN(value)?min:Mathf.Clamp(value,min,max);
        static int Clamp(int value,int min,int max)=>Mathf.Clamp(value,min,max);
        static float Unit(float value)=>Clamp(value,0,1);

        public CrtParameters Resolve(CrtAccessibility mode, bool textFocus, float strength)
        {
            var p = this;
            float s = Unit(strength);
            p.curvature = Clamp(p.curvature, 0, .12f) * s;
            p.aberrationPixels = Clamp(p.aberrationPixels, 0, 1.8f) * s;
            p.scanlinePeriod = Clamp(p.scanlinePeriod, 1.5f, 5);
            p.scanlineStrength = Clamp(p.scanlineStrength, 0, .35f) * s;
            p.noiseStrength = Clamp(p.noiseStrength, 0, .06f) * s;
            p.jitterStrengthPixels = Clamp(p.jitterStrengthPixels, 0, .35f) * s;
            p.vignetteStrength = Clamp(p.vignetteStrength, 0, .5f) * s;
            p.persistence = Clamp(p.persistence, 0, .4f) * s;
            p.phosphorBloom = Clamp(p.phosphorBloom, 0, .3f) * s;
            p.maskStyle = Clamp(p.maskStyle, 0, 2);
            p.maskStrength = Clamp(p.maskStrength, 0, .3f) * s;
            p.horizontalBleed = Clamp(p.horizontalBleed, 0, .4f) * s;
            p.echoStrength = Clamp(p.echoStrength, 0, .4f) * s;
            p.echoOffsetPixels = Clamp(p.echoOffsetPixels, 0, 10);
            p.rollStrength = Clamp(p.rollStrength, 0, .06f) * s;
            p.rollSpeed = Clamp(p.rollSpeed, 0, .2f);
            p.interlace = Clamp(p.interlace, 0, .2f) * s;
            p.beamWidth = Unit(p.beamWidth);
            p.roundness = Clamp(p.roundness, .01f, .2f);
            p.edgeFeather = Clamp(p.edgeFeather, .001f, .02f);
            p.safeAreaInset = Clamp(p.safeAreaInset, .028f, .09f);
            p.brightness=Mathf.Lerp(1,Clamp(p.brightness,.75f,1.25f),s);
            p.contrast=Mathf.Lerp(1,Clamp(p.contrast,.8f,1.2f),s);
            p.blackFloor=Clamp(p.blackFloor,0,.012f)*s;
            p.noiseSpeed=Clamp(p.noiseSpeed,0,60);
            p.jitterFrequency=Clamp(p.jitterFrequency,3,8);
            p.jitterBandCount=Clamp(p.jitterBandCount,1,3);
            p.scanlineSpeed=Unit(p.scanlineSpeed)*s;
            if (textFocus) { p.noiseStrength = Mathf.Min(p.noiseStrength, .003f); p.jitterStrengthPixels = 0; }
            if (mode == CrtAccessibility.Comfort)
            {
                p.curvature *= .3f; p.aberrationPixels *= .2f; p.scanlineStrength *= .4f;
                p.noiseStrength *= .1f; p.jitterStrengthPixels = 0; p.persistence = 0;
                p.vignetteStrength *= .4f; p.interlace = p.rollStrength = 0; p.echoStrength *= .3f;
                p.scanlineSpeed=0;p.allowEventGlitch=false;
            }
            if (mode == CrtAccessibility.Photosensitive)
            {
                p.curvature = p.aberrationPixels = p.jitterStrengthPixels = p.noiseStrength = 0;
                p.persistence = p.interlace = p.rollStrength = p.rollSpeed = p.echoStrength = 0;
                p.scanlineStrength = Mathf.Min(.025f, p.scanlineStrength); p.vignetteStrength *= .2f;
                p.noiseSpeed=p.scanlineSpeed=0;p.allowEventGlitch=false;
            }
            return p;
        }
    }
}
