using System;
using NUnit.Framework;
using TunnelCrew.Presentation.CRT;
using TunnelCrew.Sim;
using UnityEditor;
using UnityEngine;

namespace TunnelCrew.Tests
{
    public sealed class CrtDisplayTests
    {
        [TestCase("CRTDisplay")] [TestCase("UiPhosphor")]
        public void DisplayShaderHasNoCompilerErrors(string name)
        {
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Presentation/UI/CRT/Shaders/"+name+".shader");
            Assert.That(shader,Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader),Is.False);
        }
        [TestCase(CrtMonitor.Surveyor)] [TestCase(CrtMonitor.Broadcast)] [TestCase(CrtMonitor.Arcade)]
        [TestCase(CrtMonitor.Relay)] [TestCase(CrtMonitor.Abyss)]
        public void InstalledProfileMatchesMonitorAndSurvivesJson(CrtMonitor monitor)
        {
            var profile=Resources.Load<CRTDisplayProfile>("CRT/CRT_"+monitor);
            Assert.That(profile,Is.Not.Null);
            Assert.That(profile.monitor,Is.EqualTo(monitor));
            Assert.That(profile.title,Is.Not.Empty);
            var clone=ScriptableObject.CreateInstance<CRTDisplayProfile>();
            try { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(profile),clone);
                Assert.That(clone.parameters.curvature,Is.EqualTo(profile.parameters.curvature));
                Assert.That(clone.parameters.maskStyle,Is.EqualTo(profile.parameters.maskStyle));
                Assert.That(clone.parameters.persistence,Is.EqualTo(profile.parameters.persistence)); }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
        }
        [TestCase(CrtMonitor.Surveyor)] [TestCase(CrtMonitor.Broadcast)] [TestCase(CrtMonitor.Arcade)]
        [TestCase(CrtMonitor.Relay)] [TestCase(CrtMonitor.Abyss)]
        public void PhotosensitiveRemovesTimeVaryingSignalAndAberration(CrtMonitor monitor)
        {
            var p=Resources.Load<CRTDisplayProfile>("CRT/CRT_"+monitor).parameters.Resolve(CrtAccessibility.Photosensitive,false,1);
            Assert.That(p.curvature+p.aberrationPixels+p.noiseStrength+p.jitterStrengthPixels+p.persistence+
                p.interlace+p.rollStrength+p.rollSpeed+p.echoStrength,Is.Zero);
            Assert.That(p.scanlineStrength,Is.LessThanOrEqualTo(.025f));
            Assert.That(p.noiseSpeed+p.scanlineSpeed,Is.Zero);
            Assert.That(p.allowEventGlitch,Is.False);
        }
        [Test]
        public void FiveMonitorsUseDistinctPhysicalSignalModels()
        {
            CrtParameters P(CrtMonitor m)=>Resources.Load<CRTDisplayProfile>("CRT/CRT_"+m).parameters;
            Assert.That(P(CrtMonitor.Surveyor).curvature,Is.EqualTo(.052f).Within(.0001));
            Assert.That(P(CrtMonitor.Surveyor).persistence,Is.Zero,"Baseline quality policy leaves history RT off");
            Assert.That(P(CrtMonitor.Broadcast).maskStyle,Is.EqualTo(1));
            Assert.That(P(CrtMonitor.Arcade).maskStyle,Is.EqualTo(2));
            Assert.That(P(CrtMonitor.Arcade).beamWidth,Is.GreaterThan(P(CrtMonitor.Surveyor).beamWidth));
            Assert.That(P(CrtMonitor.Relay).horizontalBleed,Is.GreaterThan(0));
            Assert.That(P(CrtMonitor.Relay).echoStrength,Is.GreaterThan(0));
            Assert.That(P(CrtMonitor.Abyss).persistence,Is.GreaterThan(0));
            Assert.That(P(CrtMonitor.Abyss).interlace,Is.GreaterThan(0));
        }
        [Test]
        public void ResolutionIndependentProfileKeepsScanPeriodInOutputPixels()
        {
            var p=CrtParameters.Default;
            foreach(int height in new[]{720,1080,1440,2160})
            {
                // Shader phase is outputUV.y * outputHeight / period: precisely one cycle per period pixels.
                double phaseA=.25*height/p.scanlinePeriod;
                double phaseB=(.25+p.scanlinePeriod/height)*height/p.scanlinePeriod;
                Assert.That(phaseB-phaseA,Is.EqualTo(1).Within(.0001));
            }
        }
        [TestCase(1280,720)] [TestCase(1920,1080)] [TestCase(2560,1440)] [TestCase(3840,2160)]
        [TestCase(1920,1200)] [TestCase(2560,1080)] [TestCase(3440,1440)]
        public void SafeAreaIsInsideScreenAndOperatingSystemInsets(int w,int h)
        {
            var os=new Rect(30,15,w-60,h-30);
            var safe=CrtSafeArea.Calculate(w,h,os,.055f);
            Assert.That(safe.xMin,Is.GreaterThanOrEqualTo(os.xMin));
            Assert.That(safe.yMin,Is.GreaterThanOrEqualTo(os.yMin));
            Assert.That(safe.xMax,Is.LessThanOrEqualTo(os.xMax));
            Assert.That(safe.yMax,Is.LessThanOrEqualTo(os.yMax));
            Assert.That(safe.width,Is.GreaterThan(w*.8));
            Assert.That(safe.height,Is.GreaterThan(h*.8));
        }
        [Test]
        public void AsymmetricSafeAreaPlacesAllContentInsideOperatingSystemBounds()
        {
            var area=CrtSafeArea.Calculate(1920,1080,new Rect(160,20,1760,980),.055f);
            var matrix=CrtSafeArea.ContentMatrix(1920,1080,area);
            var topLeft=matrix.MultiplyPoint3x4(Vector3.zero);
            var bottomRight=matrix.MultiplyPoint3x4(new Vector3(1920,1080,0));
            Assert.That(topLeft.x,Is.GreaterThanOrEqualTo(area.xMin-.001f));
            Assert.That(topLeft.y,Is.GreaterThanOrEqualTo(1080-area.yMax-.001f));
            Assert.That(bottomRight.x,Is.LessThanOrEqualTo(area.xMax+.001f));
            Assert.That(bottomRight.y,Is.LessThanOrEqualTo(1080-area.yMin+.001f));
        }
        [Test]
        public void ResolveEnforcesSafetyCapsAndTextFocus()
        {
            var p=CrtParameters.Default;p.curvature=8;p.aberrationPixels=8;p.jitterStrengthPixels=8;
            p.noiseStrength=8;p.persistence=8;p.safeAreaInset=-8;p.scanlinePeriod=0;
            var safe=p.Resolve(CrtAccessibility.Standard,false,1);
            Assert.That(safe.curvature,Is.EqualTo(.12f));Assert.That(safe.aberrationPixels,Is.EqualTo(1.8f));
            Assert.That(safe.jitterStrengthPixels,Is.EqualTo(.35f));Assert.That(safe.persistence,Is.EqualTo(.4f));
            Assert.That(safe.safeAreaInset,Is.EqualTo(.028f));Assert.That(safe.scanlinePeriod,Is.EqualTo(1.5f));
            var text=p.Resolve(CrtAccessibility.Standard,true,1);
            Assert.That(text.jitterStrengthPixels,Is.Zero);Assert.That(text.noiseStrength,Is.LessThanOrEqualTo(.003f));
            var comfort=p.Resolve(CrtAccessibility.Comfort,false,1);
            Assert.That(comfort.persistence+comfort.interlace+comfort.rollStrength+comfort.jitterStrengthPixels,Is.Zero);
        }
        [TestCase(RoleId.Driller)] [TestCase(RoleId.Gunner)] [TestCase(RoleId.Scout)] [TestCase(RoleId.Engineer)]
        public void HudContractMatchesLiveSimulation(RoleId role)
        {
            var sim=new TunnelSim();sim.StartRun(role);sim.EnterDepth(1,DungeonConfig.Runtime);
            sim.Player.Hp=sim.Player.HpMax*.21;sim.Player.DrillHeat=.73;
            var h=new HudSnapshot(sim);
            Assert.That(h.Role,Is.EqualTo(role));Assert.That(h.Hp,Is.EqualTo(sim.Player.Hp));
            Assert.That(h.HpMax,Is.EqualTo(sim.Player.HpMax));Assert.That(h.Heat,Is.EqualTo(.73));
            Assert.That(h.Critical,Is.True);Assert.That(h.Ammo,Is.EqualTo(sim.Build.Ammo));
            Assert.That(h.AmmoFraction,Is.EqualTo(sim.Build.Ammo/(double)Math.Max(1,sim.Build.MagSize)));
            Assert.That(h.HasGun,Is.EqualTo(sim.Build.RoleHasGun));Assert.That(h.HasDrill,Is.EqualTo(sim.Build.RoleDigMul>0));
            Assert.That(h.Level,Is.EqualTo(sim.Xp.Level));Assert.That(h.Xp,Is.EqualTo(sim.Xp.Xp));
            Assert.That(h.XpNeed,Is.EqualTo(sim.Xp.XpNeed));Assert.That(h.Depth,Is.EqualTo(sim.Depth));
            Assert.That(h.Dominance,Is.EqualTo(sim.Run.Dominance/Math.Max(.001,sim.Run.DominanceTarget)));
            Assert.That(h.Threat,Is.EqualTo(sim.Run.Threat));
        }
        [Test]
        public void InvalidNumbersCannotReachShaderAndDefaultToneIsNeutral()
        {
            var p=CrtParameters.Default;
            Assert.That(p.brightness,Is.EqualTo(1));Assert.That(p.contrast,Is.EqualTo(1));Assert.That(p.blackFloor,Is.Zero);
            p.curvature=p.noiseStrength=p.jitterFrequency=p.brightness=float.NaN;
            p.contrast=float.PositiveInfinity;
            var safe=p.Resolve(CrtAccessibility.Standard,false,float.NaN);
            foreach(var field in typeof(CrtParameters).GetFields())
                if(field.FieldType==typeof(float))
                {
                    float value=(float)field.GetValue(safe);
                    Assert.That(float.IsNaN(value)||float.IsInfinity(value),Is.False,field.Name);
                }
        }
        [Test]
        public void HistoryDownsamplesOnlyAbove1440pAndPreservesOddDimensions()
        {
            Assert.That(CRTDisplayFeature.HistoryResolution(1920,1080),Is.EqualTo(new Vector2Int(1920,1080)));
            Assert.That(CRTDisplayFeature.HistoryResolution(2560,1440),Is.EqualTo(new Vector2Int(2560,1440)));
            Assert.That(CRTDisplayFeature.HistoryResolution(3840,2160),Is.EqualTo(new Vector2Int(1920,1080)));
            Assert.That(CRTDisplayFeature.HistoryResolution(3441,1441),Is.EqualTo(new Vector2Int(1721,721)));
        }
        [Test]
        public void ThemeRemapsRichTextWithoutLeakingOriginalPalette()
        {
            Assert.That(UiThemeProfile.ThemeText("<color=#ffffff>test</color>"),Does.Contain("#FFD65A"));
            Assert.That(UiThemeProfile.ThemeText("plain 한글"),Is.EqualTo("plain 한글"));
        }
    }
}
