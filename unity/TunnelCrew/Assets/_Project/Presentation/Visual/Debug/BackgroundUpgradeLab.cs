using System.Collections.Generic;
using TunnelCrew.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace TunnelCrew.Presentation.Visual
{
    [DefaultExecutionOrder(-200)]
    public sealed class BackgroundUpgradeLab : MonoBehaviour
    {
        public EnvironmentKit kit;
        public EnvironmentKit floorKit;
        public WorldVisualProfile profile;
        public SurfaceRuleSet rules;
        public SurfaceMaterialSet floorSet, capSet, frontSet;
        public Shader rockShader;
        public Sprite rock, crystal, lamp, pipe, beam, rail, foreground, fog;
        public Material crystalMaterial, lampMaterial;
        public Camera labCamera;
        public int Mode {get; private set;} = 3;
        public LabEnvironment Environment {get; private set;}
        public LabPlayer Player {get; private set;}
        public double LastRebuildMs {get; private set;}
        public int RebuildCount {get; private set;}
        public int VertexCount {get; private set;}
        public int ContourCount {get; private set;}
        public const int Cols=26, Rows=20;
        public static readonly Vector2 Spawn=new(12.5f,8.5f);
        EnvironmentChunkRenderer _tiles;
        Transform _organic, _dressing, _atmosphere;
        Material _capMaterial,_frontMaterial,_propMaterial;
        readonly List<Object> _owned=new();
        readonly List<Mesh> _meshes=new();
        readonly List<MeshRenderer> _walls=new();
        readonly List<SpriteRenderer> _props=new();
        readonly List<(Transform t,Vector3 origin,float phase)> _fog=new();
        MaterialPropertyBlock _block;
        ProjectionPreset _previousProjection;
        int _lastEdits=-1;
        bool _macro=true,_fogOn=true,_overview,_grid;
        float _walkZoom=6.5f;
        bool _crtWasEnabled;
        GUIStyle _title,_text;

        void OnEnable()
        {
            // Script hot reload loses nonserialized simulation state while runtime objects survive.
            // Recreate only this lab's generated children instead of leaving orphaned LabPlayers.
            if(Application.isPlaying&&_block==null)
            {
                for(int i=transform.childCount-1;i>=0;i--)
                {var child=transform.GetChild(i).gameObject;child.SetActive(false);Destroy(child);}
                Awake();Start();
            }
        }

        public static string[] Room()
        {
            var room=new string[Rows];
            for(int y=0;y<Rows;y++)
            {
                var row=new char[Cols];
                for(int x=0;x<Cols;x++)
                {
                    // Central cave, crystal alcove, machinery bay; connected by generous corridors.
                    bool open=(x>=7&&x<=18&&y>=4&&y<=13)
                        ||(x>=3&&x<=9&&y>=9&&y<=16)
                        ||(x>=17&&x<=22&&y>=7&&y<=15)
                        ||(x>=10&&x<=15&&y>=13&&y<=16)
                        ||(x>=5&&x<=19&&y>=3&&y<=5);
                    if((x<=8&&y<=6)||(x>=17&&y<=5)||(x>=15&&y>=14)) open=false;
                    if(x>=13&&x<=14&&y>=10&&y<=11) open=false;
                    row[x]=open?'.':'#';
                }
                room[Rows-1-y]=new string(row);
            }
            return room;
        }

        GameObject Child(string name,Transform parent=null)
        {
            var go=new GameObject(name);go.transform.SetParent(parent!=null?parent:transform,false);return go;
        }
        void Awake()
        {
            _block=new MaterialPropertyBlock();
            _previousProjection=IsometricProjection.Preset;
            IsometricProjection.SetPreset(ProjectionPreset.ReferenceTopDown);
            var root=Child("Playable cave");root.SetActive(false);
            _tiles=Child("Baseline tile renderer",root.transform).AddComponent<EnvironmentChunkRenderer>();
            var labKit=Instantiate(kit);_owned.Add(labKit);
            if(floorKit!=null){labKit.floorBase=floorKit.floorBase;labKit.floorEdge=floorKit.floorEdge;labKit.contactAo=floorKit.contactAo;}
            _tiles.EditorAssign(profile,rules,labKit,floorSet,capSet,frontSet);
            var collision=Child("Gameplay collision",root.transform).AddComponent<LabWallCollision>();
            var shadows=Child("Grid light shadows",root.transform).AddComponent<ShadowGeometryBuilder>();
            Environment=_tiles.gameObject.AddComponent<LabEnvironment>();
            Environment.EditorAssign(Room(),_tiles,shadows,null,collision);
            Environment.LosEnabled=false;
            Player=Child("Driller",root.transform).AddComponent<LabPlayer>();Player.EditorAssign(Spawn);
            root.SetActive(true);
            _organic=Child("Organic wall surfaces").transform;
            _dressing=Child("Multi-cell room dressing").transform;
            _atmosphere=Child("Foreground and low fog").transform;
            _capMaterial=RockMaterial(capSet,kit.wallTop[0],"Organic cap");
            _frontMaterial=RockMaterial(frontSet,kit.wallFront[0],"Organic front");
            _propMaterial=new Material(Shader.Find(SurfaceMaterialSet.WorldShaderName));
            _propMaterial.SetTexture("_MaskTex",Texture2D.blackTexture);_propMaterial.SetFloat("_NormalStrength",0);
            _propMaterial.SetFloat("_MinLight",.18f);_propMaterial.SetFloat("_ImpactWobble",0);_owned.Add(_propMaterial);
            var ambient=Child("Ambient 2D").AddComponent<Light2D>();
            ambient.lightType=Light2D.LightType.Global;ambient.color=new Color(.65f,.66f,.86f);ambient.intensity=.42f;
            ambient.targetSortingLayers=VisualLayers.LitLayerIds();
            PointLight(new Vector2(6,13),new Color(.15f,.65f,1f),1.7f,5.5f);
            PointLight(new Vector2(20,12),new Color(1f,.56f,.2f),1.5f,5.5f);
            PointLight(new Vector2(11,5),new Color(.65f,.3f,1f),.8f,4f);
        }
        Material RockMaterial(SurfaceMaterialSet set,Sprite sprite,string name)
        {
            var m=new Material(rockShader){name=name};set.Apply(m,profile);
            m.SetTexture("_MainTex",sprite.texture);
            var r=sprite.textureRect;float w=sprite.texture.width,h=sprite.texture.height;
            // Sample the rock interior; painted tile borders must not return as a checkerboard.
            m.SetVector("_LabUVRect",new Vector4((r.x+r.width*.18f)/w,(r.y+r.height*.18f)/h,r.width*.64f/w,r.height*.64f/h));
            m.SetFloat("_MinLight",.2f);m.SetFloat("_ImpactWobble",0);_owned.Add(m);return m;
        }
        void PointLight(Vector2 p,Color color,float intensity,float radius)
        {
            var l=Child("Room light").AddComponent<Light2D>();l.transform.position=p;
            l.lightType=Light2D.LightType.Point;l.color=color;l.intensity=intensity;
            l.pointLightOuterRadius=radius;l.pointLightInnerRadius=.3f;
            l.targetSortingLayers=VisualLayers.LitLayerIds();
        }
        void Start()
        {
            var crt=TunnelCrew.Presentation.CRT.CRTDisplayController.Instance;
            if(crt!=null){_crtWasEnabled=crt.DisplayEnabled;crt.SetEnabled(false);}
            Rebuild();BuildAtmosphere();SetMode(3);
#if UNITY_EDITOR
            if(UnityEditor.SessionState.GetBool("BackgroundLab.RunSmoke",false))
            {
                UnityEditor.SessionState.SetBool("BackgroundLab.RunSmoke",false);
                StartCoroutine(BackgroundLabSmoke.Run(this));
            }
#endif
        }
        public void SetMode(int mode)
        {
            Mode=Mathf.Clamp(mode,1,3);
            foreach(var r in _tiles.GetComponentsInChildren<TilemapRenderer>(true))
                if(r.name!="GroundBase"&&r.name!="GroundDetail")r.enabled=Mode!=3;
            _organic.gameObject.SetActive(Mode==3);_dressing.gameObject.SetActive(Mode>=2);
            _atmosphere.gameObject.SetActive(Mode>=2&&_fogOn);
        }
        void Clear(Transform root)
        {
            for(int i=root.childCount-1;i>=0;i--){root.GetChild(i).gameObject.SetActive(false);Destroy(root.GetChild(i).gameObject);}
        }
        void Rebuild()
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            Clear(_organic);Clear(_dressing);foreach(var m in _meshes)Destroy(m);_meshes.Clear();_walls.Clear();_props.Clear();
            VertexCount=0;
            var field=Environment.Field;var geometry=new OrganicLabGeometry(field);ContourCount=geometry.Loops.Count;
            float lift=profile.wallLiftCells;
            // 4x1 render chunks permit local foreground fading without fading an entire cave.
            for(int y=0;y<Rows;y++) for(int bx=0;bx<Cols;bx+=4)
            {
                var surface=new OrganicLabGeometry.Surface();
                for(int x=bx;x<Mathf.Min(Cols,bx+4);x++)if(field.IsSolid(x,y))
                for(int sy=0;sy<4;sy++)for(int sx=0;sx<4;sx++)
                {
                    float a=x+sx*.25f,b=y+sy*.25f;
                    var up=Vector2.up*lift;
                    surface.Quad(geometry.Point(a,b)+up,geometry.Point(a+.25f,b)+up,
                        geometry.Point(a+.25f,b+.25f)+up,geometry.Point(a,b+.25f)+up,
                        new Color(.67f,.69f,.79f),new Color(.67f,.69f,.79f));
                }
                AddMesh(surface,"Cap "+bx+","+y,_capMaterial,Mathf.RoundToInt(-y*100)+1);
            }
            var fronts=new Dictionary<Vector2Int,OrganicLabGeometry.Surface>();
            var bevels=new Dictionary<Vector2Int,OrganicLabGeometry.Surface>();
            var contacts=new OrganicLabGeometry.Surface();
            foreach(var loop in geometry.Loops)
            for(int i=0;i<loop.Count;i++)
            {
                var a=loop[i];var b=loop[(i+1)%loop.Count];var tangent=(b-a).normalized;
                var inward=new Vector2(-tangent.y,tangent.x);var up=Vector2.up*lift;
                contacts.Quad(a,b,b-inward*.30f,a-inward*.30f,new Color(.04f,.05f,.09f,.58f),new Color(.04f,.05f,.09f,0));
                var chunk=new Vector2Int(Mathf.FloorToInt((a.x+b.x)*.125f),Mathf.RoundToInt((a.y+b.y)*.5f));
                if(b.x>a.x+.01f)
                {
                    if(!fronts.TryGetValue(chunk,out var front)){front=new OrganicLabGeometry.Surface();fronts.Add(chunk,front);}
                    front.Quad(a,b,b+up,a+up,new Color(.34f,.37f,.48f),new Color(.8f,.76f,.84f));
                }
                if(!bevels.TryGetValue(chunk,out var bevel)){bevel=new OrganicLabGeometry.Surface();bevels.Add(chunk,bevel);}
                bevel.Quad(a+up,b+up,b+up+inward*.10f,a+up+inward*.10f,
                    new Color(.9f,.88f,.98f),new Color(.57f,.59f,.69f));
                if(i%13==0 && a.x>1&&a.x<Cols-1&&a.y>1&&a.y<Rows-1)
                    AddProp(rock,a-inward*.12f,.35f+.2f*Mathf.Abs(Mathf.Sin(a.x*7+a.y)),"Boundary rubble",null);
            }
            foreach(var part in fronts)AddMesh(part.Value,"Front skirt "+part.Key,_frontMaterial,-part.Key.y*100);
            foreach(var part in bevels)AddMesh(part.Value,"Contour bevel "+part.Key,_capMaterial,-part.Key.y*100+2);
            AddMesh(contacts,"Contour contact shadow",_propMaterial,0,VisualLayers.GroundDecal);
            // Deliberate large shapes: separate crystal and machinery room grammars.
            AddProp(crystal,new Vector2(4.6f,14.8f),2.6f,"Crystal anchor",crystalMaterial);
            AddProp(crystal,new Vector2(7.8f,16.1f),1.6f,"Crystal cluster",crystalMaterial);
            AddProp(rock,new Vector2(2.8f,11.5f),2.2f,"Rock shoulder",null);
            AddProp(rock,new Vector2(9.6f,16.7f),1.9f,"Rock shoulder",null);
            AddProp(pipe,new Vector2(21.8f,13.6f),2.6f,"Pipe anchor",null);
            AddProp(beam,new Vector2(18.8f,15.5f),2.5f,"Support anchor",null);
            AddProp(lamp,new Vector2(20.4f,11.0f),1.3f,"Work lamp",lampMaterial);
            for(int y=8;y<=12;y+=2)AddProp(rail,new Vector2(19.8f,y),1.3f,"Rail route",null,true);
            _lastEdits=Environment.Edits;RebuildCount++;timer.Stop();LastRebuildMs=timer.Elapsed.TotalMilliseconds;
            SetMode(Mode);
        }
        void AddMesh(OrganicLabGeometry.Surface s,string name,Material material,int order,string layer=VisualLayers.WorldEntity)
        {
            if(s.VertexCount==0)return;
            var go=Child(name,_organic);var mesh=s.CreateMesh(name);_meshes.Add(mesh);VertexCount+=s.VertexCount;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;
            r.sortingLayerName=layer;r.sortingOrder=order;if(layer==VisualLayers.WorldEntity)_walls.Add(r);
        }
        SpriteRenderer AddProp(Sprite sprite,Vector2 p,float width,string name,Material mat,bool ground=false)
        {
            if(sprite==null)return null;
            // Don't leave a dressing anchor behind after its supporting wall is mined.
            if(name.Contains("shoulder")&&!Environment.Field.IsSolid(Mathf.FloorToInt(p.x),Mathf.FloorToInt(p.y)))return null;
            var go=Child(name,_dressing);var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite;
            float scale=width/Mathf.Max(.01f,sprite.bounds.size.x);go.transform.localScale=Vector3.one*scale;
            go.transform.position=new Vector3(p.x-sprite.bounds.center.x*scale,p.y-sprite.bounds.min.y*scale,0);
            sr.sortingLayerName=ground?VisualLayers.GroundDetail:VisualLayers.WorldEntity;
            sr.sortingOrder=ground?4:Mathf.RoundToInt(-p.y*100);
            sr.sharedMaterial=mat!=null?mat:_propMaterial;
            _props.Add(sr);return sr;
        }
        void BuildAtmosphere()
        {
            if(fog!=null)for(int i=0;i<4;i++)
            {
                var go=Child("Low fog card",_atmosphere);var sr=go.AddComponent<SpriteRenderer>();sr.sprite=fog;
                sr.sortingLayerName=VisualLayers.WorldFX;sr.color=new Color(.42f,.52f,.75f,.09f);
                go.transform.localScale=Vector3.one*(7f/fog.bounds.size.x);
                var p=new Vector3(7+i*4,5+(i%2)*7,0);go.transform.position=p;_fog.Add((go.transform,p,i*1.7f));
            }
            if(foreground!=null)
            {
                var go=Child("Near rock overhang",_atmosphere);var sr=go.AddComponent<SpriteRenderer>();sr.sprite=foreground;
                sr.sortingLayerName=VisualLayers.FrontStructure;sr.color=new Color(.16f,.18f,.27f,.75f);
                go.transform.position=new Vector3(7.5f,3.2f,0);go.transform.localScale=Vector3.one*(4.4f/foreground.bounds.size.x);
            }
        }
        void Update()
        {
            if(Environment==null||Player==null)return;
            var k=Keyboard.current;
            if(k!=null)
            {
                if(k.digit1Key.wasPressedThisFrame)SetMode(1);
                if(k.digit2Key.wasPressedThisFrame)SetMode(2);
                if(k.digit3Key.wasPressedThisFrame)SetMode(3);
                if(k.mKey.wasPressedThisFrame){_macro=!_macro;_capMaterial.SetFloat("_LabMacro",_macro?1:0);_frontMaterial.SetFloat("_LabMacro",_macro?1:0);}
                if(k.gKey.wasPressedThisFrame)_grid=!_grid;
                if(k.vKey.wasPressedThisFrame){_fogOn=!_fogOn;SetMode(Mode);}
                if(k.tabKey.wasPressedThisFrame){_overview=!_overview;if(!_overview)labCamera.orthographicSize=_walkZoom;}
                if(k.rKey.wasPressedThisFrame)ResetPlayer();
                if(k.minusKey.wasPressedThisFrame){_walkZoom=Mathf.Min(12,_walkZoom+1);labCamera.orthographicSize=_walkZoom;}
                if(k.equalsKey.wasPressedThisFrame){_walkZoom=Mathf.Max(3,_walkZoom-1);labCamera.orthographicSize=_walkZoom;}
            }
            if(Environment.Edits!=_lastEdits)Rebuild();
        }
        public void ResetPlayer()
        {
            var body=Player.FollowTarget.GetComponent<Rigidbody2D>();body.position=Spawn;body.linearVelocity=Vector2.zero;
        }
        void LateUpdate()
        {
            if(Player==null||Player.State==null)return;
            var p=Player.CellPosition;
            foreach(var sr in Player.GetComponentsInChildren<SpriteRenderer>())sr.sortingOrder=Mathf.RoundToInt(-p.y*100)+5;
            var aim=_overview?new Vector2(13,10):p+Vector2.up*.65f;
            if(_overview)labCamera.orthographicSize=10.8f;
            labCamera.transform.position=Vector3.Lerp(labCamera.transform.position,new Vector3(aim.x,aim.y,-20),1-Mathf.Exp(-7*Time.deltaTime));
            foreach(var wall in _walls)
            {
                var b=wall.bounds;
                bool hides=b.Contains(new Vector3(p.x,p.y+.4f,b.center.z))&&wall.sortingOrder>Mathf.RoundToInt(-p.y*100);
                _block.SetColor("_Color",new Color(1,1,1,hides?.3f:1));wall.SetPropertyBlock(_block);
            }
            foreach(var sr in _props)
            {
                if(sr==null||sr.sortingLayerName==VisualLayers.GroundDetail)continue;
                bool hides=sr.bounds.Contains(new Vector3(p.x,p.y+.4f,0))&&sr.sortingOrder>Mathf.RoundToInt(-p.y*100);
                sr.color=new Color(1,1,1,hides?.35f:1);
            }
            foreach(var f in _fog)f.t.position=f.origin+new Vector3(Mathf.Sin(Time.time*.17f+f.phase)*.6f,Mathf.Cos(Time.time*.11f+f.phase)*.15f,0);
        }
        void OnGUI()
        {
            _title??=new GUIStyle(GUI.skin.label){fontSize=20,fontStyle=FontStyle.Bold,normal={textColor=new Color(.4f,.9f,1)}};
            _text??=new GUIStyle(GUI.skin.label){fontSize=14,normal={textColor=Color.white}};
            GUI.Box(new Rect(14,14,570,112),GUIContent.none);
            GUI.Label(new Rect(28,22,550,28),"BACKGROUND LAB   /   "+(Mode==1?"A · TILE BASELINE":Mode==2?"B · LARGE SHAPES":"C · ORGANIC CONTOUR"),_title);
            GUI.Label(new Rect(28,53,550,24),"WASD / arrows: walk   |   Mouse: aim   |   F: flashlight   |   R: respawn",_text);
            GUI.Label(new Rect(28,75,550,24),"1 / 2 / 3: compare   |   M: material   |   V: fog   |   G: grid   |   Tab: overview",_text);
            GUI.Label(new Rect(28,97,550,24),"LMB x3: mine nearby wall   |   RMB: restore   |   - / +: zoom",_text);
            GUI.Label(new Rect(24,Screen.height-32,850,25),$"26 x 20 cells  |  contours {ContourCount}  |  vertices {VertexCount:N0}  |  rebuild {LastRebuildMs:F1} ms  |  edits {Environment?.Edits}",_text);
            if(_grid&&labCamera!=null)
            {
                var previous=GUI.color;GUI.color=new Color(.2f,.85f,1,.3f);
                for(int x=0;x<=Cols;x++)DrawGridLine(new Vector2(x,0),new Vector2(x,Rows));
                for(int y=0;y<=Rows;y++)DrawGridLine(new Vector2(0,y),new Vector2(Cols,y));
                GUI.color=previous;
            }
        }
        void DrawGridLine(Vector2 a,Vector2 b)
        {
            var p=labCamera.WorldToScreenPoint(a);var q=labCamera.WorldToScreenPoint(b);
            GUI.DrawTexture(new Rect(Mathf.Min(p.x,q.x),Screen.height-Mathf.Max(p.y,q.y),Mathf.Max(1,Mathf.Abs(q.x-p.x)),Mathf.Max(1,Mathf.Abs(q.y-p.y))),Texture2D.whiteTexture);
        }
        void OnDestroy()
        {
            var crt=TunnelCrew.Presentation.CRT.CRTDisplayController.Instance;
            if(crt!=null)crt.SetEnabled(_crtWasEnabled);
            foreach(var o in _owned)if(o!=null)Destroy(o);
            foreach(var m in _meshes)if(m!=null)Destroy(m);
            IsometricProjection.SetPreset(_previousProjection);
        }
    }
}
