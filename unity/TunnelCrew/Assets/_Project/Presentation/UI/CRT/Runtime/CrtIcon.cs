using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace TunnelCrew.Presentation.CRT
{
    public enum CrtGlyph { Heart, Ammo, Heat, Drill, Shield, Detonate, Flare, Grapple, Power, Turret, Dash, Crew, Boss, Depth, Warning, Rescue, Signal, Core, Lock, Eye, Reload, Xp }

    /// <summary>Original vector pictograms. Sharp alpha silhouettes, no baked scanlines or curved artwork.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CrtIcon : MaskableGraphic
    {
        CrtGlyph _glyph;
        VertexHelper _vh;
        static readonly Dictionary<CrtGlyph,Vector2[]> MeshCache=new Dictionary<CrtGlyph,Vector2[]>();
        public void Set(CrtGlyph glyph,Color tint) { if(_glyph!=glyph){_glyph=glyph;SetVerticesDirty();} color=tint; }
        Vector2 P(float x,float y) { var r=rectTransform.rect; return new Vector2(r.xMin+x*r.width,r.yMin+y*r.height); }
        void Line(float ax,float ay,float bx,float by,float width=.055f)
        {
            var a=P(ax,ay); var b=P(bx,by); var n=new Vector2(b.y-a.y,a.x-b.x).normalized*rectTransform.rect.width*width*.5f;
            int k=_vh.currentVertCount; _vh.AddVert(a-n,color,Vector2.zero);_vh.AddVert(b-n,color,Vector2.zero);
            _vh.AddVert(b+n,color,Vector2.zero);_vh.AddVert(a+n,color,Vector2.zero);
            _vh.AddTriangle(k,k+1,k+2);_vh.AddTriangle(k,k+2,k+3);
        }
        void Poly(params float[] xy)
        {
            int k=_vh.currentVertCount;for(int i=0;i<xy.Length;i+=2)_vh.AddVert(P(xy[i],xy[i+1]),color,Vector2.zero);
            int count=xy.Length/2;var order=new List<int>(count);
            float area=0;for(int i=0;i<count;i++){int j=(i+1)%count;area+=xy[i*2]*xy[j*2+1]-xy[j*2]*xy[i*2+1];order.Add(i);}
            float sign=area>=0?1:-1;
            Vector2 Point(int index)=>new Vector2(xy[index*2],xy[index*2+1]);
            float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
            while(order.Count>3)
            {
                bool found=false;
                for(int i=0;i<order.Count;i++)
                {
                    int a=order[(i+order.Count-1)%order.Count],b=order[i],c=order[(i+1)%order.Count];
                    var pa=Point(a);var pb=Point(b);var pc=Point(c);
                    if(Cross(pa,pb,pc)*sign<=.000001f)continue;
                    bool contains=false;
                    foreach(int other in order)
                    {
                        if(other==a||other==b||other==c)continue;var p=Point(other);
                        if(Cross(pa,pb,p)*sign>=0&&Cross(pb,pc,p)*sign>=0&&Cross(pc,pa,p)*sign>=0){contains=true;break;}
                    }
                    if(contains)continue;
                    _vh.AddTriangle(k+a,k+b,k+c);order.RemoveAt(i);found=true;break;
                }
                if(!found)break;
            }
            if(order.Count==3)_vh.AddTriangle(k+order[0],k+order[1],k+order[2]);
        }
        void Ring(float x,float y,float radius,float width=.05f,int segments=24)
        {
            for(int i=0;i<segments;i++){float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                Line(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius,x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius,width);}
        }
        void Box(float x,float y,float w,float h) {Poly(x,y,x+w,y,x+w,y+h,x,y+h);}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();_vh=vh;
            if(MeshCache.TryGetValue(_glyph,out var cached))
            {
                foreach(var p in cached)vh.AddVert(P(p.x,p.y),color,Vector2.zero);
                for(int i=0;i<cached.Length;i+=3)vh.AddTriangle(i,i+1,i+2);
                return;
            }
            switch(_glyph)
            {
                case CrtGlyph.Heart:
                    Poly(.5f,.14f,.10f,.55f,.1f,.74f,.26f,.86f,.42f,.86f,.5f,.72f,.58f,.86f,.74f,.86f,.9f,.74f,.9f,.55f);break;
                case CrtGlyph.Ammo:
                    Poly(.17f,.17f,.39f,.17f,.39f,.70f,.28f,.89f,.17f,.70f);
                    Poly(.55f,.17f,.77f,.17f,.77f,.70f,.66f,.89f,.55f,.70f);break;
                case CrtGlyph.Heat:
                    for(int j=0;j<3;j++){float x=.25f+j*.25f;Line(x,.2f,x-.06f,.4f);Line(x-.06f,.4f,x+.05f,.61f);Line(x+.05f,.61f,x,.85f);}Line(.15f,.1f,.88f,.1f);break;
                case CrtGlyph.Drill:
                    Box(.35f,.79f,.30f,.09f);Line(.22f,.73f,.78f,.73f,.1f);
                    Line(.23f,.66f,.73f,.55f,.10f);Line(.30f,.49f,.66f,.40f,.09f);Line(.38f,.32f,.59f,.26f,.075f);
                    Poly(.44f,.20f,.56f,.20f,.5f,.08f);break;
                case CrtGlyph.Shield:
                    Line(.18f,.77f,.5f,.9f);Line(.5f,.9f,.82f,.77f);Line(.82f,.77f,.75f,.36f);
                    Line(.75f,.36f,.5f,.13f);Line(.5f,.13f,.25f,.36f);Line(.25f,.36f,.18f,.77f);
                    Box(.455f,.34f,.09f,.39f);Box(.31f,.49f,.38f,.09f);break;
                case CrtGlyph.Detonate:
                    Poly(.50f,.1f,.61f,.32f,.85f,.2f,.73f,.44f,.94f,.54f,.69f,.6f,.81f,.83f,.57f,.71f,.48f,.94f,.39f,.70f,.16f,.84f,.28f,.6f,.08f,.48f,.32f,.4f,.22f,.18f,.43f,.29f);break;
                case CrtGlyph.Flare:
                    Poly(.40f,.15f,.58f,.19f,.50f,.58f,.34f,.53f);Ring(.46f,.72f,.12f);
                    for(int i=0;i<7;i++){float a=i*Mathf.PI/6;Line(.46f+Mathf.Cos(a)*.20f,.72f+Mathf.Sin(a)*.20f,.46f+Mathf.Cos(a)*.30f,.72f+Mathf.Sin(a)*.30f,.04f);}break;
                case CrtGlyph.Grapple:
                    Line(.2f,.14f,.69f,.69f,.08f);Line(.69f,.69f,.50f,.83f,.08f);Line(.69f,.69f,.85f,.55f,.08f);
                    Line(.5f,.83f,.83f,.87f,.08f);Line(.85f,.55f,.9f,.85f,.08f);break;
                case CrtGlyph.Power:
                    Poly(.55f,.94f,.20f,.47f,.47f,.47f,.37f,.08f,.82f,.60f,.54f,.60f);break;
                case CrtGlyph.Turret:
                    Box(.29f,.46f,.43f,.2f);Box(.63f,.57f,.27f,.09f);Box(.40f,.36f,.11f,.16f);
                    Line(.45f,.36f,.22f,.14f,.08f);Line(.48f,.36f,.73f,.14f,.08f);Ring(.46f,.75f,.1f);break;
                case CrtGlyph.Dash:
                    Poly(.38f,.18f,.62f,.50f,.38f,.82f,.61f,.82f,.87f,.50f,.61f,.18f);
                    Line(.12f,.28f,.3f,.28f,.07f);Line(.06f,.5f,.39f,.5f,.07f);Line(.12f,.72f,.3f,.72f,.07f);break;
                case CrtGlyph.Crew:
                    Ring(.5f,.75f,.13f,.12f);Poly(.29f,.13f,.32f,.5f,.68f,.5f,.71f,.13f);
                    Ring(.17f,.60f,.09f,.08f);Box(.07f,.16f,.16f,.25f);Ring(.83f,.60f,.09f,.08f);Box(.77f,.16f,.16f,.25f);break;
                case CrtGlyph.Boss:
                    Poly(.24f,.62f,.33f,.83f,.67f,.83f,.76f,.62f,.64f,.32f,.36f,.32f);
                    Line(.26f,.7f,.12f,.88f,.07f);Line(.74f,.7f,.88f,.88f,.07f);
                    Box(.37f,.14f,.08f,.23f);Box(.55f,.14f,.08f,.23f);break;
                case CrtGlyph.Depth:
                    for(int i=0;i<3;i++){float y=.80f-i*.23f;Line(.22f,y,.5f,y-.18f,.075f);Line(.5f,y-.18f,.78f,y,.075f);}break;
                case CrtGlyph.Warning:
                    Line(.5f,.90f,.08f,.14f,.07f);Line(.08f,.14f,.92f,.14f,.07f);Line(.92f,.14f,.5f,.90f,.07f);
                    Box(.465f,.37f,.07f,.28f);Box(.465f,.24f,.07f,.07f);break;
                case CrtGlyph.Rescue: Box(.40f,.13f,.20f,.74f);Box(.13f,.40f,.74f,.20f);break;
                case CrtGlyph.Signal:
                    for(int i=0;i<4;i++)Box(.12f+i*.20f,.18f,.12f,.13f+i*.17f);break;
                case CrtGlyph.Core:
                    Poly(.5f,.1f,.2f,.36f,.3f,.78f,.53f,.93f,.8f,.67f,.77f,.31f);break;
                case CrtGlyph.Lock:
                    Box(.23f,.13f,.54f,.4f);Ring(.5f,.62f,.21f,.08f);break;
                case CrtGlyph.Eye:
                    Line(.07f,.5f,.29f,.73f);Line(.29f,.73f,.71f,.73f);Line(.71f,.73f,.93f,.5f);
                    Line(.93f,.5f,.71f,.27f);Line(.71f,.27f,.29f,.27f);Line(.29f,.27f,.07f,.5f);Ring(.5f,.5f,.15f,.09f);break;
                case CrtGlyph.Reload: Ring(.5f,.5f,.31f);Poly(.60f,.88f,.9f,.82f,.78f,.57f);break;
                case CrtGlyph.Xp: Poly(.5f,.1f,.7f,.35f,.86f,.5f,.7f,.65f,.5f,.9f,.3f,.65f,.14f,.5f,.3f,.35f);break;
            }
            var stream=new List<UIVertex>();vh.GetUIVertexStream(stream);
            var points=new Vector2[stream.Count];var r=rectTransform.rect;
            for(int i=0;i<points.Length;i++)points[i]=new Vector2((stream[i].position.x-r.xMin)/r.width,(stream[i].position.y-r.yMin)/r.height);
            MeshCache[_glyph]=points;
        }
    }
}
