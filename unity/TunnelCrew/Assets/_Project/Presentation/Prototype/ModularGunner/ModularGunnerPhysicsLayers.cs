using UnityEngine;

namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>프로토타입 전용 2D 물리 레이어와 충돌 관계를 한 곳에서 관리한다.</summary>
    public static class ModularGunnerPhysicsLayers
    {
        public const int Player = 8;
        public const int Enemy = 9;
        public const int Projectile = 10;
        public const int Debris = 11;
        public const int World = 12;

        public const int SpawnBlockingMask =
            (1 << Player) | (1 << Enemy) | (1 << World);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ConfigureBeforeSceneLoad() => ConfigureCollisionMatrix();

        public static void ConfigureCollisionMatrix()
        {
            int[] layers = { Player, Enemy, Projectile, Debris, World };
            for (int a = 0; a < layers.Length; a++)
            for (int b = a; b < layers.Length; b++)
                Physics2D.IgnoreLayerCollision(layers[a], layers[b], true);

            Allow(Player, Enemy);
            Allow(Player, World);
            Allow(Enemy, World);
            Allow(Projectile, Enemy);
            Allow(Projectile, World);
            Allow(Debris, World);
        }

        public static void Assign(GameObject target, int layer)
        {
            if (target != null) target.layer = layer;
        }

        static void Allow(int a, int b) => Physics2D.IgnoreLayerCollision(a, b, false);
    }
}
