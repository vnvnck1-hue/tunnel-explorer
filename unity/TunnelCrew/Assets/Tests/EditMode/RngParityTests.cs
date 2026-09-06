using NUnit.Framework;
using TunnelCrew.Sim;

namespace TunnelCrew.Tests
{
    /// <summary>
    /// 기준값은 원본 v7.9.2 의 <c>mul()</c> / <c>hashSeed()</c> 를 Node 에서 그대로 실행해 뽑았다.
    /// 이 테스트가 깨지면 맵 생성 패리티(계획 §6)가 전부 무너지므로 값을 고치지 말고 구현을 고칠 것.
    /// </summary>
    public class RngParityTests
    {
        [TestCase("tunnel-891730050", 3061167471u)]
        [TestCase("tunnel-891730050|1|80x72", 2942536609u)]
        [TestCase("", 2166136261u)]
        [TestCase("a", 3826002220u)]
        [TestCase("땅굴크루", 3334823336u)]
        public void HashSeed_MatchesOriginal(string input, uint expected)
        {
            Assert.AreEqual(expected, Rng.HashSeed(input));
        }

        [TestCase(1, 827681537u)]
        [TestCase(2, 1736277567u)]
        [TestCase(3, 1280121909u)]
        public void ForTunnel_InitialStateMatchesOriginal(int depth, uint expectedState)
        {
            var rng = Rng.ForTunnel("tunnel-891730050", depth, 80, 72);
            Assert.AreEqual(expectedState, rng.State);
        }

        [Test]
        public void NextDouble_MatchesOriginal_ForFixedSeed()
        {
            // 원본: mul(12345) 를 8회 호출한 결과
            double[] expected =
            {
                0.97972826776094735,
                0.30675226449966431,
                0.48420542152598500,
                0.81793441250920296,
                0.50942836934700608,
                0.34747186047025025,
                0.07375754183158278,
                0.76639646734111011,
            };

            var rng = new Rng(12345u);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], rng.NextDouble(), 1e-15, $"draw #{i}");
            }
        }

        [Test]
        public void NextDouble_MatchesOriginal_ForTunnelDepth1()
        {
            // 원본: genTunnel(1) 이 만드는 R() 의 첫 5개
            double[] expected =
            {
                0.19432813837192953,
                0.42426706757396460,
                0.88792927190661430,
                0.84760659351013601,
                0.46775038912892342,
            };

            var rng = Rng.ForTunnel("tunnel-891730050", 1, 80, 72);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], rng.NextDouble(), 1e-15, $"draw #{i}");
            }
        }

        [Test]
        public void NextDouble_StaysInUnitInterval()
        {
            var rng = new Rng(1u);
            for (int i = 0; i < 100000; i++)
            {
                double v = rng.NextDouble();
                Assert.GreaterOrEqual(v, 0.0);
                Assert.Less(v, 1.0);
            }
        }
    }
}
