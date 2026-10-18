using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VirtualHeritage
{
    public sealed class FlickerLight : MonoBehaviour
    {
        public Light lamp; public Transform flame; float baseIntensity, seed;
        void Start() { baseIntensity = lamp.intensity; seed = Random.value * 100; }
        void Update()
        {
            float n = Mathf.PerlinNoise(Time.time * 6 + seed, seed) * .5f + Mathf.PerlinNoise(Time.time * 17, seed * 2) * .25f;
            lamp.intensity = baseIntensity * (.75f + n);
            if (flame) flame.localScale = new Vector3(1, .85f + n * .7f, 1);
        }
    }

    // Dressing that makes the schematic architecture feel inhabited: landscape, walls, lamps, garlands, smoke and light.
    public static class HeritageAtmosphere
    {
        static Texture2D softDisc;
        static Material sprite;

        public static Material SpriteMaterial()
        {
            if (sprite) return sprite;
            softDisc = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Soft disc" };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f; float a = Mathf.Clamp01(1 - d); a *= a;
                softDisc.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            softDisc.Apply();
            sprite = new Material(HeritageGeometry.FindShader("Sprites/Default")) { name = "Soft particle", mainTexture = softDisc };
            return sprite;
        }

        public static void Release() { if (sprite) Object.Destroy(sprite); if (softDisc) Object.Destroy(softDisc); sprite = null; softDisc = null; }

        static float R(System.Random rnd) => (float)rnd.NextDouble();

        // ---------- Landscape ----------

        public static void Palm(HeritageGeometry g, Transform parent, Vector3 pos, float height, System.Random rnd)
        {
            var root = g.Group("Coconut palm", parent, pos);
            var bark = new MeshBuilder(); var leaves = new MeshBuilder(); var nuts = new MeshBuilder();
            var lean = Quaternion.Euler((R(rnd) - .5f) * 9, R(rnd) * 360, (R(rnd) - .5f) * 9);
            int rings = Mathf.Max(6, Mathf.RoundToInt(height / .3f));
            var profile = new List<Vector2> { new Vector2(0, 0), new Vector2(.42f, 0) };
            for (int i = 0; i < rings; i++)
            {
                float t = i / (float)rings, r = Mathf.Lerp(.3f, .17f, Mathf.Sqrt(t)), y = t * height, step = height / rings;
                profile.Add(new Vector2(r * 1.05f, y + step * .2f)); profile.Add(new Vector2(r * .95f, y + step * .85f));
            }
            profile.Add(new Vector2(.2f, height)); profile.Add(new Vector2(0, height + .1f));
            bark.Lathe(profile.ToArray(), 10, Vector3.zero, lean, Vector3.one);
            var crown = lean * new Vector3(0, height, 0);
            for (int i = 0; i < 6; i++) nuts.Ellipsoid(crown + Quaternion.Euler(0, i * 60 + R(rnd) * 20, 0) * new Vector3(.22f, -.18f - R(rnd) * .1f, 0), new Vector3(.13f, .15f, .13f));
            int fronds = 13;
            for (int f = 0; f < fronds; f++)
            {
                var dir = Quaternion.Euler(0, f * 360f / fronds + R(rnd) * 18, 0);
                Vector3 forward = dir * Vector3.forward, side = dir * Vector3.right;
                float rise = Mathf.Sin((42 - (f % 3) * 20 + R(rnd) * 10) * Mathf.Deg2Rad), length = 2.5f + R(rnd) * .9f;
                Vector3 Spine(float t) => crown + forward * length * t + Vector3.up * length * (rise * t - .55f * t * t);
                const int segments = 16;
                for (int s = 0; s < segments; s++)
                {
                    float t0 = s / (float)segments, t1 = (s + 1) / (float)segments;
                    Vector3 a = Spine(t0), b = Spine(t1), seg = b - a;
                    leaves.Box((a + b) * .5f, new Vector3(.035f, .03f, seg.magnitude), Quaternion.LookRotation(seg));
                    float leaflet = .18f + .75f * Mathf.Sin(Mathf.PI * Mathf.Min(1, t1 * 1.15f)) * (1 - t1 * .35f);
                    for (int k = -1; k <= 1; k += 2)
                    {
                        var tip = b + side * k * leaflet + Vector3.down * leaflet * .5f + forward * leaflet * .2f;
                        leaves.Tri(a, b, tip); leaves.Tri(a + seg * .5f, tip, b + seg * .3f);
                    }
                }
            }
            g.Emit("Trunk", root.transform, g.Bark, bark);
            g.Emit("Fronds", root.transform, g.Foliage, leaves);
            g.Emit("Coconuts", root.transform, g.Wood, nuts);
            var col = root.AddComponent<CapsuleCollider>(); col.center = new Vector3(0, 1.5f, 0); col.radius = .32f; col.height = 3;
        }

        static readonly int[] IcoFaces = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };

        // Weathered granite boulder: noise-displaced icosphere, flat shaded for crisp facets.
        public static Mesh Rock(HeritageGeometry g, int seed)
        {
            float t = (1 + Mathf.Sqrt(5)) / 2;
            var verts = new List<Vector3> {
                new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0), new Vector3(1,-t,0), new Vector3(0,-1,t), new Vector3(0,1,t),
                new Vector3(0,-1,-t), new Vector3(0,1,-t), new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1) };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            var tris = new List<int>(IcoFaces);
            for (int level = 0; level < 2; level++)
            {
                var mid = new Dictionary<long, int>(); var next = new List<int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (mid.TryGetValue(key, out int found)) return found;
                    verts.Add(((verts[a] + verts[b]) * .5f).normalized); mid[key] = verts.Count - 1; return verts.Count - 1;
                }
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = next;
            }
            float s1 = seed * 3.7f, s2 = seed * 1.9f;
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                float n = (Mathf.PerlinNoise(v.x * 1.4f + s1, v.y * 1.4f) + Mathf.PerlinNoise(v.y * 1.4f + s2, v.z * 1.4f) + Mathf.PerlinNoise(v.z * 1.4f, v.x * 1.4f + s1)) / 3f;
                float f = (Mathf.PerlinNoise(v.x * 4f + s2, v.z * 4f) - .5f) * .12f;
                var p = v * (.78f + n * .5f + f);
                if (p.y < -.3f) p.y = -.3f + (p.y + .3f) * .15f;
                verts[i] = p;
            }
            var mb = new MeshBuilder();
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0) { var swap = b; b = c; c = swap; }
                mb.Tri(a, b, c);
            }
            return g.Track(mb.ToMesh("Granite boulder"));
        }

        public static void Boulders(HeritageGeometry g, Transform parent, Mesh[] rocks, Vector3 position, System.Random random, float scale = 1)
        {
            int count = 2 + random.Next(3);
            for (int i = 0; i < count; i++)
            {
                var rock = g.MeshObject("Granite boulder", parent, rocks[random.Next(rocks.Length)], g.Granite);
                float s = (2.5f + R(random) * 4.5f) * scale;
                rock.transform.localPosition = position + new Vector3((R(random) * 6 - 3) * scale, s * (scale > 1 ? .05f : .26f), (R(random) * 6 - 3) * scale);
                rock.transform.localScale = new Vector3(s, s * (.55f + R(random) * .45f), s * (.75f + R(random) * .4f));
                rock.transform.localRotation = Quaternion.Euler(R(random) * 14, R(random) * 360, R(random) * 14);
            }
        }

        static void Grass(HeritageGeometry g, Transform parent, System.Random rnd, int tufts)
        {
            var mb = new MeshBuilder(); int placed = 0, guard = 0;
            while (placed < tufts && guard++ < tufts * 8)
            {
                float a = R(rnd) * Mathf.PI * 2, r = 19 + 34 * Mathf.Sqrt(R(rnd));
                var c = new Vector3(Mathf.Cos(a) * r, -.05f, Mathf.Sin(a) * r + 2);
                if (Mathf.Abs(c.x) < 18.6f && c.z > -24.5f && c.z < 24.5f) continue;
                if (Mathf.Abs(c.x) < 3 && c.z < -24) continue;
                int blades = 4 + rnd.Next(5);
                for (int b = 0; b < blades; b++)
                {
                    var basePos = c + new Vector3(R(rnd) - .5f, 0, R(rnd) - .5f) * .3f;
                    var right = Quaternion.Euler(0, R(rnd) * 360, 0) * Vector3.right * (.025f + R(rnd) * .02f);
                    float h = .2f + R(rnd) * .35f;
                    var tip = basePos + Vector3.up * h + new Vector3(R(rnd) - .5f, 0, R(rnd) - .5f) * h * .7f;
                    mb.Tri(basePos - right, tip, basePos + right);
                }
                placed++;
            }
            var go = g.Emit("Wild grass", parent, g.Blade, mb);
            if (go) go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        public static void Garden(HeritageGeometry g, Transform parent, int seed)
        {
            var random = new System.Random(seed);
            g.Box("Ground", parent, new Vector3(0, -.25f, 4), new Vector3(300, .4f, 300), g.Grass);
            g.Box("Courtyard paving", parent, new Vector3(0, -.07f, 0), new Vector3(35, .14f, 46), g.Ground);
            g.Box("Processional path", parent, new Vector3(0, 0, -10), new Vector3(4.6f, .06f, 26), g.Slab);
            g.Box("Approach path", parent, new Vector3(0, -.03f, -34), new Vector3(4.6f, .06f, 22), g.Slab);
            var rocks = new Mesh[6]; for (int i = 0; i < rocks.Length; i++) rocks[i] = Rock(g, seed * 11 + i);
            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2 + R(random) * .1f, r = 48 + R(random) * 22;
                Boulders(g, parent, rocks, new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r + 8), random);
            }
            // Distant boulder hills in the manner of the Hampi landscape.
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2 + R(random) * .2f, r = 98 + R(random) * 28;
                Boulders(g, parent, rocks, new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r), random, 2.2f + R(random) * 1.2f);
            }
            for (int i = 0; i < 40; i++)
            {
                float a = R(random) * Mathf.PI * 2, r = 28 + R(random) * 22;
                var pos = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r + 2);
                if (Mathf.Abs(pos.x) < 21 && pos.z > -27 && pos.z < 27) continue;
                if (Mathf.Abs(pos.x) < 5 && pos.z < -20) continue;
                Palm(g, parent, pos, 6 + R(random) * 3, random);
            }
            Grass(g, parent, random, 3200);
        }

        // ---------- Courtyard dressing ----------

        public static void Lamp(HeritageGeometry g, Transform parent, Vector3 pos, bool light, float intensity = 1.6f)
        {
            var root = g.Group("Deepastambha oil lamp", parent, pos);
            var stone = new MeshBuilder(); var brass = new MeshBuilder();
            stone.Box(new Vector3(0, .08f, 0), new Vector3(.46f, .16f, .46f));
            stone.Box(new Vector3(0, .19f, 0), new Vector3(.34f, .06f, .34f));
            brass.Lathe(new[] { new Vector2(0, .22f), new Vector2(.12f, .22f), new Vector2(.09f, .3f), new Vector2(.05f, .45f), new Vector2(.08f, .52f), new Vector2(.05f, .6f), new Vector2(.04f, .8f), new Vector2(.07f, .86f), new Vector2(.05f, .92f), new Vector2(.04f, 1.05f), new Vector2(.09f, 1.1f), new Vector2(.16f, 1.14f), new Vector2(.23f, 1.22f), new Vector2(.2f, 1.23f), new Vector2(.05f, 1.19f) }, 18, Vector3.zero, true);
            for (int i = 0; i < 5; i++) brass.Ellipsoid(Quaternion.Euler(0, i * 72, 0) * new Vector3(.2f, 1.23f, 0), new Vector3(.03f, .02f, .03f), Quaternion.identity, 6, 3);
            g.Emit("Lamp base", root.transform, g.Edge, stone);
            g.Emit("Lamp stem", root.transform, g.Brass, brass);
            var flame = g.MeshObject("Flame", root.transform, g.Lathe(new[] { new Vector2(0, 0), new Vector2(.05f, .06f), new Vector2(.03f, .15f), new Vector2(0, .26f) }, 8), g.Flame);
            flame.transform.localPosition = new Vector3(0, 1.22f, 0);
            flame.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            if (light)
            {
                var lg = new GameObject("Lamp glow"); lg.transform.SetParent(root.transform, false); lg.transform.localPosition = new Vector3(0, 1.5f, 0);
                var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, .62f, .25f); l.range = 7; l.intensity = intensity; l.shadows = LightShadows.None;
                var f = lg.AddComponent<FlickerLight>(); f.lamp = l; f.flame = flame.transform;
            }
            Smoke(root.transform, new Vector3(0, 1.4f, 0), new Color(.8f, .75f, .7f, .1f), .2f, 1.4f, .5f);
            var col = root.AddComponent<CapsuleCollider>(); col.center = new Vector3(0, .65f, 0); col.radius = .24f; col.height = 1.3f;
        }

        public static void Smoke(Transform parent, Vector3 local, Color tint, float size, float life, float rate)
        {
            var go = new GameObject("Incense smoke"); go.transform.SetParent(parent, false); go.transform.localPosition = local; go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var ps = go.AddComponent<ParticleSystem>(); var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = SpriteMaterial();
            var m = ps.main; m.startLifetime = life * 3; m.startSpeed = .35f; m.startSize = new ParticleSystem.MinMaxCurve(size, size * 2); m.startColor = tint; m.maxParticles = 60; m.simulationSpace = ParticleSystemSimulationSpace.World;
            var e = ps.emission; e.rateOverTime = rate * 4;
            var sh = ps.shape; sh.angle = 6; sh.radius = .02f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient(); grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .25f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .4f, 1, 2.6f));
            var noise = ps.noise; noise.enabled = true; noise.strength = .25f; noise.frequency = .6f;
        }

        public static void Motes(Transform parent, Vector3 centre, Vector3 area, Color tint, int count, float size)
        {
            var go = new GameObject("Drifting dust and petals"); go.transform.SetParent(parent, false); go.transform.localPosition = centre;
            var ps = go.AddComponent<ParticleSystem>(); var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = SpriteMaterial();
            var m = ps.main; m.startLifetime = 14; m.startSpeed = .08f; m.startSize = new ParticleSystem.MinMaxCurve(size * .5f, size); m.startColor = tint; m.maxParticles = count; m.simulationSpace = ParticleSystemSimulationSpace.World; m.prewarm = true;
            var e = ps.emission; e.rateOverTime = count / 14f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = area;
            var n = ps.noise; n.enabled = true; n.strength = .35f; n.frequency = .2f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient(); grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(1, .8f), new GradientAlphaKey(0, 1) });
            col.color = grad;
        }

        public static void Garland(HeritageGeometry g, Transform parent, Vector3 a, Vector3 b, float sag, int beads = 22)
        {
            var root = g.Group("Marigold garland", parent, Vector3.zero);
            var red = new MeshBuilder(); var gold = new MeshBuilder(); var orange = new MeshBuilder();
            int count = beads * 2;
            for (int i = 0; i <= count; i++)
            {
                float t = i / (float)count; var p = Vector3.Lerp(a, b, t); p.y -= Mathf.Sin(t * Mathf.PI) * sag;
                var mb = i % 9 == 0 ? red : (i % 2 == 0 ? gold : orange);
                mb.Ellipsoid(p, Vector3.one * (.075f + (i % 3) * .008f), Quaternion.Euler(0, i * 37, 0), 8, 5);
            }
            for (int tassel = 1; tassel < 4; tassel++)
            {
                var p = Vector3.Lerp(a, b, tassel / 4f); p.y -= Mathf.Sin(tassel / 4f * Mathf.PI) * sag;
                for (int k = 1; k <= 4; k++) (k % 2 == 0 ? gold : red).Ellipsoid(p + Vector3.down * k * .11f, Vector3.one * .06f, Quaternion.identity, 8, 4);
            }
            g.Emit("Kumkum blooms", root.transform, g.Kumkum, red);
            g.Emit("Marigold blooms", root.transform, g.Marigold, gold);
            g.Emit("Saffron blooms", root.transform, g.Saffron, orange);
        }

        public static void Rangoli(HeritageGeometry g, Transform parent, Vector3 centre, float radius)
        {
            var root = g.Group("Rangoli", parent, centre);
            Material[] ms = { g.Kumkum, g.Pale, g.Marigold, g.Teal, g.Pale };
            var builders = new MeshBuilder[ms.Length];
            for (int i = 0; i < ms.Length; i++) builders[i] = new MeshBuilder();
            for (int ring = 0; ring < 5; ring++)
            {
                int n = 8 + ring * 4; float r = radius * (1 - ring * .17f);
                for (int i = 0; i < n; i++)
                {
                    float ang = i * Mathf.PI * 2 / n + ring * .2f;
                    builders[ring].Ellipsoid(new Vector3(Mathf.Cos(ang) * r, .012f, Mathf.Sin(ang) * r), new Vector3(.1f - ring * .008f, .008f, .045f), Quaternion.Euler(0, -ang * Mathf.Rad2Deg, 0), 8, 3);
                }
            }
            builders[2].Box(new Vector3(0, .015f, 0), new Vector3(.22f, .02f, .22f), Quaternion.Euler(0, 45, 0));
            for (int i = 0; i < ms.Length; i++) g.Emit("Rangoli powder", root.transform, ms[i], builders[i]);
        }

        public static void Flagstaff(HeritageGeometry g, Transform parent, Vector3 pos)
        {
            var root = g.Group("Dwajastambha flagstaff", parent, pos);
            g.Box("Flagstaff plinth", root.transform, new Vector3(0, .25f, 0), new Vector3(1.5f, .5f, 1.5f), g.Edge);
            g.Box("Flagstaff plinth upper", root.transform, new Vector3(0, .6f, 0), new Vector3(1f, .25f, 1f), g.Stone);
            var brass = new MeshBuilder();
            brass.Lathe(new[] { new Vector2(.14f, .72f), new Vector2(.09f, 1.2f), new Vector2(.08f, 6.2f), new Vector2(.14f, 6.35f), new Vector2(.06f, 6.5f), new Vector2(0, 6.9f) }, 16, Vector3.zero, true);
            for (int i = 0; i < 4; i++) brass.Lathe(new[] { new Vector2(0, 0), new Vector2(.12f, 0), new Vector2(.13f, .03f), new Vector2(.12f, .06f), new Vector2(0, .06f) }, 16, new Vector3(0, 1.4f + i * 1.3f, 0), true);
            for (int i = 0; i < 3; i++) brass.Box(new Vector3(0, 6.1f, 0), new Vector3(.5f, .04f, .06f), Quaternion.Euler(0, i * 60, 0));
            g.Emit("Brass-clad pole", root.transform, g.Brass, brass);
        }

        public static void Wall(HeritageGeometry g, Transform parent, Vector3 centre, Vector3 size)
        {
            var root = g.Group("Compound wall", parent, centre);
            g.Box("Wall body", root.transform, Vector3.zero, size, g.Stone);
            var edge = new MeshBuilder(); var pale = new MeshBuilder();
            edge.Box(new Vector3(0, size.y * .5f + .08f, 0), new Vector3(size.x + .25f, .16f, size.z + .25f));
            edge.Box(new Vector3(0, -size.y * .5f + .15f, 0), new Vector3(size.x + .14f, .3f, size.z + .14f));
            bool along = size.x > size.z; float len = along ? size.x : size.z;
            for (int i = 0; i < (int)(len / 1.4f); i++)
            {
                float t = -len * .5f + .7f + i * 1.4f;
                var p = along ? new Vector3(t, size.y * .5f + .41f, 0) : new Vector3(0, size.y * .5f + .41f, t);
                pale.Box(p, new Vector3(along ? .5f : size.x * .8f, .5f, along ? size.z * .8f : .5f));
                pale.Lathe(new[] { new Vector2(0, 0), new Vector2(.2f, 0), new Vector2(.12f, .16f), new Vector2(0, .2f) }, 8, p + Vector3.up * .25f);
            }
            g.Emit("Wall coping", root.transform, g.Edge, edge);
            g.Emit("Wall merlons", root.transform, g.Pale, pale);
        }

        static void Shrub(MeshBuilder mb, Vector3 pos, float size, System.Random rnd)
        {
            for (int i = 0; i < 6; i++)
                mb.Ellipsoid(pos + new Vector3(R(rnd) - .5f, R(rnd) * .6f + .3f, R(rnd) - .5f) * size, Vector3.one * size * (.35f + R(rnd) * .25f), Quaternion.Euler(0, R(rnd) * 360, 0), 9, 5);
        }

        // Raised stepped tank (kalyani) with water inside a moulded rim.
        static void Tank(HeritageGeometry g, Transform parent, Vector3 c)
        {
            var root = g.Group("Stepped tank", parent, c);
            var floor = new MeshBuilder(); var edge = new MeshBuilder();
            void Ring(MeshBuilder mb, float w, float d, float thick, float top)
            {
                mb.Box(new Vector3(0, top * .5f, -d * .5f + thick * .5f), new Vector3(w, top, thick));
                mb.Box(new Vector3(0, top * .5f, d * .5f - thick * .5f), new Vector3(w, top, thick));
                mb.Box(new Vector3(-w * .5f + thick * .5f, top * .5f, 0), new Vector3(thick, top, d - thick * 2));
                mb.Box(new Vector3(w * .5f - thick * .5f, top * .5f, 0), new Vector3(thick, top, d - thick * 2));
            }
            Ring(floor, 6.4f, 5.4f, .5f, .62f);
            Ring(edge, 6.6f, 5.6f, .2f, .72f);
            Ring(floor, 5.4f, 4.4f, .4f, .46f);
            Ring(floor, 4.6f, 3.6f, .4f, .3f);
            floor.Box(new Vector3(0, .04f, 0), new Vector3(4f, .08f, 3f));
            g.Emit("Tank steps", root.transform, g.Edge, floor, Solid.Mesh);
            g.Emit("Tank rim", root.transform, g.Stone, edge);
            g.Box("Tank water", root.transform, new Vector3(0, .2f, 0), new Vector3(4.6f, .02f, 3.6f), g.Water, false);
            var col = root.AddComponent<BoxCollider>(); col.center = new Vector3(0, .8f, 0); col.size = new Vector3(6.6f, 1.6f, 5.6f);
        }

        public static void Compound(HeritageGeometry g, Transform parent, bool full)
        {
            float h = 3.2f;
            Wall(g, parent, new Vector3(-17.5f, h * .5f, 0), new Vector3(.9f, h, 46)); Wall(g, parent, new Vector3(17.5f, h * .5f, 0), new Vector3(.9f, h, 46));
            Wall(g, parent, new Vector3(0, h * .5f, 23), new Vector3(35, h, .9f));
            Wall(g, parent, new Vector3(-10.4f, h * .5f, -23), new Vector3(14.4f, h, .9f)); Wall(g, parent, new Vector3(10.4f, h * .5f, -23), new Vector3(14.4f, h, .9f));
            for (int s = -1; s <= 1; s += 2)
            {
                g.Box("Gate jamb", parent, new Vector3(s * 3.2f, 2.1f, -23), new Vector3(1.6f, 4.2f, 1.6f), g.Stone);
                g.Box("Gate jamb cap", parent, new Vector3(s * 3.2f, 4.35f, -23), new Vector3(2f, .3f, 2f), g.Edge, false);
                var door = g.Box("Teak door leaf", parent, new Vector3(s * 2.55f, 1.8f, -22.1f), new Vector3(.18f, 3.4f, 1.9f), g.Wood, false);
                door.transform.localRotation = Quaternion.Euler(0, s * -75, 0);
                for (int k = 0; k < 4; k++) g.Box("Brass boss", door.transform, new Vector3(.6f, -.1f + k * .1f, 0), new Vector3(.4f, .04f, .5f), g.Brass, false);
                Lamp(g, parent, new Vector3(s * 4.6f, 0, -21.8f), false);
            }
            var gate = new MeshBuilder(); var brass = new MeshBuilder(); var red = new MeshBuilder();
            gate.Box(new Vector3(0, 4.35f, -23), new Vector3(8.2f, .5f, 1.8f));
            for (int i = 0; i < 9; i++) (i % 2 == 0 ? brass : red).Box(new Vector3(-3.6f + i * .9f, 4.35f, -23.95f), new Vector3(.4f, .4f, .12f), Quaternion.Euler(0, 0, 45));
            gate.Box(new Vector3(0, 5.95f, -23), new Vector3(7.2f, .25f, 2f));
            for (int i = 0; i < 5; i++) brass.Lathe(new[] { new Vector2(0, 0), new Vector2(.2f, 0), new Vector2(.22f, .2f), new Vector2(.12f, .45f), new Vector2(.04f, .7f), new Vector2(0, .85f) }, 12, new Vector3(-2.8f + i * 1.4f, 6.08f, -23), true);
            g.Emit("Gate lintel", parent, g.Edge, gate);
            g.Emit("Gate brass", parent, g.Brass, brass);
            g.Emit("Gate kumkum", parent, g.Kumkum, red);
            g.Box("Gate upper register", parent, new Vector3(0, 5.2f, -23), new Vector3(6.6f, 1.2f, 1.5f), g.Pale);
            Garland(g, parent, new Vector3(-3.5f, 4.1f, -24.1f), new Vector3(3.5f, 4.1f, -24.1f), .9f, 30);
            Rangoli(g, parent, new Vector3(0, .02f, -19.5f), 1.4f);
            for (int i = 0; i < 4; i++) for (int s = -1; s <= 1; s += 2) Lamp(g, parent, new Vector3(s * 3.1f, 0, -17 + i * 4.6f), i % 2 == 0 && full, 1.4f);
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 6; i++) Palm(g, parent, new Vector3(s * 14.5f, 0, -18 + i * 7f), 6.5f + (i % 3) * .8f, new System.Random(i * 7 + s));
            var shrubs = new MeshBuilder(); var shrubRandom = new System.Random(5);
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 7; i++) Shrub(shrubs, new Vector3(s * 16.2f, 0, -20 + i * 6.4f), .9f + R(shrubRandom) * .5f, shrubRandom);
            for (int i = 0; i < 6; i++) Shrub(shrubs, new Vector3(-14 + i * 5.6f, 0, 21.6f), 1f, shrubRandom);
            g.Emit("Courtyard shrubs", parent, g.Foliage, shrubs);
            Tank(g, parent, new Vector3(-10.8f, 0, 13.5f));
            Motes(parent, new Vector3(0, 3.5f, 4), new Vector3(24, 5, 34), new Color(1f, .88f, .6f, .55f), 70, .09f);
            Motes(parent, new Vector3(0, 6.5f, 0), new Vector3(24, 1, 30), new Color(1f, .5f, .15f, .8f), 24, .14f);
            if (full) Flagstaff(g, parent, new Vector3(0, 0, -12.5f));
        }

        // Lamp, garland and incense before a closed sanctum door. No sacred image is depicted.
        public static void Sanctum(HeritageGeometry g, Transform parent, Vector3 door)
        {
            Lamp(g, parent, door, true, 2.4f);
            Garland(g, parent, door + new Vector3(-.75f, 2.3f, .45f), door + new Vector3(.75f, 2.3f, .45f), .35f, 12);
            Smoke(parent, door + new Vector3(1.1f, .3f, -.2f), new Color(.85f, .82f, .78f, .12f), .25f, 1.8f, .6f);
            var fill = new GameObject("Hall fill light"); fill.transform.SetParent(parent, false); fill.transform.localPosition = door + new Vector3(0, 2.2f, -2.2f);
            var l = fill.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, .78f, .52f); l.range = 6.5f; l.intensity = 1.1f; l.shadows = LightShadows.None;
        }

        public static void Lighting(Light sun)
        {
            sun.transform.rotation = Quaternion.Euler(30, -38, 0); sun.color = new Color(1f, .8f, .58f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.5f, .58f, .7f); RenderSettings.ambientEquatorColor = new Color(.5f, .42f, .34f); RenderSettings.ambientGroundColor = new Color(.22f, .18f, .13f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .0085f; RenderSettings.fogColor = new Color(.84f, .68f, .5f);
        }

        public static void PostProcessing(Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.None;
            var go = new GameObject("Cinematic grade"); var vol = go.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 5;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); vol.profile = profile;
            var bloom = profile.Add<Bloom>(true); bloom.threshold.Override(1.0f); bloom.intensity.Override(.6f); bloom.scatter.Override(.68f); bloom.tint.Override(new Color(1f, .85f, .65f));
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(.22f); vig.smoothness.Override(.6f);
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var grade = profile.Add<ColorAdjustments>(true); grade.postExposure.Override(.3f); grade.contrast.Override(12); grade.saturation.Override(10); grade.colorFilter.Override(new Color(1f, .97f, .92f));
            var wb = profile.Add<WhiteBalance>(true); wb.temperature.Override(10);
            var smh = profile.Add<ShadowsMidtonesHighlights>(true); smh.shadows.Override(new Vector4(.95f, .98f, 1.06f, 0)); smh.highlights.Override(new Vector4(1.04f, 1.0f, .94f, 0));
        }
    }
}
