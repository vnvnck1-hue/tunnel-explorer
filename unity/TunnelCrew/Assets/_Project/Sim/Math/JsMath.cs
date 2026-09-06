using System;

namespace TunnelCrew.Sim
{
    /// <summary>
    /// JavaScript 와 결과가 달라지는 수학 연산만 모아 둔 곳.
    /// 맵 생성 패리티가 여기에 걸려 있으므로 "더 정확한" 구현으로 바꾸지 말 것.
    /// </summary>
    public static class JsMath
    {
        /// <summary>
        /// JS <c>Math.round</c>. .NET 의 <c>Math.Round</c> 는 기본이 은행가 반올림이라
        /// 0.5 에서 결과가 다르다 (JS: 0.5 → 1, .NET: 0.5 → 0).
        /// JS 는 항상 <c>floor(x + 0.5)</c> 다.
        /// </summary>
        public static int Round(double x) => (int)Math.Floor(x + 0.5);

        /// <summary>JS <c>Math.floor</c> 후 int. 음수에서 C# 캐스팅(0 방향 절삭)과 다르다.</summary>
        public static int Floor(double x) => (int)Math.Floor(x);

        /// <summary>JS <c>Math.sign</c>.</summary>
        public static int Sign(double x) => x > 0 ? 1 : x < 0 ? -1 : 0;

        /// <summary>JS <c>Math.hypot</c>.</summary>
        public static double Hypot(double a, double b) => Math.Sqrt(a * a + b * b);

        /// <summary>JS <c>x|0</c> — 32비트 정수로 절삭.</summary>
        public static int ToInt32(double x) => unchecked((int)(long)x);

        public static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
        public static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
