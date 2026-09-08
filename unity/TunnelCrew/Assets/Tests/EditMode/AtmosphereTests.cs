using NUnit.Framework;
using TunnelCrew.Presentation.Visual;
using UnityEngine;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 대기 원근 프로파일과 결정성(기능명세서 §7.5, §12.1, §16.3).
    /// 셰이더 픽셀은 캡처로 보고, 여기서는 셰이더에 넘어가는 값의 계약만 잠근다.
    /// </summary>
    public class AtmosphereTests
    {
        static AtmosphereProfile New() => ScriptableObject.CreateInstance<AtmosphereProfile>();

        [Test]
        public void 기본값은_형태를_덮지_않을_만큼_약하다()
        {
            var p = New();
            try
            {
                // §7.5 — 후처리는 "이미 존재하는 깊이층의 분리를 강화하는 용도로만" 쓴다.
                Assert.Less(p.fogDensity, 0.35f, "안개가 이보다 진하면 바닥 재질이 안 읽힌다");
                Assert.Less(p.depthSeparation, 0.2f, "색 분리는 층을 갈라 보이게만 한다");
                Assert.Less(p.grainStrength, 0.06f, "그레인은 밴딩을 깨는 정도면 충분하다");
                Assert.Less(p.vignetteStrength, 0.5f);
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void 안개_모양은_부드러움을_0_으로_두지_않는다()
        {
            var p = New();
            try
            {
                p.fogHeight = 0.4f;
                p.fogSoftness = 0f;      // 인스펙터 Range 를 우회해 코드로 넣은 경우
                var shape = p.FogShape;
                Assert.AreEqual(0.4f, shape.x, 1e-5f);
                Assert.Greater(shape.y, 0f, "0 이면 셰이더의 smoothstep 이 띠처럼 끊긴다");
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void 암부_반경이_뒤집혀도_유효한_구간을_넘긴다()
        {
            var p = New();
            try
            {
                p.vignetteInner = 1.2f;
                p.vignetteOuter = 0.3f;   // 뒤집힘
                var v = p.VignetteShape;
                Assert.Greater(v.y, v.x, "outer 가 inner 보다 커야 smoothstep 이 정상 동작한다");
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void 암부_세기는_모양_벡터의_z_로_전달된다()
        {
            var p = New();
            try
            {
                p.vignetteStrength = 0.42f;
                Assert.AreEqual(0.42f, p.VignetteShape.z, 1e-5f);
            }
            finally { Object.DestroyImmediate(p); }
        }

        // ───────────────────────────── 결정성 (§16.3)

        [Test]
        public void 그레인을_고정하면_시간이_흘러도_0_이다()
        {
            var p = New();
            try
            {
                p.animateGrain = true;
                Assert.AreEqual(0f, AtmosphereDirector.GrainTime(p, freeze: true, time: 12.34f));
                Assert.AreEqual(0f, AtmosphereDirector.GrainTime(p, freeze: true, time: 987f));
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void 프로파일이_애니메이션을_끄면_고정하지_않아도_0_이다()
        {
            var p = New();
            try
            {
                p.animateGrain = false;
                Assert.AreEqual(0f, AtmosphereDirector.GrainTime(p, freeze: false, time: 5f));
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void 프로파일이_없으면_0_이다()
        {
            Assert.AreEqual(0f, AtmosphereDirector.GrainTime(null, freeze: false, time: 5f));
        }

        [Test]
        public void 애니메이션_그레인은_초당_24_스텝의_이산값이다()
        {
            var p = New();
            try
            {
                p.animateGrain = true;
                // 같은 1/24초 구간 안에서는 같은 값 — 프레임률에 따라 값이 흔들리지 않는다.
                Assert.AreEqual(AtmosphereDirector.GrainTime(p, false, 1.000f),
                                AtmosphereDirector.GrainTime(p, false, 1.020f));
                Assert.AreNotEqual(AtmosphereDirector.GrainTime(p, false, 1.000f),
                                   AtmosphereDirector.GrainTime(p, false, 1.100f));
            }
            finally { Object.DestroyImmediate(p); }
        }

        // ───────────────────────────── 디버그 격리

        [Test]
        public void 격리_순환은_여섯_층을_돌아_합성으로_돌아온다()
        {
            var go = new GameObject("AtmoTest");
            try
            {
                var d = go.AddComponent<AtmosphereDirector>();
                Assert.AreEqual(AtmosphereChannel.Composite, d.Isolate);

                Assert.AreEqual(AtmosphereChannel.FogOnly, d.CycleIsolate());
                Assert.AreEqual(AtmosphereChannel.DepthOnly, d.CycleIsolate());
                Assert.AreEqual(AtmosphereChannel.VignetteOnly, d.CycleIsolate());
                Assert.AreEqual(AtmosphereChannel.StateTintOnly, d.CycleIsolate());
                Assert.AreEqual(AtmosphereChannel.GrainOnly, d.CycleIsolate());
                Assert.AreEqual(AtmosphereChannel.Composite, d.CycleIsolate(),
                    "한 바퀴 돌면 합성으로 돌아와야 키 하나로 계속 순환할 수 있다");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void 셰이더가_프로젝트에_존재하고_지원된다()
        {
            var shader = Shader.Find("TunnelCrew/Atmosphere");
            Assert.IsNotNull(shader, "TunnelCrew/Atmosphere 셰이더를 찾지 못했다");
            Assert.IsTrue(shader.isSupported, "현재 그래픽스 API 에서 컴파일되지 않았다");
        }
    }
}
