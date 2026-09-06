using System;

namespace TunnelCrew.Sim
{
    /// <summary>
    /// Sim 전용 2D 벡터. UnityEngine.Vector2 를 쓰지 않는 이유는 Sim 어셈블리가
    /// <c>noEngineReferences</c> 이기 때문이고, float 가 아니라 double 인 이유는
    /// 원본 JS 가 전부 double 이라 패리티를 맞추기 위해서다.
    /// Presentation 이 경계에서 float 로 변환한다.
    /// </summary>
    public struct Vec2 : IEquatable<Vec2>
    {
        public double X, Y;

        public Vec2(double x, double y) { X = x; Y = y; }

        public static readonly Vec2 Zero = new Vec2(0, 0);

        public double Length => Math.Sqrt(X * X + Y * Y);
        public double SqrLength => X * X + Y * Y;

        public Vec2 Normalized
        {
            get
            {
                double l = Length;
                return l > 1e-12 ? new Vec2(X / l, Y / l) : Zero;
            }
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, double s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(double s, Vec2 a) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator /(Vec2 a, double s) => new Vec2(a.X / s, a.Y / s);

        public static double Distance(Vec2 a, Vec2 b) => (a - b).Length;
        public static Vec2 FromAngle(double radians) => new Vec2(Math.Cos(radians), Math.Sin(radians));
        public double Angle => Math.Atan2(Y, X);

        public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
        public override string ToString() => $"({X:F3}, {Y:F3})";
    }
}
