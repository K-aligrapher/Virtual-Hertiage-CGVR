using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VirtualHeritage
{
    // Accumulates many primitives into one mesh so dense carved ornament stays cheap to draw.
    // Triangles are clockwise when seen from outside (Unity front faces).
    public sealed class MeshBuilder
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> t = new List<int>();
        readonly Stack<Matrix4x4> stack = new Stack<Matrix4x4>();
        Matrix4x4 m = Matrix4x4.identity;
        const float Tiling = .5f;

        public int VertexCount => v.Count;

        public void Push(Vector3 position, Quaternion rotation, float scale = 1)
        {
            stack.Push(m); m = m * Matrix4x4.TRS(position, rotation, Vector3.one * scale);
        }

        public void Pop() { m = stack.Pop(); }

        Vector3 P(Vector3 p) => m.MultiplyPoint3x4(p);
        Vector3 D(Vector3 d) => m.MultiplyVector(d).normalized;
        float Scale => m.MultiplyVector(Vector3.right).magnitude;

        void Face(Vector3 c, Vector3 normal, Vector3 up, float w, float h)
        {
            Vector3 r = Vector3.Cross(normal, up);
            Vector3 tl = P(c - r * w + up * h), tr = P(c + r * w + up * h), br = P(c + r * w - up * h), bl = P(c - r * w - up * h);
            Vector3 wn = D(normal), wr = D(r), wu = D(up);
            int i = v.Count;
            v.Add(tl); v.Add(tr); v.Add(br); v.Add(bl);
            for (int k = 0; k < 4; k++) n.Add(wn);
            uv.Add(new Vector2(Vector3.Dot(tl, wr), Vector3.Dot(tl, wu)) * Tiling);
            uv.Add(new Vector2(Vector3.Dot(tr, wr), Vector3.Dot(tr, wu)) * Tiling);
            uv.Add(new Vector2(Vector3.Dot(br, wr), Vector3.Dot(br, wu)) * Tiling);
            uv.Add(new Vector2(Vector3.Dot(bl, wr), Vector3.Dot(bl, wu)) * Tiling);
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        public void Box(Vector3 c, Vector3 size) => Box(c, size, Quaternion.identity);

        public void Box(Vector3 c, Vector3 size, Quaternion rot)
        {
            Vector3 h = size * .5f;
            Vector3 x = rot * Vector3.right, y = rot * Vector3.up, z = rot * Vector3.forward;
            Face(c + x * h.x, x, y, h.z, h.y);
            Face(c - x * h.x, -x, y, h.z, h.y);
            Face(c + z * h.z, z, y, h.x, h.y);
            Face(c - z * h.z, -z, y, h.x, h.y);
            Face(c + y * h.y, y, z, h.x, h.z);
            Face(c - y * h.y, -y, z, h.x, h.z);
        }

        // A flat-shaded triangle with planar UVs.
        public void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            a = P(a); b = P(b); c = P(c);
            var normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-12f) return;
            normal.Normalize();
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); n.Add(normal); n.Add(normal); n.Add(normal);
            if (Mathf.Abs(normal.y) > .7f) { uv.Add(new Vector2(a.x, a.z) * Tiling); uv.Add(new Vector2(b.x, b.z) * Tiling); uv.Add(new Vector2(c.x, c.z) * Tiling); }
            else
            {
                var tangent = Vector3.Cross(Vector3.up, normal).normalized;
                uv.Add(new Vector2(Vector3.Dot(a, tangent), a.y) * Tiling); uv.Add(new Vector2(Vector3.Dot(b, tangent), b.y) * Tiling); uv.Add(new Vector2(Vector3.Dot(c, tangent), c.y) * Tiling);
            }
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Tri(a, b, c); Tri(a, c, d); }

        // Rectangular frustum: bottom rectangle at c, top rectangle h above. Used for sloping eaves (chhajja).
        public void Slab(Vector3 c, Vector2 bottom, Vector2 top, float h)
        {
            Vector3 B(float sx, float sz) => c + new Vector3(sx * bottom.x * .5f, 0, sz * bottom.y * .5f);
            Vector3 T(float sx, float sz) => c + new Vector3(sx * top.x * .5f, h, sz * top.y * .5f);
            Quad(T(-1, -1), T(-1, 1), T(1, 1), T(1, -1));
            Quad(B(-1, -1), B(1, -1), B(1, 1), B(-1, 1));
            Quad(B(-1, -1), T(-1, -1), T(1, -1), B(1, -1));
            Quad(B(1, 1), T(1, 1), T(-1, 1), B(-1, 1));
            Quad(B(-1, 1), T(-1, 1), T(-1, -1), B(-1, -1));
            Quad(B(1, -1), T(1, -1), T(1, 1), B(1, 1));
        }

        public void Lathe(Vector2[] profile, int sides, Vector3 c, bool smooth = false) => Lathe(profile, sides, c, Quaternion.identity, Vector3.one, smooth);

        // Revolves a (radius, height) profile. Profiles that climb upward face outward.
        public void Lathe(Vector2[] profile, int sides, Vector3 c, Quaternion rot, Vector3 scale, bool smooth = false)
        {
            int count = profile.Length;
            var segment = new Vector2[count - 1];
            float maxRadius = .05f;
            for (int s = 0; s < count - 1; s++)
            {
                var d = profile[s + 1] - profile[s];
                segment[s] = d.sqrMagnitude > 1e-10f ? new Vector2(d.y, -d.x).normalized : Vector2.up;
                maxRadius = Mathf.Max(maxRadius, profile[s].x, profile[s + 1].x);
            }
            float s0 = Scale, around = maxRadius * Mathf.PI * 2 * s0 * Mathf.Max(scale.x, scale.z) * Tiling, along = 0;
            for (int s = 0; s < count - 1; s++)
            {
                float length = (profile[s + 1] - profile[s]).magnitude;
                if (length < 1e-5f) continue;
                Vector2 n0 = segment[s], n1 = segment[s];
                if (smooth)
                {
                    if (s > 0) n0 = (segment[s - 1] + segment[s]).normalized;
                    if (s < count - 2) n1 = (segment[s] + segment[s + 1]).normalized;
                }
                int start = v.Count;
                for (int ring = 0; ring < 2; ring++)
                {
                    var p = profile[s + ring]; var pn = ring == 0 ? n0 : n1;
                    float vv = (along + ring * length) * scale.y * s0 * Tiling;
                    for (int j = 0; j <= sides; j++)
                    {
                        float a = j * Mathf.PI * 2 / sides, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                        var local = new Vector3(ca * p.x * scale.x, p.y * scale.y, sa * p.x * scale.z);
                        var ln = new Vector3(ca * pn.x / scale.x, pn.y / scale.y, sa * pn.x / scale.z);
                        v.Add(P(c + rot * local)); n.Add(D(rot * ln.normalized));
                        uv.Add(new Vector2((float)j / sides * around, vv));
                    }
                }
                for (int j = 0; j < sides; j++)
                {
                    int k = start + j, up = start + sides + 1 + j;
                    t.Add(k); t.Add(up); t.Add(k + 1);
                    t.Add(k + 1); t.Add(up); t.Add(up + 1);
                }
                along += length;
            }
        }

        static readonly Dictionary<int, Vector2[]> sphereProfiles = new Dictionary<int, Vector2[]>();

        static Vector2[] SphereProfile(int lat)
        {
            if (sphereProfiles.TryGetValue(lat, out var p)) return p;
            p = new Vector2[lat + 1];
            for (int i = 0; i <= lat; i++) { float a = Mathf.PI * i / lat; p[i] = new Vector2(Mathf.Sin(a), -Mathf.Cos(a)); }
            sphereProfiles[lat] = p; return p;
        }

        public void Ellipsoid(Vector3 c, Vector3 radii) => Ellipsoid(c, radii, Quaternion.identity);
        public void Ellipsoid(Vector3 c, Vector3 radii, Quaternion rot, int sides = 10, int lat = 6) => Lathe(SphereProfile(lat), sides, c, rot, radii, true);

        public void Cylinder(Vector3 bottom, float radius, float height, int sides = 12) => Lathe(new[] { new Vector2(0, 0), new Vector2(radius, 0), new Vector2(radius, height), new Vector2(0, height) }, sides, bottom);

        // Stellate prism: alternating outer and inner corners, the first outer point facing -Z.
        public void Star(Vector3 c, float outer, float inner, int points, float h, float topScale = 1, bool bottom = false)
        {
            int corners = points * 2; var top = c + Vector3.up * h;
            for (int i = 0; i < corners; i++)
            {
                float a = -Mathf.PI * .5f + i * Mathf.PI * 2 / corners, b = -Mathf.PI * .5f + (i + 1) * Mathf.PI * 2 / corners;
                float ra = i % 2 == 0 ? outer : inner, rb = i % 2 == 0 ? inner : outer;
                var da = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var db = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                Vector3 p = c + da * ra, q = c + db * rb, pt = top + da * ra * topScale, qt = top + db * rb * topScale;
                Tri(top, qt, pt);
                Tri(p, pt, qt); Tri(p, qt, q);
                if (bottom) Tri(c, p, q);
            }
        }

        // Calls place(position, tangent, outward) at even spacing around a stellate outline.
        public static void AlongStar(Vector3 c, float outer, float inner, int points, float spacing, System.Action<Vector3, Vector3, Vector3> place)
        {
            int corners = points * 2;
            for (int i = 0; i < corners; i++)
            {
                float a = -Mathf.PI * .5f + i * Mathf.PI * 2 / corners, b = -Mathf.PI * .5f + (i + 1) * Mathf.PI * 2 / corners;
                float ra = i % 2 == 0 ? outer : inner, rb = i % 2 == 0 ? inner : outer;
                var p = c + new Vector3(Mathf.Cos(a) * ra, 0, Mathf.Sin(a) * ra); var q = c + new Vector3(Mathf.Cos(b) * rb, 0, Mathf.Sin(b) * rb);
                var edge = q - p; float length = edge.magnitude; var tangent = edge / length;
                var outward = Vector3.Cross(Vector3.up, tangent).normalized;
                int count = Mathf.Max(1, Mathf.FloorToInt(length / spacing));
                for (int k = 0; k < count; k++) place(p + edge * ((k + .5f) / count), tangent, outward);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
        }
    }
}
