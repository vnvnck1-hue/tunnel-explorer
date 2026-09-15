using System.Collections.Generic;
using UnityEngine;

namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>
    /// 로드맵 4.3 — 피격 연출 스택.
    /// 반응 세기별 프리셋 하나가 히트 스톱 · 카메라 킥 · 줌 펀치 · 플래시 ·
    /// 불꽃 → 파편 → 연기 → 바닥 흔적의 시간차 재생을 함께 결정한다.
    /// </summary>
    public sealed class ModularGunnerEffects : MonoBehaviour
    {
        public enum Reaction
        {
            Light,
            Heavy,
            Critical,
            Death,
        }

        /// <summary>한 번의 타격이 만들어 내는 모든 반응값. 여기만 고치면 손맛이 바뀐다.</summary>
        readonly struct Preset
        {
            public readonly float HitStop;
            public readonly float Shake;
            public readonly float ShakeHighRatio;
            public readonly float Kick;
            public readonly float ZoomPunch;
            public readonly float BurstLife;
            public readonly float BurstScale;
            public readonly int Shards;
            public readonly float ShardSpeed;
            public readonly float DecalScale;
            public readonly bool Shockwave;

            public Preset(float hitStop, float shake, float shakeHighRatio, float kick, float zoomPunch,
                float burstLife, float burstScale, int shards, float shardSpeed, float decalScale, bool shockwave)
            {
                HitStop = hitStop;
                Shake = shake;
                ShakeHighRatio = shakeHighRatio;
                Kick = kick;
                ZoomPunch = zoomPunch;
                BurstLife = burstLife;
                BurstScale = burstScale;
                Shards = shards;
                ShardSpeed = shardSpeed;
                DecalScale = decalScale;
                Shockwave = shockwave;
            }
        }

        static readonly Dictionary<Reaction, Preset> Presets = new()
        {
            // 히트 스톱은 로드맵이 못박은 20~55ms 범위 안에서만 움직인다.
            // 약한 타격일수록 고주파(잘게 떨림), 사망에 가까울수록 저주파(크게 밀림)로 간다.
            [Reaction.Light] = new Preset(0.020f, 0.30f, 0.85f, 0.22f, 0.00f, 0.10f, 1.20f, 6, 4.2f, 0.70f, false),
            [Reaction.Heavy] = new Preset(0.034f, 0.46f, 0.60f, 0.42f, 0.28f, 0.14f, 1.75f, 9, 5.2f, 0.95f, false),
            [Reaction.Critical] = new Preset(0.046f, 0.64f, 0.38f, 0.58f, 0.58f, 0.18f, 2.20f, 12, 6.0f, 1.15f, true),
            [Reaction.Death] = new Preset(0.055f, 0.86f, 0.18f, 0.74f, 1.00f, 0.24f, 2.60f, 15, 6.8f, 1.35f, true),
        };

        struct Stage
        {
            public float At;
            public int Kind;
            public Vector2 Position;
            public Vector2 Incoming;
            public Reaction Reaction;
        }

        const int StageSmoke = 0;
        const int StageGroundMark = 1;

        // 승인 VFX 시트는 프레임이 32px(2유닛)이다. 기존 버스트 스프라이트는 16px(1유닛)이라
        // 같은 BurstScale 을 그대로 쓰면 두 배로 커진다. 시트에는 이 보정을 곱한다.
        const float SheetScale = 0.5f;

        [SerializeField] Sprite _casingSprite;
        [SerializeField] Sprite _stoneShardSprite;
        [SerializeField] Sprite _enemyShardSprite;
        [SerializeField] Sprite _sparkSprite;
        [SerializeField] Sprite _burstSprite;
        [SerializeField] Sprite _shadowSprite;
        [SerializeField] Material _litMaterial;
        [SerializeField] Material _unlitMaterial;
        [SerializeField] ModularGunnerCameraRig _cameraRig;

        [Header("승인 VFX 시트 — 로드맵 4.3 · 4.9")]
        [SerializeField] Sprite[] _shockwaveFrames;
        [SerializeField] Sprite[] _smokeFrames;
        [SerializeField] Sprite[] _sparkFrames;
        [SerializeField] Sprite[] _enemyShards;

        public static ModularGunnerEffects Instance { get; private set; }

        readonly List<Stage> _stages = new(32);

        public void EditorAssign(Sprite casing, Sprite stoneShard, Sprite enemyShard, Sprite spark,
            Sprite burst, Sprite shadow, Material litMaterial, Material unlitMaterial, ModularGunnerCameraRig cameraRig)
        {
            _shadowSprite = shadow;
            _casingSprite = casing;
            _stoneShardSprite = stoneShard;
            _enemyShardSprite = enemyShard;
            _sparkSprite = spark;
            _burstSprite = burst;
            _litMaterial = litMaterial;
            _unlitMaterial = unlitMaterial;
            _cameraRig = cameraRig;
        }

        public void EditorAssignSheets(Sprite[] shockwave, Sprite[] smoke, Sprite[] spark, Sprite[] enemyShards)
        {
            _shockwaveFrames = shockwave;
            _smokeFrames = smoke;
            _sparkFrames = spark;
            _enemyShards = enemyShards;
        }

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Muzzle(Vector2 position, Vector2 aim)
            => Muzzle(position, aim, 0.18f, 0.95f, 0.12f);

        /// <summary>총기별 흔들림 프리셋을 그대로 받는다(로드맵 4.11).</summary>
        public void Muzzle(Vector2 position, Vector2 aim, float shake, float shakeHighRatio, float kick)
        {
            Vector2 side = new(-aim.y, aim.x);
            Vector2 eject = -aim * Random.Range(0.4f, 1.1f) + side * Random.Range(2.2f, 3.8f);
            SpawnDebris("Casing", position - aim * 0.2f + side * 0.1f, _casingSprite,
                new Color(1f, 0.72f, 0.18f), eject, Random.Range(2.1f, 3.4f), Random.Range(2.8f, 4.8f),
                Random.Range(0.7f, 1.05f), Random.Range(-760f, 760f), 0.48f, 2);
            SpawnBurst("Muzzle Light", position, new Color(1f, 0.72f, 0.2f, 0.9f), 0.075f, 0.3f, 1.15f, 65, 3.5f,
                ModularGunnerLighting.MuzzleIntensity);
            _cameraRig?.AddShake(shake, shakeHighRatio);
            // 반동은 총구 반대쪽으로 민다. 연사 중 방향이 읽히도록 약하게만 준다.
            _cameraRig?.AddKick(-aim, kick);
        }

        public void WallImpact(Vector2 position, Vector2 incoming)
        {
            Vector2 back = -incoming.normalized;
            SpawnBurst("Wall Impact", position, new Color(1f, 0.78f, 0.3f, 0.95f), 0.13f, 0.25f, 1.55f, 64, 2.2f,
                ModularGunnerLighting.ImpactIntensity * 1.25f);
            for (int i = 0; i < 7; i++)
            {
                Vector2 direction = (back + Random.insideUnitCircle * 0.85f).normalized;
                SpawnDebris("Stone Chip", position, i < 3 ? _sparkSprite : _stoneShardSprite,
                    i < 3 ? new Color(1f, 0.74f, 0.24f) : new Color(0.46f, 0.43f, 0.4f),
                    direction * Random.Range(1.4f, 4.8f), Random.Range(1.4f, 3.8f), Random.Range(0.75f, 1.65f),
                    Random.Range(0.65f, 1.2f), Random.Range(-620f, 620f), 0.32f);
            }
            _cameraRig?.AddShake(0.42f, 0.7f);
            _cameraRig?.AddKick(incoming, 0.3f);
            // 탄흔은 즉시 남기고, 바닥으로 흘러내린 먼지 자국은 조금 늦게 찍는다.
            // 벽 정면이 아주 어두워 원색 그대로 찍으면 자갈만 반짝여 보인다.
            // 톤을 눌러 파인 자국으로 읽히게 한다.
            ModularGunnerDecals.Instance?.Spawn(ModularGunnerDecals.Surface.Wall, position,
                new Color(0.62f, 0.62f, 0.7f, 0.85f), Random.Range(0.85f, 1.15f));
            Schedule(StageGroundMark, 0.09f, position + Vector2.down * 0.28f, incoming, Reaction.Light);
        }

        public void EnemyImpact(Vector2 position, Vector2 incoming, bool killed)
            => EnemyImpact(position, incoming, killed ? Reaction.Death : Reaction.Light);

        public void EnemyImpact(Vector2 position, Vector2 incoming, Reaction reaction)
        {
            Preset preset = Presets[reaction];
            bool lethal = reaction == Reaction.Death;
            Color hot = lethal ? new Color(1f, 0.26f, 0.12f) : new Color(1f, 0.88f, 0.42f);
            Vector2 center = position + Vector2.up * 0.35f;

            ModularGunnerHitStop.Instance?.Hold(preset.HitStop);
            ModularGunnerCombatHud.Instance?.Report(position, reaction);
            _cameraRig?.AddShake(preset.Shake, preset.ShakeHighRatio);
            _cameraRig?.AddKick(incoming, preset.Kick);
            if (preset.ZoomPunch > 0f) _cameraRig?.AddZoomPunch(preset.ZoomPunch);

            // 1단계 — 불꽃. 전용 스파크 시트가 있으면 그것으로 친다.
            if (_sparkFrames != null && _sparkFrames.Length > 0)
            {
                SpawnSheet(lethal ? "Enemy Burst" : "Enemy Hit", _sparkFrames, center, hot,
                    preset.BurstLife * 1.6f, preset.BurstScale * 0.8f * SheetScale, 66,
                    lethal ? 3.4f : 2.2f,
                    lethal ? ModularGunnerLighting.LethalImpactIntensity : ModularGunnerLighting.ImpactIntensity);
            }
            else
            {
                SpawnBurst(lethal ? "Enemy Burst" : "Enemy Hit", center, hot,
                    preset.BurstLife, 0.3f, preset.BurstScale, 66, lethal ? 3.4f : 2.2f,
                    lethal ? ModularGunnerLighting.LethalImpactIntensity : ModularGunnerLighting.ImpactIntensity);
            }
            if (preset.Shockwave)
            {
                // 충격파 링: 전용 시트가 확산을 그리므로 크기는 고정하고 옅게만 깐다.
                SpawnSheet("Shockwave", _shockwaveFrames, center, new Color(1f, 0.92f, 0.78f, 0.55f),
                    0.2f, preset.BurstScale * 1.7f * SheetScale, 62);
            }

            // 2단계 — 파편.
            for (int i = 0; i < preset.Shards; i++)
            {
                Vector2 direction = (-incoming.normalized + Random.insideUnitCircle * (lethal ? 1.8f : 0.9f)).normalized;
                Sprite shard = _enemyShards != null && _enemyShards.Length > 0
                    ? _enemyShards[Random.Range(0, _enemyShards.Length)]
                    : _enemyShardSprite;
                SpawnDebris("Enemy Pixel", position + Vector2.up * 0.28f, shard,
                    Color.Lerp(new Color(0.32f, 0.05f, 0.04f), hot, Random.value),
                    direction * Random.Range(1.5f, preset.ShardSpeed), Random.Range(1.8f, lethal ? 5.5f : 3.8f),
                    Random.Range(0.8f, lethal ? 2.1f : 1.35f), Random.Range(0.7f, 1.25f),
                    Random.Range(-700f, 700f), 0.34f);
            }

            // 3단계 — 연기, 4단계 — 바닥 흔적.
            Schedule(StageSmoke, 0.055f, center, incoming, reaction);
            Schedule(StageGroundMark, 0.13f, position, incoming, reaction);
        }

        void Schedule(int kind, float delay, Vector2 position, Vector2 incoming, Reaction reaction)
        {
            _stages.Add(new Stage
            {
                At = Time.unscaledTime + delay,
                Kind = kind,
                Position = position,
                Incoming = incoming,
                Reaction = reaction,
            });
        }

        void Update()
        {
            if (_stages.Count == 0) return;
            float now = Time.unscaledTime;
            for (int i = _stages.Count - 1; i >= 0; i--)
            {
                Stage stage = _stages[i];
                if (now < stage.At) continue;
                _stages.RemoveAt(i);
                PlayStage(stage);
            }
        }

        void PlayStage(Stage stage)
        {
            Preset preset = Presets[stage.Reaction];
            if (stage.Kind == StageSmoke)
            {
                SpawnSheet("Impact Smoke", _smokeFrames, stage.Position + Vector2.up * 0.1f,
                    new Color(0.62f, 0.6f, 0.68f, 0.5f), 0.34f, preset.BurstScale * 1.1f * SheetScale, 60);
                return;
            }

            var surface = stage.Reaction == Reaction.Light && stage.Kind == StageGroundMark
                ? ModularGunnerDecals.Surface.Floor
                : ModularGunnerDecals.Surface.Splat;
            ModularGunnerDecals.Instance?.Spawn(surface, stage.Position,
                new Color(1f, 1f, 1f, 0.8f), preset.DecalScale * Random.Range(0.85f, 1.15f));
        }

        void SpawnDebris(string name, Vector2 position, Sprite sprite, Color color, Vector2 velocity,
            float verticalSpeed, float life, float scale, float spin, float bounce, int sortingOrder = 350)
        {
            if (sprite == null) return;
            ModularGunnerDebris debris = ModularGunnerFxPool.Instance?.RentDebris();
            if (debris == null) return;
            debris.name = name;
            debris.transform.position = position;
            debris.Prepare(sprite, _shadowSprite, _litMaterial, color, velocity,
                verticalSpeed, life, scale, spin, bounce, sortingOrder);
        }

        /// <summary>승인 VFX 시트를 프레임 순서대로 한 번 재생한다.</summary>
        void SpawnSheet(string name, Sprite[] frames, Vector2 position, Color color, float life,
            float scale, int sortingOrder, float lightRadius = 0f, float lightIntensity = 0f)
        {
            if (frames == null || frames.Length == 0) return;
            ModularGunnerFxBurst burst = ModularGunnerFxPool.Instance?.RentBurst();
            if (burst == null) return;
            color.a = Mathf.Min(color.a, ModularGunnerLighting.MaxFlashAlpha);
            burst.name = name;
            burst.transform.position = position;
            burst.Prepare(frames[0], frames, _unlitMaterial, color, life, scale, scale,
                sortingOrder + 300, lightRadius, lightIntensity);
        }

        void SpawnBurst(string name, Vector2 position, Color color, float life, float startScale,
            float endScale, int sortingOrder, float lightRadius, float lightIntensity)
        {
            if (_burstSprite == null) return;
            ModularGunnerFxBurst burst = ModularGunnerFxPool.Instance?.RentBurst();
            if (burst == null) return;
            // 아무리 밝은 효과라도 뒤에 선 캐릭터의 외곽선은 남겨야 한다(로드맵 4.5).
            color.a = Mathf.Min(color.a, ModularGunnerLighting.MaxFlashAlpha);
            burst.name = name;
            burst.transform.position = position;
            burst.Prepare(_burstSprite, _unlitMaterial, color,
                life, startScale, endScale, sortingOrder + 300, lightRadius, lightIntensity);
        }
    }
}
