using UnityEngine;
using UnityEngine.Tilemaps;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>
    /// 벽 정면 위에 얹는 광맥 오버레이(4차 아트 §4 · 코어키퍼 <c>oreFront</c>). 채굴 대상(Ore·Gem·Crys)인 고체 셀의
    /// 남향 정면에만 그린다 — 남쪽이 막힌 광맥은 cap 만 보이므로 표시하지 않는다(요청서 §4 "정면에 얹는").
    ///
    /// 두 타일맵을 쓴다. albedo 는 정면 재질을 복제해 채널 맵(노멀·이미션·AO)을 뗀 것 — 개별 스프라이트는 아틀라스 UV 가 아니라서
    /// 정면 재질의 노멀 아틀라스를 그대로 빌리면 엉뚱한 자리를 샘플한다. 이미션은 Unlit 으로 빛과 무관하게 발광한다.
    /// </summary>
    public sealed class WallOreOverlay : MonoBehaviour
    {
        Tilemap _albedo, _emission;
        Tile[] _tiles, _emit;
        Material _albedoMat, _emitMat;

        public bool Ready => _albedo != null && _tiles != null && _tiles.Length > 0;

        /// <summary><paramref name="host"/> 는 정면(BackStructure) 타일맵 렌더러. 키트에 광맥 아트가 없으면 비활성.</summary>
        public void Bind(EnvironmentKit kit, TilemapRenderer host)
        {
            if (kit == null || kit.oreFront == null || kit.oreFront.Length == 0) { Clear(); _tiles = null; return; }
            if (GetComponent<Grid>() == null) gameObject.AddComponent<Grid>().cellSize = new Vector3(1f, 1f, 0f);

            if (_albedo == null)
            {
                _albedoMat = OverlayMaterials.LitWithoutChannels(host != null ? host.sharedMaterial : null, "OreFront-Lit");
                _emitMat = OverlayMaterials.Unlit("OreFront-Emission");
                _albedo = MakeMap("Ore Albedo", host, 1, _albedoMat);
                _emission = MakeMap("Ore Emission", host, 3, _emitMat);   // 균열(+2) 위에서 빛난다
            }
            else { _albedo.ClearAllTiles(); _emission.ClearAllTiles(); }

            _tiles = MakeTiles(kit.oreFront);
            _emit = kit.oreFrontEmission != null && kit.oreFrontEmission.Length == kit.oreFront.Length ? MakeTiles(kit.oreFrontEmission) : null;
        }

        Tilemap MakeMap(string name, TilemapRenderer host, int orderOffset, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var map = go.AddComponent<Tilemap>();
            map.tileAnchor = new Vector3(0.5f, 0f, 0f);   // 정면과 같은 발점(셀 남쪽 경계)
            var tr = go.AddComponent<TilemapRenderer>();
            tr.mode = TilemapRenderer.Mode.Individual;
            tr.sortOrder = TilemapRenderer.SortOrder.TopLeft;
            if (host != null) { tr.sortingLayerName = host.sortingLayerName; tr.sortingOrder = host.sortingOrder + orderOffset; }
            else if (VisualLayers.Exists(VisualLayers.BackStructure)) tr.sortingLayerName = VisualLayers.BackStructure;
            if (material != null) tr.sharedMaterial = material;
            return map;
        }

        static Tile[] MakeTiles(Sprite[] sprites)
        {
            var tiles = new Tile[sprites.Length];
            for (int i = 0; i < tiles.Length; i++)
            {
                var t = ScriptableObject.CreateInstance<Tile>();
                t.sprite = sprites[i];
                t.colliderType = Tile.ColliderType.None;
                tiles[i] = t;
            }
            return tiles;
        }

        /// <summary>셀에 광맥 정면을 놓거나 지운다. <paramref name="variantHash"/> 로 a/b 를 결정적으로 고른다.</summary>
        public void Set(int col, int row, bool ore, uint variantHash)
        {
            if (!Ready) return;
            var pos = new Vector3Int(col, row, 0);
            if (!ore) { _albedo.SetTile(pos, null); _emission.SetTile(pos, null); return; }
            int i = (int)(variantHash % (uint)_tiles.Length);
            _albedo.SetTile(pos, _tiles[i]);
            _emission.SetTile(pos, _emit != null ? _emit[i] : null);
        }

        public void Clear()
        {
            if (_albedo != null) _albedo.ClearAllTiles();
            if (_emission != null) _emission.ClearAllTiles();
        }

        void OnDestroy()
        {
            if (_albedoMat != null) Destroy(_albedoMat);
            if (_emitMat != null) Destroy(_emitMat);
        }
    }

    /// <summary>
    /// 정면 위 오버레이(균열·광맥)가 쓰는 재질. 정면 재질을 복제해 <b>채널 맵만 뗀다</b> — 최소광·어둠 색·마스크(black) 는
    /// 그대로 받아 같은 빛을 받고, 노멀·이미션·AO 아틀라스는 개별 스프라이트 UV 와 맞지 않으므로 기본값(평면·비발광·차폐 없음)으로 되돌린다.
    /// Sprite-Lit-Default 로 두면 <c>_MaskTex</c> 기본이 white 라 마스크 광원에 날아간다(SurfaceMaterialSet 주석).
    /// </summary>
    public static class OverlayMaterials
    {
        public static Material LitWithoutChannels(Material host, string name)
        {
            Material m;
            if (host != null)
            {
                m = new Material(host) { name = name };
                if (m.HasProperty("_NormalMap")) m.SetTexture("_NormalMap", null);
                if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", null);
                if (m.HasProperty("_AOMap")) m.SetTexture("_AOMap", null);
                if (m.HasProperty("_MaskTex")) m.SetTexture("_MaskTex", Texture2D.blackTexture);
                return m;
            }
            var lit = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            return lit != null ? new Material(lit) { name = name } : null;
        }

        public static Material Unlit(string name)
        {
            var unlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            return unlit != null ? new Material(unlit) { name = name } : null;
        }
    }
}
