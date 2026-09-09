using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// Material Mask 채널 규약을 고정한다 — 정답지는
    /// `docs/unity-port/mask-channel-convention.md`.
    ///
    /// <b>왜 테스트로 묶는가</b> — 2026-09-09 이전에는 마스크 블렌드 스타일 두 슬롯이
    /// <b>둘 다 R</b> 이었다. 채널이 같으면 "금속에만 곱하는 빛" 과 "금속에만 더하는 빛" 이
    /// 되어 광택·결정 용도가 성립하지 않는다. 화면에 오류가 뜨지 않고 조용히 무의미해지는
    /// 종류의 결함이라, 눈으로는 다시 새어 나간다.
    ///
    /// 아트 46장이 `R 금속 · G 광택 · B 습윤/결정 · A 효과강도` 로 그려져 있으므로
    /// 슬롯을 그 의미에 맞춰 옮겼다(G=Multiply, B=Additive).
    /// </summary>
    public class MaskChannelConventionTests
    {
        // URP Light2DBlendStyle.TextureChannel — None 0 · R 1 · G 2 · B 3 · A 4
        const int None = 0, R = 1, G = 2, B = 3;
        // Light2DBlendStyle.BlendMode — Additive 0 · Multiply 1
        const int Additive = 0, Multiply = 1;

        const string RendererPath = "Assets/Settings/Renderer2D.asset";

        static SerializedProperty Styles()
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
            Assert.IsNotNull(data, $"{RendererPath} 를 찾지 못했다");
            var styles = new SerializedObject(data).FindProperty("m_LightBlendStyles");
            Assert.IsNotNull(styles, "m_LightBlendStyles 가 없다 — URP 버전이 바뀌었는지 확인할 것");
            return styles;
        }

        static (string name, int channel, int mode) Style(int index)
        {
            var s = Styles();
            Assert.Greater(s.arraySize, index, $"블렌드 스타일 슬롯 {index} 가 없다");
            var e = s.GetArrayElementAtIndex(index);
            return (e.FindPropertyRelative("name").stringValue,
                    e.FindPropertyRelative("maskTextureChannel").intValue,
                    e.FindPropertyRelative("blendMode").intValue);
        }

        [Test]
        public void 블렌드_스타일이_네_개다()
        {
            // 슬롯 번호가 곧 계약이다(코드가 blendStyleIndex 로 가리킨다). 개수가 바뀌면 번호가 밀린다.
            Assert.AreEqual(4, Styles().arraySize);
        }

        [Test]
        public void 슬롯0_1은_마스크_없는_일반_광원이다()
        {
            var multiply = Style(0);
            Assert.AreEqual(None, multiply.channel, "일반 Multiply 는 마스크를 보지 않는다");
            Assert.AreEqual(Multiply, multiply.mode);

            var additive = Style(1);
            Assert.AreEqual(None, additive.channel);
            Assert.AreEqual(Additive, additive.mode);
        }

        [Test]
        public void 슬롯2_Multiply_with_Mask_는_G_광택_이다()
        {
            var s = Style(2);
            Assert.AreEqual(Multiply, s.mode);
            Assert.AreEqual(G, s.channel,
                "G = 광택. 크루 실루엣 림과 금속 모서리가 이 채널을 쓴다");
        }

        [Test]
        public void 슬롯3_Additive_with_Mask_는_B_습윤결정_이다()
        {
            var s = Style(3);
            Assert.AreEqual(Additive, s.mode);
            Assert.AreEqual(B, s.channel,
                "B = 습윤·결정. 광맥 발광이 이 채널을 쓴다(라이트 하나로 화면 전체 광맥)");
        }

        [Test]
        public void 두_마스크_슬롯은_서로_다른_채널을_본다()
        {
            // 이게 깨져 있던 결함이다 — 둘 다 R 이면 용도 분리가 성립하지 않는다.
            Assert.AreNotEqual(Style(2).channel, Style(3).channel,
                "마스크 슬롯이 같은 채널을 보면 광택·결정 용도가 겹친다");
        }

        [Test]
        public void 금속_R_은_아직_어느_슬롯도_쓰지_않는다()
        {
            // 계약상 예약 채널. 나중에 금속 전용 라이트를 넣을 때 이 테스트를 고친다.
            for (int i = 0; i < Styles().arraySize; i++)
                Assert.AreNotEqual(R, Style(i).channel,
                    $"슬롯 {i} 가 R(금속)을 쓴다 — 규약 문서를 먼저 갱신할 것");
        }
    }
}
