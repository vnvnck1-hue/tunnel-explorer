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
        Material _floor, _cap, _front, _rim, _contact, _props, _supportProps;
        Chunk[] _chunks;
        ForegroundOccluder[] _occluders;
        MaterialPropertyBlock _block;
        public int RebuildCount { get; private set; }
        public double LastChunkMs { get; private set; }
        public int VertexCount { get; private set; }
        public int ChunkCount => _chunks?.Length ?? 0;

        public bool Initialize(EnvironmentChunkRenderer owner, ISolidField field,
            SurfaceMaterialSet floorSet, SurfaceMaterialSet capSet, SurfaceMaterialSet frontSet,
            ForegroundOccluder[] occluders)
        {
            _style = Resources.Load<OrganicEnvironmentStyle>("Visual/OrganicEnvironmentStyle");
            if (_style == null || _style.rockShader == null || ! _style.rockShader.isSupported) return false;
            var capSprite = EnvironmentKit.Pick(owner.Kit.wallTop, 0);
            var frontSprite = EnvironmentKit.Pick(owner.Kit.wallFront, 0);
            if (capSprite == null || frontSprite == null) return false;
            _owner = owner; _field = field; _occluders = occluders;
            var floorSprite = EnvironmentKit.Pick(owner.Kit.floorBase, 0);
            _floor = RockMaterial(floorSet, _style.floorMacro, _style.floorNormal,
                _style.floorAo, _style.floorEmission, floorSprite, "Organic floor",
                _style.floorMacroSizeCells, _style.floorMinLight, _style.floorAccentEmission,
                _style.floorDerivedNormalStrength, _style.floorAoStrength, _style.floorMirrorMacro);
            _cap = RockMaterial(capSet, _style.wallTopMacro, _style.wallTopNormal,
                _style.wallTopAo, _style.wallTopEmission, capSprite, "Organic cap",
                _style.wallTopMacroSizeCells, _style.wallTopMinLight, _style.wallAccentEmission,
                _style.wallTopDerivedNormalStrength, _style.wallAoStrength, _style.mirrorMacro);
            _front = RockMaterial(frontSet, _style.wallFrontMacro, _style.wallFrontNormal,
                _style.wallFrontAo, _style.wallFrontEmission, frontSprite, "Organic front",
                _style.wallFrontMacroSizeCells, _style.wallFrontMinLight, _style.wallAccentEmission,
                _style.wallFrontDerivedNormalStrength, _style.wallAoStrength, _style.mirrorMacro);
            _rim = RockMaterial(capSet, _style.wallRimMacro, _style.wallRimNormal,
                _style.wallRimAo, _style.wallRimEmission, capSprite, "Organic rim",
                _style.wallRimMacroSizeCells, _style.wallRimMinLight, _style.wallAccentEmission,
                _style.wallRimDerivedNormalStrength, _style.wallAoStrength, _style.mirrorMacro);
            _contact = OverlayMaterials.Unlit("Organic contact AO");
            _props = PropMaterial(_style.rock != null ? _style.rock.texture : null, "Organic wall dressing");
            var supportShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            if (supportShader == null) supportShader = Shader.Find("Sprites/Default");
            _supportProps = supportShader != null ? new Material(supportShader) { name = "Integrated wall supports" } : null;
            _block = new MaterialPropertyBlock();
            _chunks = new Chunk[SurfaceTopologyBuilder.ChunkCols(field.Cols) * SurfaceTopologyBuilder.ChunkRows(field.Rows)];
            for (int i = 0; i < _chunks.Length; i++) Rebuild(i);
            return true;
        }

        Material PropMaterial(Texture2D albedo, string label)
        {
            var m = new Material(Shader.Find(SurfaceMaterialSet.WorldShaderName)) { name = label };
            if (albedo != null) m.SetTexture("_MainTex", albedo);
            m.SetTexture("_MaskTex", Texture2D.blackTexture);
            m.SetTexture("_EmissionMap", Texture2D.blackTexture);
            m.SetTexture("_AOMap", Texture2D.whiteTexture);
            m.SetFloat("_NormalStrength", 0);
            m.SetFloat("_EmissionIntensity", 0);
            m.SetFloat("_AOStrength", 0);
            m.SetFloat("_ImpactWobble", 0);
            m.SetFloat("_MinLight", _owner.Profile != null ? _owner.Profile.minLightBySurface.x : .03f);
            return m;
        }

        Material RockMaterial(SurfaceMaterialSet set, Texture2D macro, Texture2D normal,
            Texture2D ao, Texture2D emission, Sprite fallback, string label,
            float macroCells, float minLight, float accentEmission, float normalStrength,
            float aoStrength, bool mirrorMacro)
        {
            var m = new Material(_style.rockShader) { name = label };
            if (set != null) set.Apply(m, _owner.Profile);
            else { m.SetTexture("_MaskTex", Texture2D.blackTexture); m.SetFloat("_NormalStrength", 0); }
            if (macro != null)
            {
                m.SetTexture("_MainTex", macro);
                // The first primary-match delivery is albedo-only. Reusing the legacy tile's
                // channel atlas would project unrelated normals/emission across the new painting.
                m.SetTexture("_MaskTex", Texture2D.blackTexture);
                if (normal != null) m.SetTexture("_NormalMap", normal);
                m.SetTexture("_EmissionMap", emission != null ? emission : Texture2D.blackTexture);
                m.SetTexture("_AOMap", ao != null ? ao : Texture2D.whiteTexture);
                m.SetFloat("_NormalStrength", Mathf.Max(0f, normalStrength));
                m.SetFloat("_LabDerivedNormal", normal == null && normalStrength > 0f ? 1f : 0f);
                m.SetFloat("_EmissionIntensity", emission != null ? accentEmission : 0f);
                m.SetFloat("_AOStrength", ao != null ? Mathf.Clamp01(aoStrength) : 0f);
                m.SetVector("_LabUVRect", new Vector4(0f, 0f, 1f, 1f));
                float cells = Mathf.Max(1f, macroCells > 0f ? macroCells : _style.macroSizeCells);
                m.SetVector("_LabScale", new Vector4(1f / cells, 1f / cells, 0f, 0f));
                m.SetFloat("_LabMirrorRepeat", mirrorMacro ? 1f : 0f);
                m.SetFloat("_MinLight", minLight);
                m.SetFloat("_LabAccentEmission", emission == null ? accentEmission : 0f);
            }
            else if (fallback != null)
            {
                m.SetTexture("_MainTex", fallback.texture);
                var r = fallback.textureRect;
                m.SetVector("_LabUVRect", new Vector4((r.x + r.width * .18f) / fallback.texture.width,
                    (r.y + r.height * .18f) / fallback.texture.height,
                    r.width * .64f / fallback.texture.width, r.height * .64f / fallback.texture.height));
            }
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
            var floor = new OrganicLabGeometry.Surface();
            var cap = new OrganicLabGeometry.Surface(); var fg = new OrganicLabGeometry.Surface();
            var front = new OrganicLabGeometry.Surface(); var rim = new OrganicLabGeometry.Surface();
            var contact = new OrganicLabGeometry.Surface();
            float lift = _owner.Profile != null ? _owner.Profile.wallLiftCells : 1f;
            var up = Vector2.up * lift;
            var capColor = new Color(.76f, .77f, .83f);
            for (int y = y0; y < Mathf.Min(y0 + side, _field.Rows); y++)
            for (int x = x0; x < Mathf.Min(x0 + side, _field.Cols); x++)
            {
                var cell = _owner.SurfaceAt(x, y);
                if (cell.IsFloor && _floor != null)
                    floor.Quad(new Vector2(x,y), new Vector2(x+1,y), new Vector2(x+1,y+1), new Vector2(x,y+1),
                        new Color(.92f,.90f,.96f), new Color(.92f,.90f,.96f));
                if ((cell.Surfaces & SurfaceMask.WallTop) == 0 || (cell.Surfaces & SurfaceMask.BossWall) != 0) continue;
                bool foreground = (cell.Surfaces & SurfaceMask.ForegroundTop) != 0;
                var top = foreground ? fg : cap;
                for (int sy = 0; sy < 4; sy++) for (int sx = 0; sx < 4; sx++)
                {
                    float a = x + sx * .25f, b = y + sy * .25f;
                    top.Quad(P(a,b)+up, P(a+.25f,b)+up, P(a+.25f,b+.25f)+up, P(a,b+.25f)+up, capColor, capColor);
                }
                if (!_field.IsSolid(x,y-1)) Edge(new Vector2(x,y), Vector2.right, top, front, rim, contact, up, true);
                if (!_field.IsSolid(x+1,y)) Edge(new Vector2(x+1,y), Vector2.up, top, front, rim, contact, up, false);
                if (!_field.IsSolid(x,y+1)) Edge(new Vector2(x+1,y+1), Vector2.left, top, front, rim, contact, up, false);
                if (!_field.IsSolid(x-1,y)) Edge(new Vector2(x,y+1), Vector2.down, top, front, rim, contact, up, false);
                // 장식 바위는 남향 정면에만 붙인다. 모든 경계 cap 에 놓으면 동/서/북 모서리에서
                // 둥근 독립 장애물처럼 떠 보이고, 벽 구조가 끊긴 것으로 오해된다.
                uint hash = SurfaceTopologyBuilder.Hash(x,y,0x0A61);
                if (_style.rock != null && !_field.IsSolid(x, y - 1) && hash % 17 == 0)
                    AddRock(chunk, x, y, lift, foreground, hash);
                if (ShouldAddWallSupport(x, y, out int runStart, out int runLength))
                    AddWallSupport(chunk, x, y, lift, foreground, runStart, runLength);
                if (ShouldAddWallConduit(x, y, out runStart, out runLength))
                    AddWallConduit(chunk, x, y, lift, foreground, runStart, runLength);
                if (ShouldAddWallJunction(x, y, out runStart, out runLength,
                    out bool leftTurn, out int sideRunLength))
                    AddWallJunction(chunk, x, y, lift, foreground, runStart, runLength,
                        leftTurn, sideRunLength);
            }
            AddMesh(chunk, floor, "Floor macro", _floor, VisualLayers.GroundBase, order: 1);
            AddMesh(chunk, cap, "Cap", _cap, VisualLayers.WallTop);
            AddMesh(chunk, fg, "Foreground cap", _cap, VisualLayers.FrontStructure, true);
            AddMesh(chunk, front, "Front skirt", _front, VisualLayers.BackStructure);
            AddMesh(chunk, rim, "Wall rim", _rim, VisualLayers.WallTop, order: 1);
            AddMesh(chunk, contact, "Contact shadow", _contact, VisualLayers.GroundDecal);
            RebuildCount++; timer.Stop(); LastChunkMs = timer.Elapsed.TotalMilliseconds;
        }

        Vector2 P(float x, float y) => OrganicWallSampling.Point(_field,x,y);
        void Edge(Vector2 start, Vector2 direction, OrganicLabGeometry.Surface top,
            OrganicLabGeometry.Surface front, OrganicLabGeometry.Surface rim,
            OrganicLabGeometry.Surface contact, Vector2 up, bool south)
        {
            for (int i = 0; i < 4; i++)
            {
                var p = start + direction * (i * .25f); var q = p + direction * .25f;
                var a = P(p.x,p.y); var b = P(q.x,q.y);
                var tangent = (b-a).normalized; var inward = new Vector2(-tangent.y,tangent.x);
                if (south) front.Quad(a,b,b+up,a+up,new Color(.40f,.42f,.50f),new Color(.88f,.84f,.90f));
                rim.Quad(a+up,b+up,b+up+inward*.09f,a+up+inward*.09f,
                    new Color(.94f,.91f,1f),new Color(.61f,.63f,.73f));
                contact.Quad(a,b,b-inward*.26f,a-inward*.26f,new Color(.035f,.025f,.07f,.5f),new Color(.035f,.025f,.07f,0));
            }
        }
        void AddMesh(Chunk chunk, OrganicLabGeometry.Surface surface, string label, Material material,
            string layer, bool foreground = false, int order = 0)
        {
            if (surface.VertexCount == 0 || material == null) return;
            var go = new GameObject(label); go.transform.SetParent(chunk.root.transform,false);
            var mesh = surface.CreateMesh(label); chunk.meshes.Add(mesh); VertexCount += mesh.vertexCount;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.sortingLayerName = layer;
            r.sortingOrder = order;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            if (foreground) chunk.foreground.Add(r);
        }
        void AddRock(Chunk chunk, int x, int y, float lift, bool foreground, uint hash)
        {
            var sprite = _style.rock; var go = new GameObject("Supported rock shoulder");
            go.transform.SetParent(chunk.root.transform,false);
            float width = .65f + (hash % 100) * .005f, scale = width / sprite.bounds.size.x;
            go.transform.localScale = Vector3.one * scale;
            // 바닥을 cap 위로 띄우지 않고 정면 경계 안에 조금 묻어 "붙은 어깨"로 읽히게 한다.
            go.transform.localPosition = new Vector3(x+.5f-sprite.bounds.center.x*scale,
                y+lift-.16f-sprite.bounds.min.y*scale,0);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sharedMaterial = _props;
            sr.sortingLayerName = foreground ? VisualLayers.FrontStructure : VisualLayers.WallTop;
            sr.sortingOrder = 2; sr.color = new Color(.55f,.57f,.67f);
            if (foreground) chunk.foregroundProps.Add(sr);
        }

        bool ShouldAddWallSupport(int x, int y, out int runStart, out int runLength)
        {
            runStart = x;
            runLength = 0;
            if (_style.wallSupports == null || _style.wallSupports.Length == 0 ||
                !_field.IsSolid(x, y) || _field.IsSolid(x, y - 1)) return false;

            while (runStart > 0 && _field.IsSolid(runStart - 1, y) &&
                   !_field.IsSolid(runStart - 1, y - 1)) runStart--;
            int runEnd = x;
            while (runEnd + 1 < _field.Cols && _field.IsSolid(runEnd + 1, y) &&
                   !_field.IsSolid(runEnd + 1, y - 1)) runEnd++;
            runLength = runEnd - runStart + 1;

            // A support needs a real facade span on both sides. Short one-off wall teeth keep
            // their geological silhouette instead of receiving a conspicuous prop.
            if (runLength < 5) return false;
            return IsWallSupportSlot(x - runStart, runLength);
        }

        public static bool IsWallSupportSlot(int local, int runLength)
        {
            if (runLength < 5 || local < 0 || local >= runLength) return false;
            return local == 1 || local == runLength - 2 ||
                   (local > 1 && local < runLength - 2 && (local - 1) % 5 == 0);
        }

        bool ShouldAddWallConduit(int x, int y, out int runStart, out int runLength)
        {
            runStart = x;
            runLength = 0;
            if (_style.wallConduits == null || _style.wallConduits.Length < 9 ||
                !_field.IsSolid(x, y) || _field.IsSolid(x, y - 1)) return false;

            while (runStart > 0 && _field.IsSolid(runStart - 1, y) &&
                   !_field.IsSolid(runStart - 1, y - 1)) runStart--;
            int runEnd = x;
            while (runEnd + 1 < _field.Cols && _field.IsSolid(runEnd + 1, y) &&
                   !_field.IsSolid(runEnd + 1, y - 1)) runEnd++;
            runLength = runEnd - runStart + 1;
            return ConduitPieceFor(x - runStart, runLength) >= 0;
        }

        public static int ConduitPieceFor(int local, int runLength)
        {
            if (runLength < 5 || local < 1 || local > runLength - 2) return -1;
            if (local == 1) return 0;
            return local == runLength - 2 ? 2 : 1;
        }

        public static int ConduitRowFor(int runStart, int y)
        {
            return (int)(SurfaceTopologyBuilder.Hash(runStart, y, 0xC04D) % 3u);
        }

        bool ShouldAddWallJunction(int x, int y, out int runStart, out int runLength,
            out bool leftTurn, out int sideRunLength)
        {
            runStart = x;
            runLength = 0;
            leftTurn = false;
            sideRunLength = 0;
            if (_style.wallJunctions == null || _style.wallJunctions.Length < 9 ||
                !_field.IsSolid(x, y) || _field.IsSolid(x, y - 1)) return false;

            while (runStart > 0 && _field.IsSolid(runStart - 1, y) &&
                   !_field.IsSolid(runStart - 1, y - 1)) runStart--;
            int runEnd = x;
            while (runEnd + 1 < _field.Cols && _field.IsSolid(runEnd + 1, y) &&
                   !_field.IsSolid(runEnd + 1, y - 1)) runEnd++;
            runLength = runEnd - runStart + 1;
            // Generated cave rooms naturally produce five-to-seven-cell service facades.
            // Match the horizontal conduit contract instead of waiting for an artificial
            // nine-cell corridor that the current map topology almost never creates.
            if (runLength < 5) return false;

            int local = x - runStart;
            if (local == 0)
            {
                leftTurn = true;
                sideRunLength = CountSideAscent(runStart, y, true, runLength);
            }
            else if (local == runLength - 1)
            {
                sideRunLength = CountSideAscent(runStart, y, false, runLength);
            }
            else return false;

            return JunctionPieceFor(local, runLength,
                leftTurn ? sideRunLength : 0, leftTurn ? 0 : sideRunLength) >= 0;
        }

        int CountSideAscent(int runStart, int y, bool left, int runLength)
        {
            int rockX = left ? runStart : runStart + runLength - 1;
            int openX = left ? rockX - 1 : rockX + 1;
            if (openX < 0 || openX >= _field.Cols) return 0;
            int count = 0;
            for (int yy = y + 1; yy < _field.Rows; yy++)
            {
                if (!_field.IsSolid(rockX, yy) || _field.IsSolid(openX, yy)) break;
                count++;
            }
            return count;
        }

        public static int JunctionPieceFor(int local, int runLength,
            int leftSideCells, int rightSideCells)
        {
            if (runLength < 5 || local < 0 || local >= runLength) return -1;
            if (local == 0 && leftSideCells >= 2) return 0;
            if (local == runLength - 1 && rightSideCells >= 2) return 2;
            return -1;
        }

        public static int VerticalJunctionSegmentCount(int sideRunLength)
            => sideRunLength < 2 ? 0 : Mathf.Min(4, sideRunLength);

        void AddWallSupport(Chunk chunk, int x, int y, float lift, bool foreground,
            int runStart, int runLength)
        {
            uint hash = SurfaceTopologyBuilder.Hash(runStart, y, 0x51A7) + (uint)(x - runStart);
            var sprite = _style.wallSupports[hash % (uint)_style.wallSupports.Length];
            if (sprite == null) return;
            var go = new GameObject($"Integrated wall support {x},{y}");
            go.transform.SetParent(chunk.root.transform, false);
            float targetHeight = lift + .75f;
            float scale = targetHeight / Mathf.Max(.01f, sprite.bounds.size.y);
            var basePoint = P(x + .5f, y);
            go.transform.localPosition = new Vector3(basePoint.x, basePoint.y, 0f);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.sharedMaterial = _supportProps != null ? _supportProps : _props;
            sr.sortingLayerName = VisualLayers.WorldEntity;
            int units = _owner.Profile != null ? _owner.Profile.depthUnitsPerCell : DepthSort.DefaultUnitsPerCell;
            sr.sortingOrder = DepthSort.OrderFor(y, units) + 1;
            sr.color = Color.white;
            if (foreground) chunk.foregroundProps.Add(sr);
        }

        void AddWallConduit(Chunk chunk, int x, int y, float lift, bool foreground,
            int runStart, int runLength)
        {
            int piece = ConduitPieceFor(x - runStart, runLength);
            if (piece < 0) return;
            int row = ConduitRowFor(runStart, y);
            int spriteIndex = row * 3 + piece;
            if (spriteIndex >= _style.wallConduits.Length) return;
            var sprite = _style.wallConduits[spriteIndex];
            if (sprite == null) return;

            var go = new GameObject($"Integrated wall conduit {x},{y}");
            go.transform.SetParent(chunk.root.transform, false);
            // A whole run shares one rigid datum. The geological rim may undulate underneath,
            // but the installed service line remains visibly continuous from socket to socket.
            float runDatum = P(runStart + .5f, y).y;
            go.transform.localPosition = new Vector3(x + .5f, runDatum + lift * .62f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.sharedMaterial = _supportProps != null ? _supportProps : _props;
            sr.sortingLayerName = VisualLayers.WorldEntity;
            int units = _owner.Profile != null ? _owner.Profile.depthUnitsPerCell : DepthSort.DefaultUnitsPerCell;
            sr.sortingOrder = DepthSort.OrderFor(y, units);
            sr.color = Color.white;
            if (foreground) chunk.foregroundProps.Add(sr);
        }

        void AddWallJunction(Chunk chunk, int x, int y, float lift, bool foreground,
            int runStart, int runLength, bool leftTurn, int sideRunLength)
        {
            int row = ConduitRowFor(runStart, y);
            int turnIndex = row * 3 + (leftTurn ? 0 : 2);
            if (turnIndex >= _style.wallJunctions.Length) return;
            var turnSprite = _style.wallJunctions[turnIndex];
            if (turnSprite == null) return;

            float runDatum = P(runStart + .5f, y).y;
            float junctionY = runDatum + lift * .62f;
            int units = _owner.Profile != null ? _owner.Profile.depthUnitsPerCell : DepthSort.DefaultUnitsPerCell;

            var turn = new GameObject($"Integrated wall junction {(leftTurn ? "left" : "right")} {x},{y}");
            turn.transform.SetParent(chunk.root.transform, false);
            turn.transform.localPosition = new Vector3(x + .5f, junctionY, 0f);
            var turnRenderer = turn.AddComponent<SpriteRenderer>();
            turnRenderer.sprite = turnSprite;
            turnRenderer.sharedMaterial = _supportProps != null ? _supportProps : _props;
            turnRenderer.sortingLayerName = VisualLayers.WorldEntity;
            turnRenderer.sortingOrder = DepthSort.OrderFor(y, units) + 1;
            turnRenderer.color = Color.white;
            if (foreground) chunk.foregroundProps.Add(turnRenderer);

            int repeatIndex = row * 3 + 1;
            if (repeatIndex >= _style.wallJunctions.Length) return;
            var repeatSprite = _style.wallJunctions[repeatIndex];
            if (repeatSprite == null) return;

            int segments = VerticalJunctionSegmentCount(sideRunLength);
            for (int i = 0; i < segments; i++)
            {
                var repeat = new GameObject($"Integrated side-wall conduit {x},{y + 1 + i}");
                repeat.transform.SetParent(chunk.root.transform, false);
                // The generated repeat retains a small transparent authoring margin. A 1.15 Y scale
                // closes that margin while the one-cell centres and the painted cross-section stay fixed.
                repeat.transform.localPosition = new Vector3(x + .5f, junctionY + .94f + i, 0f);
                repeat.transform.localScale = new Vector3(1f, 1.15f, 1f);
                var repeatRenderer = repeat.AddComponent<SpriteRenderer>();
                repeatRenderer.sprite = repeatSprite;
                repeatRenderer.sharedMaterial = _supportProps != null ? _supportProps : _props;
                repeatRenderer.sortingLayerName = VisualLayers.WorldEntity;
                repeatRenderer.sortingOrder = DepthSort.OrderFor(y + 1 + i, units);
                repeatRenderer.color = Color.white;
                if (foreground) chunk.foregroundProps.Add(repeatRenderer);
            }
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
            if (_floor != null) Destroy(_floor); if (_cap != null) Destroy(_cap); if (_front != null) Destroy(_front);
            if (_rim != null) Destroy(_rim);
            if (_contact != null) Destroy(_contact); if (_props != null) Destroy(_props);
            if (_supportProps != null) Destroy(_supportProps);
        }
    }
}
