using System;

namespace TunnelCrew.Sim
{
    /// <summary>
    /// 원본 v7.9.2 의 <c>mul()</c> (mulberry32, 2478행) 과 <c>hashSeed()</c> (FNV-1a, 2369행) 를
    /// 비트 단위로 동일하게 옮긴 것. 맵 생성 패리티 테스트가 이 동일성에 의존하므로
    /// 알고리즘을 "개선"하지 말 것.
    ///
    /// JS 의 <c>Math.imul</c> · <c>&gt;&gt;&gt;</c> · <c>|0</c> 은 전부 32비트 정수 연산이므로
    /// C# 에서는 <see cref="uint"/> + <c>unchecked</c> 로 같은 비트 결과를 얻는다.
    /// </summary>
    public struct Rng
    {
        uint _state;

        public Rng(uint seed) => _state = seed;

        /// <summary>원본: <c>hashSeed(s)</c> — FNV-1a 32비트.</summary>
        public static uint HashSeed(string s)
        {
            uint h = 2166136261u;
            for (int i = 0; i < s.Length; i++)
            {
                // JS charCodeAt 은 UTF-16 코드 단위. C# 의 char 와 동일하다.
                h ^= s[i];
                h = unchecked(h * 16777619u);
            }
            return h;
        }

        /// <summary>
        /// 원본: <c>genTunnel()</c> 5649~5650행의 시드 조합.
        /// <c>hashSeed(seed|depth|COLSxROWS) ^ (depth*104729) ^ 0x9e3779b9</c>
        /// </summary>
        public static Rng ForTunnel(string seed, int depth, int cols, int rows)
        {
            uint h = HashSeed($"{seed}|{depth}|{cols}x{rows}");
            uint mixed = unchecked(h ^ (uint)(depth * 104729) ^ 0x9e3779b9u);
            return new Rng(mixed);
        }

        /// <summary>원본 <c>mul(a)()</c> 와 동일. [0, 1) 구간.</summary>
        public double NextDouble()
        {
            unchecked
            {
                _state += 0x6D2B79F5u;
                uint a = _state;
                uint t = (a ^ (a >> 15)) * (1u | a);
                t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        public float NextFloat() => (float)NextDouble();

        /// <summary>[0, max) 정수. 원본의 <c>Math.floor(R()*max)</c> 패턴과 동일.</summary>
        public int NextInt(int maxExclusive) => (int)(NextDouble() * maxExclusive);

        /// <summary>[min, max) 정수.</summary>
        public int Range(int minInclusive, int maxExclusive)
            => minInclusive + NextInt(maxExclusive - minInclusive);

        /// <summary>[min, max) 실수. 원본의 <c>a + R()*(b-a)</c> 패턴과 동일.</summary>
        public double Range(double min, double max) => min + NextDouble() * (max - min);

        /// <summary>현재 내부 상태. 패리티 테스트에서 단계별 스냅샷 비교에 쓴다.</summary>
        public uint State => _state;
    }
}
