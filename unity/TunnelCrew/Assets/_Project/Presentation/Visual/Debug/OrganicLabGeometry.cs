using System.Collections.Generic;
using UnityEngine;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>Render-only subdivision of the existing solid field. Shared boundary samples
    /// keep caps, bevels and front skirts watertight, including concave rooms and wall islands.</summary>
    public sealed class OrganicLabGeometry
    {
        public const int Subdivision = 4;
        public const float MaxOffset = 0.18f;
        public readonly List<WallContourTracer.Contour> Contours = new();
        public readonly List<List<Vector2>> Loops = new();
        readonly Dictionary<Vector2Int, Vector2> _boundary = new();

        public OrganicLabGeometry(ISolidField field)
        {
            WallContourTracer.Trace((x,y) => x >= 0 && y >= 0 && x < field.Cols && y < field.Rows
                && field.IsSolid(x,y), field.Cols, field.Rows, Contours, false);
            foreach (var contour in Contours)
            {
                var raw = new List<Vector2>();
                for (int i = 0; i < contour.Points.Count; i++)
                {
                    var a = contour.Points[i]; var b = contour.Points[(i+1)%contour.Points.Count];
                    for (int j = 0; j < Subdivision; j++)
                        raw.Add(Vector2.Lerp(new Vector2(a.X,a.Y),new Vector2(b.X,b.Y),j/(float)Subdivision));
                }
                var smooth = new List<Vector2>(raw.Count);
                for (int i = 0; i < raw.Count; i++)
                {
                    Vector2 before = raw[(i+raw.Count-1)%raw.Count], after = raw[(i+1)%raw.Count];
                    var tangent = (after-before).normalized;
                    var normal = new Vector2(-tangent.y,tangent.x);
                    var p = raw[i];
                    float noise = Mathf.Sin(p.x*2.73f+p.y*1.37f)*0.075f
                                + Mathf.Sin(p.x*5.19f-p.y*3.41f)*0.035f;
                    var offset = ((before+after)*0.5f-p)*0.9f + normal*noise;
                    var value = p + Vector2.ClampMagnitude(offset,MaxOffset);
                    // A corner shared by diagonally touching walls gets one authoritative position.
                    var key = Key(p);
                    if (_boundary.TryGetValue(key,out var shared)) value=shared;
                    else _boundary.Add(key,value);
                    smooth.Add(value);
                }
                Loops.Add(smooth);
            }
        }

        static Vector2Int Key(Vector2 p) => new(Mathf.RoundToInt(p.x*Subdivision),Mathf.RoundToInt(p.y*Subdivision));
        public Vector2 Point(float x,float y)
        {
            var p=new Vector2(x,y);
            return _boundary.TryGetValue(Key(p),out var value)?value:p;
        }

        public sealed class Surface
        {
            readonly List<Vector3> _v=new();
            readonly List<Vector2> _uv=new();
            readonly List<Color> _color=new();
            readonly List<int> _tri=new();
            public int VertexCount => _v.Count;
            public void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color lower,Color upper)
            {
                int n=_v.Count;
                _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
                _uv.Add(a); _uv.Add(b); _uv.Add(c); _uv.Add(d);
                _color.Add(lower);_color.Add(lower);_color.Add(upper);_color.Add(upper);
                _tri.Add(n);_tri.Add(n+1);_tri.Add(n+2);_tri.Add(n);_tri.Add(n+2);_tri.Add(n+3);
            }
            public Mesh CreateMesh(string name)
            {
                var mesh=new Mesh {name=name};
                mesh.SetVertices(_v); mesh.SetUVs(0,_uv); mesh.SetColors(_color); mesh.SetTriangles(_tri,0);
                mesh.RecalculateBounds(); return mesh;
            }
        }
    }
}
