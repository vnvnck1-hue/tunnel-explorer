using System.Collections.Generic;
using UnityEngine;

namespace TunnelCrew.Presentation
{
    /// <summary>
    /// 천장 붕괴 — 원본 <c>J.ceiling</c> / <c>J.ceilingArea</c>. 돌·먼지가 높이 z 에서 떨어져(55~120px/s) 바닥에 닿으면 잠시 남았다 사라진다.
    /// 보스 돌진 착지(dashEnd) 때 화면 전체에 돌진 지점에서 밖으로 번지는 낙하 파동으로 쓴다.
    /// </summary>
    public sealed class CeilingFx : MonoBehaviour
    {
        sealed class Debris { public bool Rock; public Vector2 P, V; public float Z, Fall, Rot, VR, Size, Alpha, Life = 1, Delay; public bool Landed; }
        readonly List<Debris> _bd = new List<Debris>();
        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        Sprite _square, _dot;
        const int Max = 420;

        void Awake() { _square = ProcSprites.Square(); _dot = ProcSprites.Circle(16, .8f); }

        /// <summary>한 뭉치 — (x,y) 주변 spread 에 rocks 개의 돌 + dust 개의 먼지를 height(셀) 에서 떨어뜨린다.</summary>
        public void Ceiling(Vector2 at, int rocks, int dust, float alpha, float height, float delay = 0, float spreadMul = 1)
        {
            int count = Mathf.Max(0, rocks + dust);
            for (int i = 0; i < count; i++)
            {
                bool rock = i < rocks; float a = Random.value * 6.283f, spread = (.12f + Random.value * .55f) * spreadMul;
                _bd.Add(new Debris
                {
                    Rock = rock, P = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * spread, Z = height * (.72f + Random.value * .5f),
                    Fall = (55 + Random.value * 65) / 50f, V = new Vector2((Random.value - .5f) * 18, (Random.value - .5f) * 12) / 50f,
                    Rot = Random.value * 360, VR = (Random.value - .5f) * 400, Size = (rock ? 5 + Random.value * 7 : 7 + Random.value * 10) / 50f,
                    Alpha = alpha, Delay = Mathf.Max(0, delay) + Random.value * .07f,
                });
            }
            if (_bd.Count > Max) _bd.RemoveRange(0, _bd.Count - Max);
        }
        /// <summary>화면 전체 — 격자로 clusters 뭉치를 깔고, 원점(ox,oy)에서 멀수록 wave 만큼 늦게 떨어진다.</summary>
        public void CeilingArea(Rect area, int clusters, int rocks, int dust, float alpha, float height, float wave, Vector2 origin)
        {
            int n = Mathf.Max(1, clusters); float spanX = Mathf.Max(1, area.width), spanY = Mathf.Max(1, area.height), maxD = Mathf.Sqrt(spanX * spanX + spanY * spanY);
            int cols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(n * spanX / spanY))), rows = Mathf.Max(1, Mathf.CeilToInt(n / (float)cols));
            for (int i = 0; i < n; i++)
            {
                int gc = i % cols, gr = Mathf.Min(rows - 1, i / cols);
                var p = new Vector2(area.x + spanX * ((gc + .15f + Random.value * .7f) / cols), area.y + spanY * ((gr + .15f + Random.value * .7f) / rows));
                float d = Vector2.Distance(p, origin);
                Ceiling(p, rocks, dust, alpha, height, wave * Mathf.Min(1, d / maxD) * (.7f + Random.value * .6f), 1.25f);
            }
        }

        SpriteRenderer Rent(int i)
        {
            while (_pool.Count <= i) { var go = new GameObject("debris"); go.transform.SetParent(transform, false); var s = go.AddComponent<SpriteRenderer>(); s.sortingOrder = 36; _pool.Add(s); }
            var r = _pool[i]; r.gameObject.SetActive(true); return r;
        }

        void Update()
        {
            float dt = Time.deltaTime; int i = 0;
            for (int k = _bd.Count - 1; k >= 0; k--)
            {
                var d = _bd[k];
                if (d.Delay > 0) { d.Delay -= dt; continue; }
                if (!d.Landed)
                {
                    d.Z -= d.Fall * dt; d.P += d.V * dt; d.Rot += d.VR * dt;
                    if (d.Z <= 0) { d.Z = 0; d.Landed = true; d.V = Vector2.zero; }
                }
                else { d.Life -= dt / (d.Rock ? 1.4f : .6f); if (d.Life <= 0) { _bd.RemoveAt(k); continue; } }
                // 그림자(바닥) + 몸통(z 만큼 위)
                var sh = Rent(i++); sh.sprite = _dot; sh.color = new Color(0, 0, 0, .22f * d.Alpha * d.Life * Mathf.Clamp01(1 - d.Z / 4)); sh.transform.position = new Vector3(d.P.x, d.P.y, 0); sh.transform.localScale = Vector3.one * d.Size * 1.3f; sh.transform.rotation = Quaternion.identity; sh.sortingOrder = 35;
                var b = Rent(i++); b.sprite = d.Rock ? _square : _dot;
                b.color = d.Rock ? new Color(.33f, .27f, .3f, d.Alpha * 3.2f * d.Life) : new Color(.55f, .48f, .45f, d.Alpha * 1.6f * d.Life);
                b.transform.position = new Vector3(d.P.x, d.P.y + d.Z, 0); b.transform.localScale = Vector3.one * d.Size; b.transform.rotation = Quaternion.Euler(0, 0, d.Rot); b.sortingOrder = 36;
            }
            for (; i < _pool.Count; i++) _pool[i].gameObject.SetActive(false);
        }
    }
}
