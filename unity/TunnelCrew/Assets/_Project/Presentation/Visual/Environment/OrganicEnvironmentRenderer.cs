using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>Main-game counterpart of BackgroundUpgradeLab C. Keeps the existing surface,
    /// boss/ore/crack, lighting and occlusion contracts; rebuilds only the owner's dirty chunks.</summary>
    [DefaultExecutionOrder(120)]
    public sealed class OrganicEnvironmentRenderer : MonoBehaviour
    {
        sealed class Chunk
        {
            public GameObject root;
            public readonly List<Mesh> meshes = new();
            public readonly List<MeshRenderer> foreground = new();
            public readonly List<SpriteRenderer> foregroundProps = new();
            public ForegroundOccluder occluder;
            public float alpha = -1;
        }
        EnvironmentChunkRenderer _owner;
        ISolidField _field;
        OrganicEnvironmentStyle _style;
        Material _cap, _front, _contact, _props;
        Chunk[] _chunks;
        ForegroundOccluder[] _occluders;
        MaterialPropertyBlock _block;
        public int RebuildCount { get; private set; }
        public double LastChunkMs { get; private set; }
        public int VertexCount { get; private set; }
        public int ChunkCount => _chunks?.Length ?? 0;

        public bool Initialize(EnvironmentChunkRenderer owner, ISolidField field,
            SurfaceMaterialSet capSet, SurfaceMaterialSet frontSet, ForegroundOccluder[] occluders)
        {
            _style = Resources.Load<OrganicEnvironmentStyle>("Visual/OrganicEnvironmentStyle");
            if (_style == null || _style.rockShader == null || ! _style.rockShader.isSupported) return false;
            var capSprite = EnvironmentKit.Pick(owner.Kit.wallTop, 0);
            var frontSprite = EnvironmentKit.Pick(owner.Kit.wallFront, 0);
            if (capSprite == null || frontSprite == null) return false;
            _owner = owner; _field = field; _occluders = occluders;
            _cap = RockMaterial(capSet, capSprite, "Organic cap");
            _front = RockMaterial(frontSet, frontSprite, "Organic front");
            _contact = OverlayMaterials.Unlit("Organic contact AO");
            _props = new Material(Shader.Find(SurfaceMaterialSet.WorldShaderName)) { name = "Organic wall dressing" };
            _props.SetTexture("_MaskTex", Texture2D.blackTexture);
            _props.SetFloat("_NormalStrength", 0);
            _props.SetFloat("_ImpactWobble", 0);
            _props.SetFloat("_MinLight", owner.Profile != null ? owner.Profile.minLightBySurface.x : .03f);
            _block = new MaterialPropertyBlock();
            _chunks = new Chunk[SurfaceTopologyBuilder.ChunkCols(field.Cols) * SurfaceTopologyBuilder.ChunkRows(field.Rows)];
            for (int i = 0; i < _chunks.Length; i++) Rebuild(i);
            return true;
        }

        Material RockMaterial(SurfaceMaterialSet set, Sprite sprite, string label)
        {
            var m = new Material(_style.rockShader) { name = label };
            if (set != null) set.Apply(m, _owner.Profile);
            else { m.SetTexture("_MaskTex", Texture2D.blackTexture); m.SetFloat("_NormalStrength", 0); }
            m.SetTexture("_MainTex", sprite.texture);
            var r = sprite.textureRect;
            m.SetVector("_LabUVRect", new Vector4((r.x + r.width * .18f) / sprite.texture.width,
                (r.y + r.height * .18f) / sprite.texture.height,
                r.width * .64f / sprite.texture.width, r.height * .64f / sprite.texture.height));
            m.SetFloat("_LabMacro", _style.macroVariation);
            return m;
        }

        public void Rebuild(int index)
        {
            if (_chunks == null || index < 0 || index >= _chunks.Length) return;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            Release(_chunks[index]);
            var chunk = new Chunk { root = new GameObject("Organic chunk " + index), occluder = _occluders[index] };
            chunk.root.transform.SetParent(transform, false);
            _chunks[index] = chunk;
            int side = SurfaceTopologyBuilder.ChunkSize, cols = SurfaceTopologyBuilder.ChunkCols(_field.Cols);
            int x0 = index % cols * side, y0 = index / cols * side;
            var cap = new OrganicLabGeometry.Surface(); var fg = new OrganicLabGeometry.Surface();
            var front = new OrganicLabGeometry.Surface(); var contact = new OrganicLabGeometry.Surface();
            float lift = _owner.Profile != null ? _owner.Profile.wallLiftCells : 1f;
            var up = Vector2.up * lift;
            var capColor = new Color(.76f, .77f, .83f);
            for (int y = y0; y < Mathf.Min(y0 + side, _field.Rows); y++)
            for (int x = x0; x < Mathf.Min(x0 + side, _field.Cols); x++)
            {
                var cell = _owner.SurfaceAt(x, y);
                if ((cell.Surfaces & SurfaceMask.WallTop) == 0 || (cell.Surfaces & SurfaceMask.BossWall) != 0) continue;
                bool foreground = (cell.Surfaces & SurfaceMask.ForegroundTop) != 0;
                var top = foreground ? fg : cap;
                for (int sy = 0; sy < 4; sy++) for (int sx = 0; sx < 4; sx++)
                {
                    float a = x + sx * .25f, b = y + sy * .25f;
                    top.Quad(P(a,b)+up, P(a+.25f,b)+up, P(a+.25f,b+.25f)+up, P(a,b+.25f)+up, capColor, capColor);
                }
                if (!_field.IsSolid(x,y-1)) Edge(new Vector2(x,y), Vector2.right, top, front, contact, up, true);
                if (!_field.IsSolid(x+1,y)) Edge(new Vector2(x+1,y), Vector2.up, top, front, contact, up, false);
                if (!_field.IsSolid(x,y+1)) Edge(new Vector2(x+1,y+1), Vector2.left, top, front, contact, up, false);
                if (!_field.IsSolid(x-1,y)) Edge(new Vector2(x,y+1), Vector2.down, top, front, contact, up, false);
                // Sparse supported shoulders, never loose obstacles in the navigable floor.
                uint hash = SurfaceTopologyBuilder.Hash(x,y,0x0A61);
                if (_style.rock != null && hash % 17 == 0) AddRock(chunk, x, y, lift, foreground, hash);
            }
            AddMesh(chunk, cap, "Cap", _cap, VisualLayers.WallTop);
            AddMesh(chunk, fg, "Foreground cap", _cap, VisualLayers.FrontStructure, true);
            AddMesh(chunk, front, "Front skirt", _front, VisualLayers.BackStructure);
            AddMesh(chunk, contact, "Contact shadow", _contact, VisualLayers.GroundDecal);
            RebuildCount++; timer.Stop(); LastChunkMs = timer.Elapsed.TotalMilliseconds;
        }

        Vector2 P(float x, float y) => OrganicWallSampling.Point(_field,x,y);
        void Edge(Vector2 start, Vector2 direction, OrganicLabGeometry.Surface top,
            OrganicLabGeometry.Surface front, OrganicLabGeometry.Surface contact, Vector2 up, bool south)
        {
            for (int i = 0; i < 4; i++)
            {
                var p = start + direction * (i * .25f); var q = p + direction * .25f;
                var a = P(p.x,p.y); var b = P(q.x,q.y);
                var tangent = (b-a).normalized; var inward = new Vector2(-tangent.y,tangent.x);
                if (south) front.Quad(a,b,b+up,a+up,new Color(.40f,.42f,.50f),new Color(.88f,.84f,.90f));
                top.Quad(a+up,b+up,b+up+inward*.09f,a+up+inward*.09f,
                    new Color(.94f,.91f,1f),new Color(.61f,.63f,.73f));
                contact.Quad(a,b,b-inward*.26f,a-inward*.26f,new Color(.035f,.025f,.07f,.5f),new Color(.035f,.025f,.07f,0));
            }
        }
        void AddMesh(Chunk chunk, OrganicLabGeometry.Surface surface, string label, Material material, string layer, bool foreground = false)
        {
            if (surface.VertexCount == 0) return;
            var go = new GameObject(label); go.transform.SetParent(chunk.root.transform,false);
            var mesh = surface.CreateMesh(label); chunk.meshes.Add(mesh); VertexCount += mesh.vertexCount;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.sortingLayerName = layer;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            if (foreground) chunk.foreground.Add(r);
        }
        void AddRock(Chunk chunk, int x, int y, float lift, bool foreground, uint hash)
        {
            var sprite = _style.rock; var go = new GameObject("Supported rock shoulder");
            go.transform.SetParent(chunk.root.transform,false);
            float width = .65f + (hash % 100) * .005f, scale = width / sprite.bounds.size.x;
            go.transform.localScale = Vector3.one * scale;
            go.transform.localPosition = new Vector3(x+.5f-sprite.bounds.center.x*scale,y+lift+.3f-sprite.bounds.min.y*scale,0);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sharedMaterial = _props;
            sr.sortingLayerName = foreground ? VisualLayers.FrontStructure : VisualLayers.WallTop;
            sr.sortingOrder = 2; sr.color = new Color(.55f,.57f,.67f);
            if (foreground) chunk.foregroundProps.Add(sr);
        }
        void LateUpdate()
        {
            if (_chunks == null) return;
            foreach (var chunk in _chunks)
            {
                if (chunk?.occluder == null) continue;
                float a = chunk.occluder.Alpha;
                if (Mathf.Abs(a-chunk.alpha) < 1f/255f) continue;
                chunk.alpha = a; _block.SetColor("_Color",new Color(1,1,1,a));
                foreach (var r in chunk.foreground) r.SetPropertyBlock(_block);
                foreach (var r in chunk.foregroundProps) { var c=r.color; c.a=a; r.color=c; }
            }
        }
        void Release(Chunk chunk)
        {
            if (chunk == null) return;
            chunk.root.SetActive(false);
            foreach (var mesh in chunk.meshes) { VertexCount -= mesh.vertexCount; Destroy(mesh); }
            Destroy(chunk.root);
        }
        void OnDestroy()
        {
            if (_chunks != null) foreach (var chunk in _chunks)
                if (chunk != null) foreach (var mesh in chunk.meshes) if (mesh != null) Destroy(mesh);
            if (_cap != null) Destroy(_cap); if (_front != null) Destroy(_front);
            if (_contact != null) Destroy(_contact); if (_props != null) Destroy(_props);
        }
    }
}
