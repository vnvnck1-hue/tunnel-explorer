using UnityEngine;
using TunnelCrew.Sim;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 투사체 한 종류의 시각 언어. 색만 바꾸지 않고 실루엣·코어·궤적·회전·환경광을 함께 바꾼다.
    /// 값은 런타임 자산을 만들지 않는 정적 데이터라 FTUE 씬/프리팹과 결합되지 않는다.
    /// </summary>
    public readonly struct ProjectileVfxProfile
    {
        public readonly string Id;
        public readonly Color Body, Core, Trail, Accent;
        public readonly float Length, Width, Shape, AccentMode;
        public readonly float TrailTime, TrailWidth, Spin, Pulse;
        public readonly float LightRadius, LightIntensity;
        public readonly int LightPriority;

        public ProjectileVfxProfile(
            string id, Color body, Color core, Color trail, Color accent,
            float length, float width, float shape, float accentMode,
            float trailTime, float trailWidth, float spin, float pulse,
            float lightRadius, float lightIntensity, int lightPriority)
        {
            Id = id;
            Body = body; Core = core; Trail = trail; Accent = accent;
            Length = length; Width = width; Shape = shape; AccentMode = accentMode;
            TrailTime = trailTime; TrailWidth = trailWidth; Spin = spin; Pulse = pulse;
            LightRadius = lightRadius; LightIntensity = lightIntensity; LightPriority = lightPriority;
        }
    }

    public static class ProjectileVfxProfiles
    {
        /// <summary>고속탄의 비행 방향과 궤적을 읽을 수 있도록 모든 탄체를 길이 방향으로 늘린다.</summary>
        public const float LengthScale = 3f;

        static Color C(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }

        // shape: 0=slug, 1=pellet, 2=needle, 3=diamond, 4=plasma,
        //        5=meteor, 6=beam, 7=hex cell, 8=crystal shard.
        public static readonly ProjectileVfxProfile Standard = new ProjectileVfxProfile(
            "standard", C("#FF9E32"), C("#FFF5C7"), C("#FF6A18"), C("#FFD36A"),
            .58f, .20f, 0, 0, .075f, .13f, 0, 34f, 1.20f, .46f, 2);

        public static readonly ProjectileVfxProfile Multi = new ProjectileVfxProfile(
            "multi", C("#FF6B21"), C("#FFF0B0"), C("#D93A13"), C("#FFB347"),
            .42f, .25f, 1, 1, .055f, .16f, 220f, 46f, .95f, .34f, 1);

        public static readonly ProjectileVfxProfile Pierce = new ProjectileVfxProfile(
            "pierce", C("#2EDAFF"), Color.white, C("#167DFF"), C("#9EFFFF"),
            1.10f, .14f, 2, 2, .145f, .12f, 0, 58f, 1.90f, .72f, 7);

        public static readonly ProjectileVfxProfile Ricochet = new ProjectileVfxProfile(
            "ricochet", C("#A65CFF"), C("#F8E8FF"), C("#5D29D8"), C("#E696FF"),
            .58f, .25f, 3, 3, .18f, .15f, 420f, 50f, 1.45f, .58f, 5);

        public static readonly ProjectileVfxProfile Explosive = new ProjectileVfxProfile(
            "explosive", C("#FF3A16"), C("#FFF2A1"), C("#8F1211"), C("#FFB321"),
            .72f, .40f, 4, 4, .16f, .25f, -150f, 64f, 2.30f, .95f, 9);

        public static readonly ProjectileVfxProfile Rain = new ProjectileVfxProfile(
            "rain", C("#FF2917"), C("#FFF4BD"), C("#9B1710"), C("#FF7A20"),
            .92f, .34f, 5, 5, .20f, .23f, 95f, 70f, 2.05f, .84f, 8);

        public static readonly ProjectileVfxProfile Laser = new ProjectileVfxProfile(
            "laser", C("#21FFE0"), Color.white, C("#087EFF"), C("#B9FFFF"),
            1.48f, .15f, 6, 6, .22f, .14f, 0, 82f, 2.75f, 1.18f, 10);

        public static readonly ProjectileVfxProfile Support = new ProjectileVfxProfile(
            "support", C("#34F2A3"), C("#E2FFF4"), C("#147B67"), C("#7CFFD2"),
            .46f, .25f, 7, 7, .11f, .14f, 180f, 42f, 1.15f, .38f, 3);

        public static readonly ProjectileVfxProfile Shard = new ProjectileVfxProfile(
            "shard", C("#44FFE7"), C("#F2FFFF"), C("#167BBA"), C("#9DFFF6"),
            .72f, .14f, 8, 8, .16f, .11f, 260f, 60f, 1.35f, .50f, 4);

        public static ProjectileVfxProfile Get(string id)
        {
            switch (id)
            {
                case "multi": return Multi;
                case "pierce": return Pierce;
                case "ricochet": return Ricochet;
                case "explosive": return Explosive;
                case "rain": return Rain;
                case "laser": return Laser;
                case "support": return Support;
                case "shard": return Shard;
                default: return Standard;
            }
        }

        /// <summary>대표 탄형 위에 특성 플래그를 겹친다. 새 텍스처 없이 조합 빌드가 외형에도 남는다.</summary>
        public static ProjectileVfxProfile Compose(string id, ProjectileStyleFlags flags)
        {
            var p = Get(id);
            Color body = p.Body, core = p.Core, trail = p.Trail, accent = p.Accent;
            float length = p.Length * LengthScale, width = p.Width, trailTime = p.TrailTime, trailWidth = p.TrailWidth;
            float spin = p.Spin, lightRadius = p.LightRadius, lightIntensity = p.LightIntensity;
            int priority = p.LightPriority;

            if ((flags & ProjectileStyleFlags.Multi) != 0 && id != "multi" && id != "rain")
            {
                width *= 1.16f; trailWidth *= 1.12f; spin += 90f;
                accent = Color.Lerp(accent, C("#FF9B45"), .35f);
            }
            if ((flags & ProjectileStyleFlags.Pierce) != 0 && id != "pierce" && id != "laser" && id != "shard")
            {
                length *= 1.22f; width *= .86f; trailTime *= 1.20f;
                trail = Color.Lerp(trail, C("#2CCBFF"), .52f); priority = Mathf.Max(priority, 7);
            }
            if ((flags & ProjectileStyleFlags.Ricochet) != 0 && id != "ricochet")
            {
                spin += 320f; trailTime *= 1.22f;
                accent = Color.Lerp(accent, C("#BB68FF"), .62f); trail = Color.Lerp(trail, C("#6B38E8"), .52f);
            }
            if ((flags & ProjectileStyleFlags.Explosive) != 0 && id != "explosive" && id != "rain" && id != "laser")
            {
                width *= 1.18f; trailWidth *= 1.25f;
                body = Color.Lerp(body, C("#FF3B16"), .34f); accent = Color.Lerp(accent, C("#FFB321"), .55f);
                lightRadius *= 1.24f; lightIntensity *= 1.25f; priority = Mathf.Max(priority, 8);
            }
            return new ProjectileVfxProfile(p.Id, body, core, trail, accent, length, width, p.Shape, p.AccentMode,
                trailTime, trailWidth, spin, p.Pulse, lightRadius, lightIntensity, priority);
        }
    }
}
