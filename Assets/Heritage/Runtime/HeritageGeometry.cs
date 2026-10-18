using System.Collections.Generic;
using UnityEngine;

namespace VirtualHeritage
{
    public enum Solid { None, Mesh, Box }

    // All geometry is original and deliberately schematic, not an archaeological model.
    public sealed class HeritageGeometry
    {
        public readonly Material Stone, Edge, Dark, Recess, Ground, Foliage, Blade, Brass, Teal, Pale, Slab, Granite, Marigold, Saffron, Kumkum, Flame, Bark, Water, Grass, Wood;
        public readonly List<Material> OwnedMaterials = new List<Material>();
        // Sanctum doorways (position on the floor, facing direction) for lamps and garlands.
        public readonly List<Vector3> Doors = new List<Vector3>();
        readonly List<Mesh> meshes = new List<Mesh>();
        const float Jagati = .72f;

        public HeritageGeometry()
        {
            Stone = Material("Carved stone", new Color(.5f, .45f, .38f), 0, Pattern.Ashlar, 52, .14f, 1f);
            Edge = Material("Cut mouldings", new Color(.6f, .55f, .47f), 0, Pattern.Fine, 11, .2f, .8f);
            Dark = Material("Polished basalt", new Color(.1f, .09f, .08f), 0, Pattern.Fine, 11, .45f, .5f);
            Recess = Material("Shaded recess", new Color(.3f, .27f, .23f), 0, Pattern.Fine, 11, .08f, .6f);
            Ground = Material("Courtyard paving", new Color(.72f, .63f, .5f), 0, Pattern.Paving, 7, .1f, 1f);
            Slab = Material("Processional path", new Color(.5f, .42f, .34f), 0, Pattern.Paving, 9, .16f, 1f);
            Granite = Material("Granite", new Color(.58f, .5f, .43f), 0, Pattern.Granite, 31, .12f, 1.2f);
            Foliage = Material("Palm foliage", new Color(.2f, .34f, .12f), 0, Pattern.Leaf, 77, .25f, .5f, true);
            Blade = Material("Grass blades", new Color(.36f, .45f, .16f), 0, Pattern.Leaf, 77, .1f, .3f, true);
            Grass = Material("Turf", new Color(.42f, .47f, .2f), 0, Pattern.Turf, 81, 0, 1f);
            Bark = Material("Palm bark", new Color(.36f, .29f, .21f), 0, Pattern.Grain, 41, .05f, 1.4f);
            Brass = Material("Brass details", new Color(.86f, .62f, .26f), .9f, Pattern.Fine, 11, .66f, .3f);
            Teal = Material("Wayfinding jade", new Color(.18f, .72f, .62f), .2f, Pattern.Fine, 11, .45f, .2f);
            Pale = Material("Lime plaster", new Color(.86f, .81f, .69f), 0, Pattern.Plaster, 23, .06f, .7f);
            Wood = Material("Teak", new Color(.32f, .19f, .1f), 0, Pattern.Grain, 41, .3f, 1f);
            Marigold = Material("Marigold", new Color(.98f, .55f, .06f), 0, Pattern.Leaf, 77, .2f, .6f);
            Saffron = Material("Saffron cloth", new Color(.88f, .36f, .06f), 0, Pattern.Fine, 11, .1f, .3f);
            Kumkum = Material("Kumkum red", new Color(.62f, .07f, .05f), 0, Pattern.Fine, 11, .12f, .3f);
            Water = Material("Tank water", new Color(.07f, .16f, .17f), 0, Pattern.Fine, 11, .95f, .25f);
            Flame = new Material(FindShader("Universal Render Pipeline/Unlit")) { name = "Lamp flame" };
            Flame.color = new Color(3.2f, 1.6f, .4f); Flame.SetColor("_BaseColor", new Color(3.2f, 1.6f, .4f)); OwnedMaterials.Add(Flame);
        }

        public static Shader FindShader(string name)
        {
            var shader = Shader.Find(name);
            if (!shader) { Debug.LogWarning("HERITAGE: shader missing, using Lit: " + name); shader = Shader.Find("Universal Render Pipeline/Lit"); }
            return shader;
        }

        // ---------- Procedural surface maps (cached for the whole session) ----------

        enum Pattern { Ashlar, Paving, Fine, Granite, Grain, Leaf, Turf, Plaster }
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        public static void ReleaseTextures()
        {
            foreach (var tex in textures.Values) if (tex) Object.Destroy(tex);
            textures.Clear();
        }

        static float Fbm(float x, float y, int seed, int octaves)
        {
            float sum = 0, amp = .5f, norm = 0;
            for (int i = 0; i < octaves; i++) { sum += amp * Mathf.PerlinNoise(x + seed * 17.13f, y + seed * 9.71f); norm += amp; x *= 2.03f; y *= 2.03f; amp *= .5f; }
            return sum / norm;
        }

        // Blends four offset samples so noise wraps seamlessly across texture repeats.
        static float Tile(float u, float v, float fx, float fy, int seed, int octaves)
        {
            float a = Fbm(u * fx, v * fy, seed, octaves), b = Fbm((u - 1) * fx, v * fy, seed, octaves);
            float c = Fbm(u * fx, (v - 1) * fy, seed, octaves), d = Fbm((u - 1) * fx, (v - 1) * fy, seed, octaves);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        static void Maps(Pattern pattern, int seed, out Texture2D albedo, out Texture2D normal)
        {
            string key = pattern + "/" + seed;
            if (textures.TryGetValue(key, out albedo) && textures.TryGetValue(key + "/n", out normal)) return;
            int size = pattern == Pattern.Ashlar || pattern == Pattern.Paving || pattern == Pattern.Granite || pattern == Pattern.Turf ? 512 : 256;
#if UNITY_WEBGL
            size /= 2; // Browser builds generate on a single thread; smaller maps keep the first load quick.
#endif
            int cols = pattern == Pattern.Ashlar ? 4 : 2, rows = pattern == Pattern.Ashlar ? 3 : 2;
            bool blocks = pattern == Pattern.Ashlar || pattern == Pattern.Paving;
            var rnd = new System.Random(seed); var blockTone = new float[cols * rows];
            for (int i = 0; i < blockTone.Length; i++) blockTone[i] = (float)rnd.NextDouble();
            var height = new float[size * size]; var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size, h, tone;
                    float broad = Tile(u, v, 5, 5, seed, 4), fine = Tile(u, v, 26, 26, seed + 3, 3);
                    switch (pattern)
                    {
                        case Pattern.Ashlar:
                        case Pattern.Paving:
                        {
                            float fv = v * rows; int row = Mathf.FloorToInt(fv);
                            float uu = u * cols + (pattern == Pattern.Ashlar && row % 2 == 1 ? .5f : 0);
                            int col = ((Mathf.FloorToInt(uu) % cols) + cols) % cols; float fu = uu - Mathf.Floor(uu), fr = fv - row;
                            float edge = Mathf.Min(Mathf.Min(fu, 1 - fu) * cols / (float)rows, Mathf.Min(fr, 1 - fr)) + (fine - .5f) * .025f;
                            float joint = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.018f, .06f, edge));
                            float chip = Mathf.Clamp01((Tile(u, v, 60, 60, seed + 9, 2) - .62f) * 4) * .35f;
                            h = joint * (.72f + broad * .28f - chip) + fine * .08f;
                            tone = (.56f + (blockTone[(row % rows) * cols + col] - .5f) * .22f + (broad - .5f) * .34f + (fine - .5f) * .14f) * Mathf.Lerp(.42f, 1, joint) * (1 - chip * .4f);
                            break;
                        }
                        case Pattern.Granite:
                        {
                            float speck = Tile(u, v, 90, 90, seed + 5, 1);
                            h = broad * .5f + fine * .35f + speck * .15f;
                            tone = .6f + (broad - .5f) * .35f + (fine - .5f) * .12f + (speck > .66f ? -.22f : speck < .34f ? .1f : 0);
                            break;
                        }
                        case Pattern.Grain:
                        {
                            float streak = Tile(u, v, 48, 3, seed + 2, 3);
                            h = streak * .8f + fine * .2f;
                            tone = .58f + (streak - .5f) * .55f + (broad - .5f) * .2f;
                            break;
                        }
                        case Pattern.Turf:
                            h = fine * .6f + broad * .4f;
                            tone = .52f + (broad - .5f) * .7f + (fine - .5f) * .35f;
                            break;
                        case Pattern.Plaster:
                        {
                            float stain = Mathf.Clamp01((Tile(u, v, 3, 9, seed + 4, 3) - .55f) * 3);
                            h = broad * .35f + fine * .2f;
                            tone = .86f + (broad - .5f) * .12f + (fine - .5f) * .06f - stain * .14f;
                            break;
                        }
                        case Pattern.Leaf:
                            h = fine; tone = .62f + (broad - .5f) * .45f + (fine - .5f) * .2f;
                            break;
                        default:
                            h = broad * .6f + fine * .4f; tone = .78f + (broad - .5f) * .14f + (fine - .5f) * .08f;
                            break;
                    }
                    height[y * size + x] = h;
                    tone = Mathf.Clamp01(tone);
                    px[y * size + x] = new Color(tone * 1.02f, tone, tone * .96f, 1);
                }
            albedo = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = key, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            albedo.SetPixels(px); albedo.Apply(true);
            float strength = (blocks ? 3f : pattern == Pattern.Granite ? 2f : 1.4f) * size / 64f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = height[y * size + (x + 1) % size] - height[y * size + (x + size - 1) % size];
                    float dy = height[((y + 1) % size) * size + x] - height[((y + size - 1) % size) * size + x];
                    var nrm = new Vector3(-dx * strength * .5f, -dy * strength * .5f, 1).normalized;
                    px[y * size + x] = new Color(nrm.x * .5f + .5f, nrm.y * .5f + .5f, nrm.z * .5f + .5f, 1);
                }
            normal = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = key + " normal", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            normal.SetPixels(px); normal.Apply(true);
            textures[key] = albedo; textures[key + "/n"] = normal;
        }

        Material Material(string name, Color color, float metallic, Pattern pattern, int seed, float smooth, float bump, bool doubleSided = false)
        {
            Maps(pattern, seed, out var albedo, out var normal);
            var m = new Material(FindShader("Universal Render Pipeline/Lit")) { name = name, color = color };
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smooth);
            m.SetTexture("_BaseMap", albedo);
            m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP");
            if (doubleSided) m.SetFloat("_Cull", 0);
            OwnedMaterials.Add(m); return m;
        }

        // ---------- Scene object helpers ----------

        public GameObject Group(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position; return go;
        }

        readonly Dictionary<Vector3, Mesh> boxMeshes = new Dictionary<Vector3, Mesh>();
        static Mesh cubeSource;

        Mesh TiledCube(Vector3 size)
        {
            var key = new Vector3(Mathf.Round(size.x * 100), Mathf.Round(size.y * 100), Mathf.Round(size.z * 100));
            if (boxMeshes.TryGetValue(key, out var found)) return found;
            if (!cubeSource) { var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube); cubeSource = tmp.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(tmp); }
            var mesh = Object.Instantiate(cubeSource); mesh.name = "Tiled cube";
            var n = mesh.normals; var uv = mesh.uv; const float texelsPerMetre = .5f;
            for (int i = 0; i < uv.Length; i++)
            {
                var a = new Vector3(Mathf.Abs(n[i].x), Mathf.Abs(n[i].y), Mathf.Abs(n[i].z));
                Vector2 dims = a.x > .5f ? new Vector2(size.z, size.y) : a.y > .5f ? new Vector2(size.x, size.z) : new Vector2(size.x, size.y);
                uv[i] = new Vector2(uv[i].x * dims.x, uv[i].y * dims.y) * texelsPerMetre;
            }
            mesh.uv = uv; mesh.RecalculateTangents(); meshes.Add(mesh); boxMeshes[key] = mesh; return mesh;
        }

        public GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.GetComponent<MeshFilter>().sharedMesh = TiledCube(size);
            go.transform.SetParent(parent, false); go.transform.localPosition = position;
            // Mesh carries the size in its UVs; scale stays the authored size so colliders and inspection bounds are unchanged.
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) { go.GetComponent<Collider>().enabled = false; Object.Destroy(go.GetComponent<Collider>()); }
            return go;
        }

        public GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat)
        {
            var go = Group(name, parent, Vector3.zero);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        public GameObject Emit(string name, Transform parent, Material mat, MeshBuilder mb, Solid solid = Solid.None)
        {
            if (mb.VertexCount == 0) return null;
            var mesh = mb.ToMesh(name); meshes.Add(mesh);
            var go = MeshObject(name, parent, mesh, mat);
            if (solid == Solid.Mesh) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            else if (solid == Solid.Box) { var box = go.AddComponent<BoxCollider>(); box.center = mesh.bounds.center; box.size = mesh.bounds.size; }
            return go;
        }

        public Mesh Lathe(Vector2[] profile, int sides = 32)
        {
            var mb = new MeshBuilder(); mb.Lathe(profile, sides, Vector3.zero);
            var mesh = mb.ToMesh("Lathed profile"); meshes.Add(mesh); return mesh;
        }

        public Mesh Track(Mesh mesh) { meshes.Add(mesh); return mesh; }

        // One builder per material; collision is authored separately as simple boxes.
        public sealed class Kit
        {
            public readonly MeshBuilder Floor = new MeshBuilder(), Stone = new MeshBuilder(), Edge = new MeshBuilder(), Recess = new MeshBuilder(),
                Pale = new MeshBuilder(), Brass = new MeshBuilder(), Wood = new MeshBuilder();
            public readonly List<Bounds> Blocks = new List<Bounds>();
            public void Block(Vector3 centre, Vector3 size) { Blocks.Add(new Bounds(centre, size)); }
        }

        public void Emit(Kit k, Transform parent, string name)
        {
            Emit(name + " platform", parent, Stone, k.Floor, Solid.Mesh);
            Emit(name + " stonework", parent, Stone, k.Stone);
            Emit(name + " mouldings", parent, Edge, k.Edge);
            Emit(name + " recesses", parent, Recess, k.Recess);
            Emit(name + " plasterwork", parent, Pale, k.Pale);
            Emit(name + " brass", parent, Brass, k.Brass);
            Emit(name + " teak", parent, Wood, k.Wood);
            if (k.Blocks.Count == 0) return;
            var collision = Group(name + " collision", parent, Vector3.zero);
            foreach (var b in k.Blocks) { var box = collision.AddComponent<BoxCollider>(); box.center = b.center; box.size = b.size; }
        }

        // ---------- Carved vocabulary ----------

        static readonly Vector2[] TurnedProfile = {
            new Vector2(0,0), new Vector2(.40f,0), new Vector2(.40f,.13f), new Vector2(.3f,.17f), new Vector2(.3f,.30f), new Vector2(.37f,.33f),
            new Vector2(.39f,.40f), new Vector2(.37f,.46f), new Vector2(.26f,.51f), new Vector2(.24f,.74f), new Vector2(.28f,.78f), new Vector2(.30f,.84f),
            new Vector2(.28f,.89f), new Vector2(.22f,.93f), new Vector2(.2f,1.2f), new Vector2(.19f,1.55f), new Vector2(.23f,1.6f), new Vector2(.25f,1.66f),
            new Vector2(.23f,1.71f), new Vector2(.27f,1.76f), new Vector2(.33f,1.82f), new Vector2(.35f,1.9f), new Vector2(.33f,1.98f), new Vector2(.27f,2.03f),
            new Vector2(.27f,2.1f), new Vector2(.36f,2.15f), new Vector2(.41f,2.22f), new Vector2(.36f,2.3f), new Vector2(.27f,2.35f), new Vector2(.25f,2.5f),
            new Vector2(.33f,2.56f), new Vector2(.44f,2.68f), new Vector2(.49f,2.78f), new Vector2(.49f,2.87f), new Vector2(0,2.87f) };

        // Lathe-turned Hoysala column: square base, turned shaft, abacus and four angled brackets.
        public void Turned(Kit k, Vector3 basePos, float height, float girth = 1, bool brackets = true, int sides = 32)
        {
            float shaft = height - .5f, s = shaft / 2.87f;
            k.Edge.Box(basePos + new Vector3(0, .14f, 0), new Vector3(.95f, .28f, .95f) * girth);
            k.Edge.Box(basePos + new Vector3(0, .3f, 0), new Vector3(.8f, .06f, .8f) * girth);
            k.Stone.Lathe(TurnedProfile, sides, basePos + Vector3.up * .33f, Quaternion.identity, new Vector3(girth, s, girth), false);
            float top = .33f + shaft;
            k.Edge.Box(basePos + new Vector3(0, top + .07f, 0), new Vector3(1.05f, .14f, 1.05f) * girth);
            if (!brackets) return;
            for (int i = 0; i < 4; i++)
            {
                var dir = Quaternion.Euler(0, i * 90, 0);
                k.Stone.Box(basePos + Vector3.up * (top - .1f) + dir * new Vector3(0, 0, .42f * girth), new Vector3(.18f, .5f, .12f) * girth, dir * Quaternion.Euler(38, 0, 0));
            }
        }

        // Vijayanagara composite pier: square blocks with recessed panels, octagonal necks, corbel with pendant buds.
        public void Composite(Kit k, Vector3 basePos, float height, float girth = 1)
        {
            float g = girth, unit = (height - .55f) / 5f;
            k.Edge.Box(basePos + Vector3.up * .14f, new Vector3(.7f, .28f, .7f) * g);
            for (int i = 0; i < 5; i++)
            {
                float y = .28f + i * unit;
                if (i % 2 == 0)
                {
                    k.Stone.Box(basePos + Vector3.up * (y + unit * .5f), new Vector3(.5f * g, unit, .5f * g));
                    for (int f = 0; f < 4; f++)
                    {
                        var dir = Quaternion.Euler(0, f * 90, 0);
                        k.Recess.Box(basePos + Vector3.up * (y + unit * .5f) + dir * new Vector3(0, 0, .251f * g), new Vector3(.3f * g, unit * .66f, .02f), dir);
                        k.Edge.Ellipsoid(basePos + Vector3.up * (y + unit * .5f) + dir * new Vector3(0, 0, .27f * g), new Vector3(.08f, .1f, .03f) * g * Mathf.Max(1, unit * 1.6f), dir, 8, 4);
                    }
                }
                else k.Stone.Lathe(new[] { new Vector2(.27f, 0), new Vector2(.2f, .1f), new Vector2(.2f, .9f), new Vector2(.27f, 1) }, 8, basePos + Vector3.up * y, Quaternion.Euler(0, 22.5f, 0), new Vector3(g, unit, g));
            }
            float top = .28f + unit * 5;
            k.Edge.Box(basePos + Vector3.up * (top + .06f), new Vector3(.62f, .12f, .62f) * g);
            for (int i = 0; i < 4; i++)
            {
                var dir = Quaternion.Euler(0, i * 90, 0);
                k.Edge.Box(basePos + Vector3.up * (top + .2f) + dir * new Vector3(0, 0, .42f * g), new Vector3(.3f, .16f, .55f) * g, dir);
                k.Stone.Lathe(new[] { new Vector2(0, 0), new Vector2(.06f, .04f), new Vector2(.08f, .12f), new Vector2(.04f, .2f), new Vector2(0, .22f) }, 10, basePos + Vector3.up * (top - .1f) + dir * new Vector3(0, 0, .62f * g), Quaternion.identity, Vector3.one * g);
            }
        }

        // Abstract elephant for the procession friezes; faces +Z, stands on y = 0, about 0.9 units tall before scaling.
        public static void Elephant(MeshBuilder mb, Vector3 pos, Quaternion rot, float scale)
        {
            mb.Push(pos, rot, scale);
            mb.Ellipsoid(new Vector3(0, .56f, 0), new Vector3(.32f, .3f, .48f), Quaternion.identity, 10, 6);
            mb.Ellipsoid(new Vector3(0, .7f, .48f), new Vector3(.24f, .25f, .22f), Quaternion.identity, 10, 6);
            for (int s = -1; s <= 1; s += 2) mb.Ellipsoid(new Vector3(s * .22f, .7f, .4f), new Vector3(.05f, .2f, .15f), Quaternion.identity, 6, 4);
            mb.Lathe(new[] { new Vector2(0, 0), new Vector2(.045f, 0), new Vector2(.075f, .5f), new Vector2(0, .5f) }, 6, new Vector3(0, .12f, .66f));
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2) mb.Lathe(new[] { new Vector2(0, 0), new Vector2(.09f, 0), new Vector2(.09f, .42f), new Vector2(0, .42f) }, 6, new Vector3(x * .17f, 0, z * .27f));
            mb.Pop();
        }

        // Horizontal relief registers around a stellate outline: elephants, a scrolling vine or lotus petals.
        public enum Register { Elephants, Scroll, Lotus }

        public static void StarRegister(MeshBuilder mb, Vector3 c, float outer, float inner, int points, Register kind, float scale, System.Func<Vector3, bool> skip = null)
        {
            float spacing = kind == Register.Elephants ? 1.35f * scale : kind == Register.Scroll ? .36f * scale : .3f * scale;
            int index = 0;
            MeshBuilder.AlongStar(c, outer, inner, points, spacing, (p, tangent, outward) =>
            {
                index++;
                if (skip != null && skip(p)) return;
                Motif(mb, p, tangent, outward, kind, scale, index);
            });
        }

        public static void Motif(MeshBuilder mb, Vector3 p, Vector3 tangent, Vector3 outward, Register kind, float scale, int index)
        {
            switch (kind)
            {
                case Register.Elephants:
                    Elephant(mb, p + outward * .3f * scale, Quaternion.LookRotation(tangent, Vector3.up), scale);
                    break;
                case Register.Scroll:
                    float wave = Mathf.Sin(index * 1.3f);
                    mb.Ellipsoid(p + outward * .05f * scale + Vector3.up * (.5f + wave * .22f) * scale, new Vector3(.14f, .14f, .14f) * scale, Quaternion.LookRotation(outward), 8, 4);
                    mb.Ellipsoid(p + outward * .03f * scale + Vector3.up * (.5f - wave * .28f) * scale, new Vector3(.16f, .08f, .05f) * scale, Quaternion.LookRotation(outward) * Quaternion.Euler(0, 0, 35 * wave), 6, 4);
                    break;
                default:
                    mb.Ellipsoid(p + outward * .04f * scale + Vector3.up * .45f * scale, new Vector3(.13f, .36f, .06f) * scale, Quaternion.LookRotation(outward) * Quaternion.Euler(-24, 0, 0), 8, 4);
                    break;
            }
        }

        // Miniature shrine (kuta) used on parapets and eaves.
        public static void Kuta(Kit k, Vector3 basePos, float s)
        {
            k.Stone.Box(basePos + Vector3.up * .18f * s, new Vector3(.42f, .36f, .42f) * s);
            k.Edge.Slab(basePos + Vector3.up * .36f * s, new Vector2(.58f, .58f) * s, new Vector2(.36f, .36f) * s, .1f * s);
            k.Stone.Lathe(new[] { new Vector2(0, 0), new Vector2(.2f, 0), new Vector2(.22f, .08f), new Vector2(.16f, .2f), new Vector2(0, .26f) }, 8, basePos + Vector3.up * .46f * s, Quaternion.Euler(0, 22.5f, 0), Vector3.one * s);
            k.Brass.Lathe(new[] { new Vector2(0, 0), new Vector2(.04f, 0), new Vector2(.05f, .06f), new Vector2(0, .14f) }, 8, basePos + Vector3.up * .72f * s, Quaternion.identity, Vector3.one * s);
        }

        // Pierced stone screen (jali) in the XY plane of rot, centred on c.
        public static void Jali(MeshBuilder mb, Vector3 c, float w, float h, Quaternion rot)
        {
            mb.Push(c, rot);
            const float bar = .07f, depth = .1f, cell = .21f;
            mb.Box(new Vector3(0, h * .5f - bar * .5f, 0), new Vector3(w, bar, depth));
            mb.Box(new Vector3(0, -h * .5f + bar * .5f, 0), new Vector3(w, bar, depth));
            mb.Box(new Vector3(-w * .5f + bar * .5f, 0, 0), new Vector3(bar, h, depth));
            mb.Box(new Vector3(w * .5f - bar * .5f, 0, 0), new Vector3(bar, h, depth));
            int nx = Mathf.Max(1, Mathf.RoundToInt((w - bar) / cell)), ny = Mathf.Max(1, Mathf.RoundToInt((h - bar) / cell));
            for (int i = 1; i < nx; i++) mb.Box(new Vector3(-w * .5f + i * w / nx, 0, 0), new Vector3(.04f, h - bar, depth * .8f));
            for (int j = 1; j < ny; j++) mb.Box(new Vector3(0, -h * .5f + j * h / ny, 0), new Vector3(w - bar, .04f, depth * .8f));
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                    if ((i + j) % 2 == 0) mb.Lathe(new[] { new Vector2(0, 0), new Vector2(.05f, 0), new Vector2(.05f, .06f), new Vector2(0, .06f) }, 8, new Vector3(-w * .5f + (i + .5f) * w / nx, -h * .5f + (j + .5f) * h / ny, -.03f), Quaternion.Euler(90, 0, 0), Vector3.one);
            mb.Pop();
        }

        // Carved sanctum doorway with closed teak leaves, built proud of a wall face.
        // bottom is on the floor at the wall face; outward points away from the wall.
        public void Doorway(Kit k, Vector3 bottom, Vector3 outward, float width = 1.1f, float height = 2.1f)
        {
            var rot = Quaternion.LookRotation(outward, Vector3.up);
            Vector3 L(float x, float y, float z) => bottom + rot * new Vector3(x, y, z);
            k.Edge.Box(L(0, .06f, .25f), new Vector3(width + .9f, .12f, .5f), rot);
            for (int band = 0; band < 3; band++)
            {
                float off = width * .5f + .07f + band * .14f, depth = .26f - band * .06f, tall = height + .12f + band * .14f;
                var mat = band == 1 ? k.Stone : k.Edge;
                for (int s = -1; s <= 1; s += 2) mat.Box(L(s * off, tall * .5f, depth * .5f), new Vector3(.14f, tall, depth), rot);
                mat.Box(L(0, tall + .07f, depth * .5f), new Vector3(2 * off + .14f, .14f, depth), rot);
            }
            for (int i = 0; i < 7; i++) k.Edge.Ellipsoid(L(-width * .45f + i * width * .15f, height + .72f, .22f), new Vector3(.06f, .08f, .04f), rot, 8, 4);
            for (int s = -1; s <= 1; s += 2)
            {
                k.Wood.Box(L(s * width * .25f, .12f + height * .5f, .05f), new Vector3(width * .5f - .02f, height, .1f), rot);
                for (int r = 0; r < 6; r++)
                    for (int c = 0; c < 2; c++) k.Brass.Ellipsoid(L(s * (width * .12f + c * width * .22f), .35f + r * height * .16f, .105f), new Vector3(.03f, .03f, .02f), rot, 6, 3);
                k.Brass.Lathe(new[] { new Vector2(.04f, 0), new Vector2(.06f, .015f), new Vector2(.04f, .03f) }, 10, L(s * .08f, .12f + height * .5f, .1f), rot * Quaternion.Euler(90, 0, 0), Vector3.one);
            }
            Doors.Add(bottom + outward * .7f);
        }

        // ---------- Hoysala shrine ----------

        // Stellate jagati platform with an elephant register on its middle tier.
        void Platform(Kit k, Vector3 c, float r, bool detail, System.Func<Vector3, bool> skip = null)
        {
            k.Floor.Star(c, r, r * .8f, 16, .26f);
            k.Floor.Star(c + Vector3.up * .26f, r - .3f, (r - .3f) * .8f, 16, .3f);
            k.Floor.Star(c + Vector3.up * .56f, r - .55f, (r - .55f) * .8f, 16, .16f);
            k.Edge.Star(c + Vector3.up * .22f, r + .05f, (r + .05f) * .8f, 16, .06f);
            k.Edge.Star(c + Vector3.up * .52f, r - .25f, (r - .25f) * .8f, 16, .05f);
            k.Edge.Star(c + Vector3.up * .68f, r - .5f, (r - .5f) * .8f, 16, .05f);
            if (detail) StarRegister(k.Stone, c + Vector3.up * .27f, r - .3f, (r - .3f) * .8f, 16, Register.Elephants, .27f, skip);
        }

        // Garbhagriha wall on a stellate plan: moulded base with friezes, ribbed wall, eave. The tower goes to tower kit.
        public void Vimana(Kit k, Vector3 c, float R, Kit tower, bool detail)
        {
            float u = R / 2.9f; float y = 0;
            void Band(MeshBuilder mb, float rScale, float h, Register? register = null)
            {
                mb.Star(c + Vector3.up * y, R * rScale, R * rScale * .8f, 16, h);
                if (register.HasValue && detail) StarRegister(k.Stone, c + Vector3.up * (y + .02f * u), R * rScale, R * rScale * .8f, 16, register.Value, h / .95f);
                y += h;
            }
            Band(k.Floor, 1.07f, .16f * u);
            Band(k.Edge, 1.0f, .1f * u);
            Band(k.Floor, .97f, .3f * u, Register.Elephants);
            Band(k.Edge, 1.04f, .08f * u);
            Band(k.Floor, .97f, .22f * u, Register.Scroll);
            Band(k.Edge, 1.05f, .1f * u);
            Band(k.Floor, .95f, .16f * u, Register.Lotus);
            float wallBase = y, wallH = 1.85f * u;
            k.Floor.Star(c + Vector3.up * y, R * .92f, R * .92f * .8f, 16, wallH);
            // Each projecting point carries a miniature pilaster tower.
            for (int i = 0; i < 16; i++)
            {
                float a = -Mathf.PI * .5f + i * Mathf.PI * 2 / 16; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var rot = Quaternion.LookRotation(dir); var p = c + dir * R * .93f;
                k.Edge.Box(p + Vector3.up * (wallBase + wallH * .42f), new Vector3(.2f, wallH * .8f, .1f) * 1, rot);
                k.Edge.Box(p + Vector3.up * (wallBase + wallH * .86f), new Vector3(.32f, .08f * u + .02f, .16f), rot);
                k.Stone.Lathe(new[] { new Vector2(0, 0), new Vector2(.13f, 0), new Vector2(.14f, .07f), new Vector2(.08f, .18f), new Vector2(0, .22f) }, 8, p + dir * .02f + Vector3.up * (wallBase + wallH * .9f), Quaternion.identity, Vector3.one * Mathf.Max(.4f, u));
            }
            y += wallH;
            k.Edge.Star(c + Vector3.up * y, R * 1.16f, R * 1.16f * .8f, 16, .28f * u, .82f, true);
            y += .28f * u;
            if (tower == null) return;
            tower.Stone.Star(c + Vector3.up * y, R * .88f, R * .88f * .8f, 16, .26f * u);
            y += .26f * u;
            for (int i = 0; i < 16; i += 2)
            {
                float a = -Mathf.PI * .5f + i * Mathf.PI * 2 / 16; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Kuta(tower, c + dir * R * .82f + Vector3.up * y, .55f * u);
            }
            float r = R * .8f;
            for (int tier = 0; tier < 6; tier++)
            {
                tower.Stone.Star(c + Vector3.up * y, r, r * .78f, 16, .48f * u, .94f);
                tower.Edge.Star(c + Vector3.up * (y + .48f * u), r * 1.02f, r * 1.02f * .78f, 16, .1f * u);
                y += .58f * u; r *= .86f;
            }
            tower.Stone.Lathe(new[] { new Vector2(0, 0), new Vector2(r * 1.25f, 0), new Vector2(r * 1.35f, .2f), new Vector2(r * 1.2f, .55f), new Vector2(r * .7f, .9f), new Vector2(0, 1.02f) }, 16, c + Vector3.up * y, Quaternion.identity, new Vector3(1, u, 1), true);
            y += 1.02f * u;
            tower.Brass.Lathe(new[] { new Vector2(0, 0), new Vector2(.22f, 0), new Vector2(.28f, .2f), new Vector2(.14f, .42f), new Vector2(.06f, .7f), new Vector2(.1f, .78f), new Vector2(0, 1.05f) }, 16, c + Vector3.up * y, Quaternion.identity, Vector3.one * u, true);
        }

        // Closed pillared hall with a seat-backed parapet, pierced screens, inner turned columns and a coffered ceiling.
        public void Mandapa(Kit k, Vector3 c, float w, float d, float[] doors, float[] backGaps, bool friezes)
        {
            float floorTop = c.y + .1f, roofBottom = c.y + 3.25f, wallTop = floorTop + .9f;
            k.Floor.Box(c + Vector3.up * .05f, new Vector3(w, .1f, d));
            void Side(Vector3 a, Vector3 b, Vector3 outward, float[] gaps, bool doorway)
            {
                var axis = b - a; float length = axis.magnitude; axis /= length;
                var rot = Quaternion.LookRotation(outward, Vector3.up);
                bool alongX = Mathf.Abs(axis.x) > .5f;
                var solids = new List<Vector2>(); float cursor = 0;
                var sorted = new List<float>(gaps); sorted.Sort();
                foreach (float g in sorted)
                {
                    float t0 = (alongX ? (c.x + g - a.x) * Mathf.Sign(axis.x) : g) - (doorway ? .95f : 1.3f);
                    float t1 = t0 + (doorway ? 1.9f : 2.6f);
                    if (t0 > cursor) solids.Add(new Vector2(cursor, t0));
                    cursor = t1;
                    if (!doorway) continue;
                    var mid = a + axis * (t0 + t1) * .5f;
                    for (int s = -1; s <= 1; s += 2)
                        k.Edge.Box(mid + axis * s * .82f + Vector3.up * (floorTop + 1.2f), new Vector3(.26f, 2.4f, .52f), rot);
                    k.Edge.Box(mid + Vector3.up * (floorTop + 2.5f), new Vector3(1.9f, .2f, .56f), rot);
                    k.Stone.Box(mid + Vector3.up * ((floorTop + 2.6f + roofBottom) * .5f), new Vector3(1.9f, roofBottom - floorTop - 2.6f, .36f), rot);
                    for (int i = 0; i < 6; i++) k.Edge.Ellipsoid(mid + axis * (-.7f + i * .28f) + outward * .2f + Vector3.up * (floorTop + 2.86f), new Vector3(.07f, .1f, .04f), rot, 8, 4);
                    k.Edge.Slab(mid + outward * .5f + Vector3.up * (floorTop + 2.62f), new Vector2(alongX ? 2.4f : 1.1f, alongX ? 1.1f : 2.4f), new Vector2(alongX ? 2.1f : .5f, alongX ? .5f : 2.1f), .22f);
                }
                if (cursor < length) solids.Add(new Vector2(cursor, length));
                foreach (var span in solids)
                {
                    float l = span.y - span.x; if (l < .2f) continue;
                    var mid = a + axis * (span.x + span.y) * .5f;
                    k.Edge.Box(mid + Vector3.up * (floorTop + .12f), new Vector3(l, .24f, .5f), rot);
                    k.Stone.Box(mid + Vector3.up * (floorTop + .5f), new Vector3(l, .52f, .38f), rot);
                    k.Edge.Box(mid + Vector3.up * (floorTop + .82f), new Vector3(l + .02f, .14f, .56f), rot);
                    if (friezes)
                    {
                        int count = Mathf.FloorToInt(l / .42f);
                        for (int i = 0; i < count; i++) Elephant(k.Stone, a + axis * (span.x + (i + .5f) * l / count) + outward * .27f + Vector3.up * (floorTop + .27f), Quaternion.LookRotation(axis), .4f);
                    }
                    else
                    {
                        int count = Mathf.FloorToInt(l / .45f);
                        for (int i = 0; i < count; i++) k.Edge.Box(a + axis * (span.x + (i + .5f) * l / count) + outward * .2f + Vector3.up * (floorTop + .5f), new Vector3(.12f, .36f, .05f), rot);
                    }
                    int bays = Mathf.Max(1, Mathf.RoundToInt(l / 1.6f));
                    for (int i = 0; i <= bays; i++) Turned(k, a + axis * (span.x + i * l / bays) + Vector3.up * wallTop, roofBottom - wallTop, .5f, false, 20);
                    for (int i = 0; i < bays; i++)
                        Jali(k.Stone, a + axis * (span.x + (i + .5f) * l / bays) + Vector3.up * ((wallTop + roofBottom) * .5f), l / bays - .36f, roofBottom - wallTop - .06f, rot);
                    var centre = mid + Vector3.up * ((floorTop + roofBottom) * .5f);
                    k.Block(centre, alongX ? new Vector3(l, roofBottom - floorTop, .5f) : new Vector3(.5f, roofBottom - floorTop, l));
                }
            }
            float x0 = c.x - w * .5f, x1 = c.x + w * .5f, z0 = c.z - d * .5f, z1 = c.z + d * .5f;
            Side(new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), Vector3.back, doors, true);
            Side(new Vector3(x1, 0, z1), new Vector3(x0, 0, z1), Vector3.forward, backGaps, false);
            Side(new Vector3(x0, 0, z1), new Vector3(x0, 0, z0), Vector3.left, new float[0], false);
            Side(new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), Vector3.right, new float[0], false);
            // Inner columns flank each doorway axis; a coffered lotus ceiling hangs over each bay.
            foreach (float door in doors)
            {
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        var p = new Vector3(c.x + door + sx * 1.25f, floorTop, c.z + sz * 1.05f);
                        Turned(k, p, roofBottom - floorTop, .78f, true, 32);
                        k.Block(p + Vector3.up * 1.4f, new Vector3(.6f, 2.8f, .6f));
                    }
                var bay = new Vector3(c.x + door, roofBottom, c.z);
                k.Edge.Lathe(new[] { new Vector2(0, -.02f), new Vector2(.9f, -.02f), new Vector2(.9f, .1f), new Vector2(0, .1f) }, 24, bay + Vector3.down * .14f);
                k.Floor.Lathe(new[] { new Vector2(0, 0), new Vector2(.95f, 0), new Vector2(.95f, .1f), new Vector2(0, .1f) }, 32, new Vector3(c.x + door, floorTop, c.z));
                for (int ring = 0; ring < 3; ring++)
                {
                    float size = 2.2f - ring * .5f, yy = roofBottom - .3f + ring * .08f;
                    for (int side = 0; side < 4; side++)
                    {
                        var dir = Quaternion.Euler(0, side * 90, 0);
                        k.Edge.Box(bay + Vector3.up * (yy - roofBottom) + dir * new Vector3(0, 0, size * .5f), new Vector3(size + .12f, .1f, .12f), dir);
                    }
                }
                k.Stone.Lathe(new[] { new Vector2(0, -.62f), new Vector2(.08f, -.58f), new Vector2(.16f, -.46f), new Vector2(.22f, -.3f), new Vector2(.3f, -.14f), new Vector2(.36f, 0) }, 16, bay, true);
                for (int i = 0; i < 8; i++) k.Edge.Ellipsoid(bay + Quaternion.Euler(0, i * 45, 0) * new Vector3(0, -.22f, .3f), new Vector3(.08f, .18f, .04f), Quaternion.Euler(0, i * 45, 0) * Quaternion.Euler(30, 0, 0), 8, 4);
            }
            k.Stone.Box(new Vector3(c.x, roofBottom + .15f, c.z), new Vector3(w + .2f, .3f, d + .2f));
            k.Edge.Slab(new Vector3(c.x, roofBottom - .02f, c.z), new Vector2(w + 1.3f, d + 1.3f), new Vector2(w + .2f, d + .2f), .34f);
            float rt = roofBottom + .3f;
            k.Stone.Box(new Vector3(c.x, rt + .14f, z0 + .1f), new Vector3(w, .28f, .2f)); k.Stone.Box(new Vector3(c.x, rt + .14f, z1 - .1f), new Vector3(w, .28f, .2f));
            k.Stone.Box(new Vector3(x0 + .1f, rt + .14f, c.z), new Vector3(.2f, .28f, d)); k.Stone.Box(new Vector3(x1 - .1f, rt + .14f, c.z), new Vector3(.2f, .28f, d));
            for (int i = 0; i <= Mathf.RoundToInt(w / 1.8f); i++) { float x = x0 + .2f + i * (w - .4f) / Mathf.RoundToInt(w / 1.8f); Kuta(k, new Vector3(x, rt, z0 + .2f), .8f); }
            Kuta(k, new Vector3(x0 + .2f, rt, z1 - .2f), .8f); Kuta(k, new Vector3(x1 - .2f, rt, z1 - .2f), .8f);
        }

        void Steps(Kit k, Vector3 frontCentre, float width, float rise, int count, float tread = .38f)
        {
            float h = rise / count;
            for (int i = 0; i < count; i++)
            {
                float top = h * (i + 1);
                k.Floor.Box(frontCentre + new Vector3(0, top * .5f, (i + .5f) * tread), new Vector3(width, top, tread));
            }
            for (int s = -1; s <= 1; s += 2)
            {
                k.Edge.Box(frontCentre + new Vector3(s * (width * .5f + .18f), rise * .5f + .1f, count * tread * .5f), new Vector3(.36f, rise + .2f, count * tread));
                Kuta(k, frontCentre + new Vector3(s * (width * .5f + .18f), rise + .2f, count * tread * .35f), .7f);
                k.Block(frontCentre + new Vector3(s * (width * .5f + .18f), .8f, count * tread * .5f), new Vector3(.36f, 1.6f, count * tread));
            }
        }

        // ---------- Vijayanagara gopuram and pavilions ----------

        public GameObject Tower(Transform parent, Vector3 position, float scale = 1, bool detail = true)
        {
            var root = Group("Gopuram - schematic study", parent, position);
            var k = new Kit();
            k.Floor.Box(new Vector3(0, .25f, 0), new Vector3(6.8f, .5f, 3.8f));
            k.Edge.Box(new Vector3(0, .48f, 0), new Vector3(7f, .08f, 4f));
            for (int s = -1; s <= 1; s += 2)
            {
                var pier = new Vector3(s * 2.15f, 0, 0);
                k.Stone.Box(pier + Vector3.up * 2.05f, new Vector3(1.6f, 3.1f, 3f));
                k.Block(pier + Vector3.up * 2.05f, new Vector3(1.6f, 3.1f, 3f));
                for (int f = -1; f <= 1; f += 2)
                {
                    float z = f * 1.5f;
                    var rot = Quaternion.LookRotation(new Vector3(0, 0, f));
                    k.Edge.Box(pier + new Vector3(0, .7f, z), new Vector3(1.7f, .12f, .12f));
                    k.Edge.Box(pier + new Vector3(0, 1.95f, z), new Vector3(1.7f, .1f, .1f));
                    for (int p = -1; p <= 1; p += 2) k.Edge.Box(pier + new Vector3(p * .62f, 2.05f, z + f * .03f), new Vector3(.16f, 3.1f, .08f));
                    k.Recess.Box(pier + new Vector3(0, 1.32f, z + f * .01f), new Vector3(.6f, 1f, .04f));
                    k.Edge.Box(pier + new Vector3(0, 1.88f, z + f * .05f), new Vector3(.8f, .1f, .1f));
                    k.Edge.Lathe(new[] { new Vector2(0, 0), new Vector2(.26f, 0), new Vector2(.26f, .06f), new Vector2(0, .06f) }, 14, pier + new Vector3(0, 2.25f, z + f * .02f), rot * Quaternion.Euler(90, 0, 0), Vector3.one);
                    k.Recess.Lathe(new[] { new Vector2(0, 0), new Vector2(.17f, 0), new Vector2(.17f, .06f), new Vector2(0, .06f) }, 14, pier + new Vector3(0, 2.25f, z + f * .06f), rot * Quaternion.Euler(90, 0, 0), Vector3.one);
                    k.Edge.Box(pier + new Vector3(0, 2.75f, z + f * .05f), new Vector3(1.6f, .14f, .12f));
                }
            }
            k.Edge.Box(new Vector3(0, 3.8f, 0), new Vector3(6f, .4f, 3.1f));
            k.Stone.Box(new Vector3(0, 4.4f, 0), new Vector3(5.9f, .8f, 3.05f));
            for (int f = -1; f <= 1; f += 2)
                for (int i = 0; i < 9; i++) k.Edge.Box(new Vector3(-2.8f + i * .7f, 4.4f, f * 1.53f), new Vector3(.14f, .8f, .06f));
            k.Edge.Slab(new Vector3(0, 4.78f, 0), new Vector2(6.8f, 3.8f), new Vector2(6f, 3.1f), .2f);
            float y = 4.98f;
            for (int tier = 0; tier < 7; tier++)
            {
                float width = 5.8f - tier * .62f, depth = 3.0f - tier * .2f, h = .78f;
                k.Pale.Box(new Vector3(0, y + h * .5f, 0), new Vector3(width * .94f, h, depth * .94f));
                int niches = 7 - tier / 2;
                for (int f = -1; f <= 1; f += 2)
                {
                    float z = f * depth * .47f; var rot = Quaternion.LookRotation(new Vector3(0, 0, f));
                    for (int n = 0; n <= niches; n++) k.Pale.Box(new Vector3((n - niches * .5f) * width * .94f / niches, y + h * .5f, z + f * .03f), new Vector3(.1f, h, .06f));
                    for (int n = 0; n < niches; n++)
                    {
                        float x = (n + .5f - niches * .5f) * width * .94f / niches;
                        if (n == niches / 2 && niches % 2 == 1)
                        {
                            k.Pale.Lathe(new[] { new Vector2(0, 0), new Vector2(.26f, 0), new Vector2(.26f, .07f), new Vector2(0, .07f) }, 14, new Vector3(x, y + h * .52f, z), rot * Quaternion.Euler(90, 0, 0), Vector3.one);
                            k.Recess.Lathe(new[] { new Vector2(0, 0), new Vector2(.17f, 0), new Vector2(.17f, .07f), new Vector2(0, .07f) }, 14, new Vector3(x, y + h * .52f, z + f * .04f), rot * Quaternion.Euler(90, 0, 0), Vector3.one);
                        }
                        else
                        {
                            k.Recess.Box(new Vector3(x, y + h * .44f, z + f * .005f), new Vector3(.24f, h * .58f, .03f));
                            if (detail) k.Pale.Ellipsoid(new Vector3(x, y + h * .44f, z + f * .04f), new Vector3(.07f, h * .22f, .05f), rot, 8, 4);
                        }
                        k.Pale.Box(new Vector3(x, y + h * .8f, z + f * .06f), new Vector3(.34f, .08f, .14f));
                    }
                }
                k.Edge.Slab(new Vector3(0, y + h, 0), new Vector2(width + .4f, depth + .35f), new Vector2(width * .94f, depth * .94f), .14f);
                // Parapet of miniature pavilions on each cornice.
                for (int n = 0; n < niches; n++)
                {
                    float x = (n + .5f - niches * .5f) * width / niches;
                    for (int f = -1; f <= 1; f += 2)
                    {
                        var basePos = new Vector3(x, y + h + .14f, f * (depth * .5f - .05f));
                        if (n % 2 == 0) { k.Pale.Box(basePos + Vector3.up * .08f, new Vector3(.22f, .16f, .2f)); k.Pale.Lathe(new[] { new Vector2(0, 0), new Vector2(.11f, 0), new Vector2(.12f, .05f), new Vector2(0, .14f) }, 8, basePos + Vector3.up * .16f); }
                        else { k.Pale.Box(basePos + Vector3.up * .06f, new Vector3(.3f, .12f, .14f)); k.Pale.Lathe(new[] { new Vector2(0, 0), new Vector2(.07f, 0), new Vector2(.07f, .26f), new Vector2(0, .26f) }, 8, basePos + new Vector3(-.13f, .16f, 0), Quaternion.Euler(0, 0, -90), Vector3.one); }
                    }
                }
                y += h + .22f;
            }
            // Barrel-vaulted crown (shala) with gable ends and a row of kalasha finials.
            float crownWidth = 5.8f - 7 * .62f;
            k.Pale.Box(new Vector3(0, y + .12f, 0), new Vector3(crownWidth * .9f, .24f, 1.3f));
            k.Pale.Lathe(new[] { new Vector2(0, 0), new Vector2(.62f, 0), new Vector2(.62f, crownWidth * .8f), new Vector2(0, crownWidth * .8f) }, 18, new Vector3(-crownWidth * .4f, y + .62f, 0), Quaternion.Euler(0, 0, -90), new Vector3(1, 1, .8f));
            for (int s = -1; s <= 1; s += 2)
            {
                k.Pale.Lathe(new[] { new Vector2(0, 0), new Vector2(.8f, 0), new Vector2(.8f, .1f), new Vector2(0, .1f) }, 18, new Vector3(s < 0 ? -crownWidth * .4f : crownWidth * .4f - .1f, y + .62f, 0), Quaternion.Euler(0, 0, -90), new Vector3(1, 1, .9f));
                k.Pale.Ellipsoid(new Vector3(s * (crownWidth * .45f + .15f), y + 1.3f, 0), new Vector3(.12f, .3f, .1f), Quaternion.Euler(0, 0, s * -30), 8, 4);
            }
            for (int i = 0; i < 9; i++)
                k.Brass.Lathe(new[] { new Vector2(0, 0), new Vector2(.12f, 0), new Vector2(.15f, .12f), new Vector2(.08f, .26f), new Vector2(.03f, .42f), new Vector2(.06f, .46f), new Vector2(0, .6f) }, 12, new Vector3(-crownWidth * .38f + i * crownWidth * .095f, y + 1.18f, 0), Quaternion.identity, Vector3.one, true);
            Emit(k, root.transform, "Gopuram");
            root.transform.localScale = Vector3.one * scale; return root;
        }

        void Pavilion(Kit k, Vector3 c, float w, float len)
        {
            k.Floor.Box(c + Vector3.up * .3f, new Vector3(w, .6f, len));
            k.Edge.Box(c + Vector3.up * .08f, new Vector3(w + .16f, .16f, len + .16f));
            k.Edge.Box(c + Vector3.up * .55f, new Vector3(w + .1f, .1f, len + .1f));
            k.Recess.Box(c + Vector3.up * .32f, new Vector3(w + .02f, .2f, len + .02f));
            for (int i = 0; i < 2; i++)
            {
                float stepTop = .4f - .2f * i;
                k.Floor.Box(new Vector3(c.x, stepTop * .5f, c.z - len * .5f - .17f - i * .32f), new Vector3(w * .6f, stepTop, .34f));
            }
            float top = .6f, pillarH = 3f;
            for (int row = -1; row <= 1; row += 2)
                for (int j = 0; j < 5; j++)
                {
                    var p = new Vector3(c.x + row * (w * .5f - .5f), top, c.z - len * .5f + .8f + j * (len - 1.6f) / 4);
                    Composite(k, p, pillarH, .85f);
                    k.Block(p + Vector3.up * 1.5f, new Vector3(.55f, 3f, .55f));
                }
            float roof = top + pillarH + .28f;
            k.Edge.Box(new Vector3(c.x, roof - .14f, c.z), new Vector3(w - .5f, .28f, len - .8f));
            k.Stone.Box(new Vector3(c.x, roof + .14f, c.z), new Vector3(w + .3f, .28f, len + .3f));
            k.Edge.Slab(new Vector3(c.x, roof - .3f, c.z), new Vector2(w + 1.9f, len + 1.9f), new Vector2(w + 1.1f, len + 1.1f), .16f);
            k.Edge.Slab(new Vector3(c.x, roof - .14f, c.z), new Vector2(w + 1.1f, len + 1.1f), new Vector2(w + .3f, len + .3f), .3f);
            k.Stone.Box(new Vector3(c.x, roof + .45f, c.z), new Vector3(w - .2f, .34f, len - .2f));
            int count = Mathf.RoundToInt(len / 1.7f);
            for (int i = 0; i <= count; i++)
                for (int s = -1; s <= 1; s += 2) Kuta(k, new Vector3(c.x + s * (w * .5f - .25f), roof + .62f, c.z - len * .5f + .2f + i * (len - .4f) / count), .75f);
        }

        // ---------- Studies and whole temples ----------

        public GameObject Pillar(Transform parent, Vector3 position, bool square = false)
        {
            var root = Group("Architectural column", parent, position);
            var k = new Kit();
            if (square) Composite(k, Vector3.zero, 3.2f); else Turned(k, Vector3.zero, 3.35f, 1, true, 48);
            Emit(k, root.transform, "Column");
            var col = root.AddComponent<BoxCollider>(); col.center = new Vector3(0, 1.7f, 0); col.size = new Vector3(.9f, 3.4f, .9f);
            return root;
        }

        public GameObject Star(Transform parent, Vector3 position, float radius, float height)
        {
            var root = Group("Stellate platform", parent, position);
            var k = new Kit(); float r = radius * .95f;
            k.Floor.Star(Vector3.zero, r, r * .8f, 16, height * .4f);
            k.Floor.Star(Vector3.up * height * .4f, r * .9f, r * .72f, 16, height * .45f);
            k.Edge.Star(Vector3.up * height * .36f, r * 1.02f, r * .82f, 16, height * .08f);
            k.Floor.Star(Vector3.up * height * .85f, r * .82f, r * .66f, 16, height * .25f);
            StarRegister(k.Stone, Vector3.up * height * .42f, r * .9f, r * .72f, 16, Register.Elephants, height * .4f);
            Emit(k, root.transform, "Stellate study");
            var col = root.AddComponent<BoxCollider>(); col.center = new Vector3(0, height * .55f, 0); col.size = new Vector3(r * 2, height * 1.1f, r * 2);
            return root;
        }

        public GameObject ShrineStudy(Transform parent, Vector3 position)
        {
            var root = Group("Paired shrine study", parent, position);
            var k = new Kit();
            for (int s = -1; s <= 1; s += 2)
            {
                var c = new Vector3(s * .55f, 0, 0);
                k.Floor.Star(c, .95f, .76f, 16, .12f);
                Vimana(k, c + Vector3.up * .12f, .5f, k, false);
            }
            k.Stone.Box(new Vector3(0, .3f, -.55f), new Vector3(1.6f, .36f, .6f));
            Emit(k, root.transform, "Twin shrines");
            var col = root.AddComponent<BoxCollider>(); col.center = new Vector3(0, .8f, 0); col.size = new Vector3(2.1f, 1.6f, 1.6f);
            return root;
        }

        public GameObject Frieze(Transform parent, Vector3 position)
        {
            var root = Group("Ornament study", parent, position);
            var k = new Kit(); const float w = 3.2f;
            k.Stone.Box(new Vector3(0, .65f, .05f), new Vector3(w, 1.3f, .35f));
            k.Edge.Box(new Vector3(0, .06f, -.05f), new Vector3(w + .12f, .12f, .5f));
            k.Edge.Box(new Vector3(0, .45f, -.16f), new Vector3(w + .06f, .06f, .14f));
            k.Edge.Box(new Vector3(0, .86f, -.16f), new Vector3(w + .06f, .06f, .14f));
            k.Edge.Box(new Vector3(0, 1.26f, -.1f), new Vector3(w + .16f, .1f, .4f));
            for (int i = 0; i < 9; i++) Elephant(k.Stone, new Vector3(-w * .5f + .18f + i * .355f, .12f, -.22f), Quaternion.LookRotation(Vector3.right), .34f);
            for (int i = 0; i < 18; i++) Motif(k.Edge, new Vector3(-w * .5f + .09f + i * .177f, .45f, -.13f), Vector3.right, Vector3.back, Register.Scroll, .5f, i);
            for (int i = 0; i < 20; i++) Motif(k.Edge, new Vector3(-w * .5f + .08f + i * .16f, .86f, -.12f), Vector3.right, Vector3.back, Register.Lotus, .45f, i);
            Emit(k, root.transform, "Relief bands");
            var col = root.AddComponent<BoxCollider>(); col.center = new Vector3(0, .65f, 0); col.size = new Vector3(w + .2f, 1.35f, .6f);
            return root;
        }

        public GameObject CorniceStudy(Transform parent, Vector3 position)
        {
            var root = Group("Pavilion cornice study", parent, position);
            var k = new Kit();
            Composite(k, new Vector3(-.7f, 0, 0), 1.6f, .7f);
            Composite(k, new Vector3(.7f, 0, 0), 1.6f, .7f);
            k.Edge.Box(new Vector3(0, 1.98f, 0), new Vector3(2.4f, .22f, .5f));
            k.Edge.Slab(new Vector3(0, 1.9f, 0), new Vector2(2.9f, 1.5f), new Vector2(2.5f, .8f), .14f);
            k.Edge.Slab(new Vector3(0, 2.04f, 0), new Vector2(2.5f, .8f), new Vector2(2.3f, .5f), .22f);
            k.Stone.Box(new Vector3(0, 2.36f, 0), new Vector3(2.2f, .2f, .45f));
            for (int i = -1; i <= 1; i++) Kuta(k, new Vector3(i * .8f, 2.46f, 0), .7f);
            Emit(k, root.transform, "Cornice");
            var col = root.AddComponent<BoxCollider>(); col.center = new Vector3(0, 1.4f, 0); col.size = new Vector3(2.9f, 2.9f, 1.5f);
            return root;
        }

        // Builds a temple at the origin of parent. Illustrative superstructures go under restored (Then state) when given.
        public GameObject Temple(Transform parent, int index, bool miniature = false, Transform restored = null)
        {
            var root = Group("Temple architecture " + index, parent, Vector3.zero);
            var k = new Kit(); var tower = restored ? new Kit() : null;
            bool detail = !miniature;
            if (index == 2)
            {
                Tower(root.transform, new Vector3(0, 0, 8), 1, detail);
                for (int side = -1; side <= 1; side += 2) Pavilion(k, new Vector3(side * 6.5f, 0, 4), 3.6f, 10.4f);
            }
            else
            {
                float[] xs = index == 1 ? new[] { -3.9f, 3.9f } : new[] { 0f };
                float jagatiR = index == 1 ? 6.4f : 7.2f; const float jagatiZ = 5.8f;
                for (int s = 0; s < xs.Length; s++)
                {
                    float sx = xs[s], other = -sx;
                    // The twin platforms overlap; a few millimetres of height keep their tops from z-fighting.
                    var centre = new Vector3(sx, -.006f * s, jagatiZ);
                    Platform(k, centre, jagatiR, detail, xs.Length > 1 ? (System.Func<Vector3, bool>)(p => new Vector2(p.x - other, p.z - jagatiZ).magnitude < (jagatiR - .3f) * .82f) : null);
                    Steps(k, new Vector3(sx, 0, jagatiZ - jagatiR - .95f), 2.2f, Jagati, 3);
                    float R = index == 1 ? 2.6f : 2.9f;
                    // The base mouldings' front point stops just behind the vestibule face.
                    var sanctum = new Vector3(sx, Jagati, 5.65f + 1.07f * R);
                    Vimana(k, sanctum, R, tower, detail);
                    k.Stone.Box(new Vector3(sx, Jagati + 1.6f, 6.15f), new Vector3(2.6f, 3.2f, 1.2f));
                    k.Edge.Box(new Vector3(sx, Jagati + 3.3f, 6.15f), new Vector3(2.8f, .2f, 1.3f));
                    k.Block(new Vector3(sx, Jagati + 1.6f, 6.15f), new Vector3(2.6f, 3.2f, 1.2f));
                    if (!miniature) Doorway(k, new Vector3(sx, Jagati + .1f, 5.55f), Vector3.back);
                }
                float hallWidth = index == 1 ? 13.2f : 5.6f;
                var hallDoors = index == 1 ? new[] { -3.9f, 3.9f } : new[] { 0f };
                Mandapa(k, new Vector3(0, Jagati, 3.4f), hallWidth, 4.6f, hallDoors, hallDoors, index == 1 && detail);
            }
            Emit(k, root.transform, "Temple");
            if (tower != null) Emit(tower, restored, "Illustrative superstructure");
            if (miniature) root.transform.localScale = Vector3.one * .26f;
            return root;
        }

        public GameObject Ring(Transform parent, Vector3 position, float radius, Material material)
        {
            var ring = MeshObject("Teleport landing ring", parent, Lathe(new[] { new Vector2(radius, .015f), new Vector2(radius, .045f), new Vector2(radius - .06f, .045f), new Vector2(radius - .06f, .015f) }, 40), material);
            ring.transform.localPosition = position; return ring;
        }

        public void Dispose()
        {
            foreach (var mat in OwnedMaterials) Object.Destroy(mat);
            foreach (var mesh in meshes) Object.Destroy(mesh);
        }
    }
}
