using UnityEngine;
using Surface = Frontier.ArchitecturalMaterials.Surface;

namespace Frontier
{
    // Shared original landscape surfaces and the non-interactive route caravan.
    // This class never changes terrain heights, collision, fog or simulation state.
    public static class LandscapePresentation
    {
        private static Material groundMaterial;

        public static Material GroundMaterial()
        {
            if (groundMaterial != null) return groundMaterial;
            const int size = 1024;
            const float worldSize = 128f;
            var colors = new Color32[size * size];
            var heights = new float[size * size];
            var normals = new Color32[size * size];
            var grass = new Color(.365f, .405f, .245f);
            var shade = new Color(.30f, .35f, .19f);
            var earth = new Color(.48f, .435f, .30f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float wx = x * worldSize / size, wz = y * worldSize / size;
                    float broad = Mathf.PerlinNoise(wx * .063f + 8.3f, wz * .063f + 19.1f);
                    float patch = Mathf.PerlinNoise(wx * .19f + 23.4f, wz * .19f + 3.2f);
                    float clump = Mathf.PerlinNoise(wx * 1.7f + 17.2f, wz * 1.7f + 41.8f);
                    float dry = Mathf.SmoothStep(.52f, .78f, broad * .72f + patch * .28f);
                    float grit = Hash(x, y) - .5f;
                    float blades = Mathf.Sin(wx * 35f + wz * 7f + clump * 5f) * Mathf.Sin(wz * 23f - wx * 5f);
                    Color c = Color.Lerp(shade, grass, .25f + broad * .75f);
                    c = Color.Lerp(c, earth, dry * .63f);
                    float fine = (clump - .5f) * .065f + grit * .027f + blades * .013f * (1 - dry);
                    c.r += fine; c.g += fine; c.b += fine * .72f;
                    colors[x + y * size] = c;
                    heights[x + y * size] = clump * .48f + grit * .065f + blades * .10f * (1 - dry);
                }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = heights[Mathf.Min(size - 1, x + 1) + y * size] - heights[Mathf.Max(0, x - 1) + y * size];
                    float dy = heights[x + Mathf.Min(size - 1, y + 1) * size] - heights[x + Mathf.Max(0, y - 1) * size];
                    var normal = new Vector3(-dx * .52f, -dy * .52f, 1).normalized;
                    normals[x + y * size] = new Color(normal.x * .5f + .5f, normal.y * .5f + .5f, normal.z * .5f + .5f, 1);
                }
            var albedo = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            { name = "Frontier meadow and bare earth", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var normalMap = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            { name = "Frontier short grass and grit normal", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            albedo.SetPixels32(colors); albedo.Apply(true, true);
            normalMap.SetPixels32(normals); normalMap.Apply(true, true);
            groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { name = "Frontier / layered meadow", color = Color.white, enableInstancing = true };
            groundMaterial.SetTexture("_BaseMap", albedo);
            groundMaterial.SetTexture("_BumpMap", normalMap); groundMaterial.EnableKeyword("_NORMALMAP");
            groundMaterial.SetFloat("_BumpScale", .48f); groundMaterial.SetFloat("_Smoothness", .055f);
            return groundMaterial;
        }

        // The visible road follows the supplied simulation route, not a painted
        // approximation in GroundMaterial. UVs are in metres, matching buildings.
        public static Material RoadMaterial() => ArchitecturalMaterials.Get(Surface.Earth, new Color(.58f, .48f, .32f));

        private static float Hash(int x, int y)
        {
            uint n = unchecked((uint)x * 1597334677u ^ (uint)y * 3812015801u ^ 0x91e10da5u);
            n ^= n >> 16; n = unchecked(n * 2246822519u); n ^= n >> 13;
            return (n & 0x00ffffffu) / 16777216f;
        }

        public static void BuildNeutralCaravan(Transform root)
        {
            if (root == null) return;
            var g = new ArchitecturalGeometry();
            var wood = ArchitecturalMaterials.Get(Surface.Wood, new Color(.43f, .285f, .145f));
            var edge = ArchitecturalMaterials.Get(Surface.Wood, new Color(.61f, .44f, .24f));
            var iron = ArchitecturalMaterials.Get(Surface.Metal, new Color(.23f, .25f, .245f));
            var canvas = ArchitecturalMaterials.Get(Surface.Cloth, new Color(.81f, .735f, .55f));
            var leather = ArchitecturalMaterials.Get(Surface.Cloth, new Color(.25f, .17f, .105f));
            var horse = ArchitecturalMaterials.Get(Surface.Cloth, new Color(.47f, .315f, .19f));
            // Narrow side rails, distinct planks and suspended axles give a wagon
            // an open undercarriage. The entire decoration is one static mesh.
            for (int side = -1; side <= 1; side += 2)
            {
                g.Box(new Vector3(side * .54f, .61f, 0), new Vector3(.115f, .16f, 2.18f), wood);
                for (int row = 0; row < 3; row++)
                    g.Box(new Vector3(side * .73f, .79f + row * .13f, 0), new Vector3(.075f, .118f, 2.12f), wood);
                for (int stake = 0; stake < 4; stake++)
                    g.Box(new Vector3(side * .781f, .98f, -.88f + stake * .59f), new Vector3(.045f, .54f, .075f), edge);
                g.Box(new Vector3(side * .72f, 1.16f, 0), new Vector3(.105f, .055f, 2.20f), edge);
                // Two shafts, leather traces and a crossbar visibly connect the
                // load to the draft horse rather than leaving a floating wagon.
                g.Beam(new Vector3(side * .57f, .62f, .87f), new Vector3(side * .42f, .72f, 2.80f), .055f, .065f, wood);
                g.Beam(new Vector3(side * .45f, .82f, 1.28f), new Vector3(side * .255f, 1.04f, 2.69f), .025f, .025f, leather);
            }
            for (int plank = 0; plank < 8; plank++)
                g.Box(new Vector3((plank - 3.5f) * .18f, .745f, 0), new Vector3(.171f, .095f, 2.07f), wood);
            foreach (float z in new[] { -.72f, .72f })
            {
                g.Box(new Vector3(0, .49f, z), new Vector3(1.85f, .10f, .105f), iron);
                foreach (float x in new[] { -.86f, .86f }) Wheel(g, new Vector3(x, .49f, z), wood, iron);
            }
            for (int row = 0; row < 2; row++)
                g.Box(new Vector3(0, .82f + row * .13f, -1.06f), new Vector3(1.43f, .119f, .065f), wood);
            g.Box(new Vector3(0, 1.03f, .97f), new Vector3(1.30f, .095f, .28f), edge);
            g.Box(new Vector3(0, .83f, 1.17f), new Vector3(.76f, .065f, .23f), wood);
            // Half elliptical bows support a gently sagging, open ended cover.
            for (int segment = 0; segment < 18; segment++)
                for (int strip = 0; strip < 8; strip++)
                {
                    float a = segment * Mathf.PI / 18, b = (segment + 1) * Mathf.PI / 18;
                    float z0 = -.93f + strip * .22375f, z1 = z0 + .22375f;
                    g.Quad(CanvasPoint(a, z1), CanvasPoint(a, z0), CanvasPoint(b, z0), CanvasPoint(b, z1), canvas);
                    // The open back also exposes the cloth underside.
                    g.Quad(CanvasPoint(b, z1), CanvasPoint(b, z0), CanvasPoint(a, z0), CanvasPoint(a, z1), canvas);
                }
            foreach (float z in new[] { -.95f, -.49f, -.03f, .43f, .89f })
                for (int segment = 0; segment < 14; segment++)
                {
                    float a = segment * Mathf.PI / 14, b = (segment + 1) * Mathf.PI / 14;
                    Tube(g, Vector3.Scale(CanvasPoint(a, z), new Vector3(.973f, .994f, 1)), Vector3.Scale(CanvasPoint(b, z), new Vector3(.973f, .994f, 1)), .022f, .022f, edge, 6);
                }
            foreach (int side in new[] { -1, 1 })
                for (int tie = 0; tie < 4; tie++)
                {
                    float z = -.85f + tie * .56f;
                    g.Beam(new Vector3(side * .74f, 1.19f, z), new Vector3(side * .78f, .99f, z + .035f), .014f, .015f, leather);
                }
            // Cargo remains visible through both openings: barrel, sacks and a
            // low bale. These forms are part of the same material batches.
            Barrel(g, new Vector3(-.39f, .80f, -.43f), .22f, .56f, wood, iron);
            Ovoid(g, new Vector3(.27f, .99f, -.49f), new Vector3(.27f, .22f, .28f), canvas, 10, 6);
            Ovoid(g, new Vector3(.12f, .99f, .19f), new Vector3(.38f, .20f, .31f), canvas, 10, 6);
            g.Box(new Vector3(.13f, 1.12f, .15f), new Vector3(.045f, .08f, .50f), leather);
            DraftHorse(g, horse, leather, canvas, iron);
            g.Create(root, "Neutral caravan / bowed canvas, spoked wheels and draft horse");
        }

        private static Vector3 CanvasPoint(float angle, float z)
        {
            float sag = Mathf.Sin((z + .95f) * Mathf.PI / .46f) * .012f;
            return new Vector3(Mathf.Cos(angle) * .745f, 1.17f + Mathf.Sin(angle) * (.86f + sag), z);
        }

        private static void Wheel(ArchitecturalGeometry g, Vector3 c, Material wood, Material iron)
        {
            const int segments = 20;
            Vector3 P(float a, float r, float x) => c + new Vector3(x, Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                g.Quad(P(a, .46f, -.065f), P(b, .46f, -.065f), P(b, .46f, .065f), P(a, .46f, .065f), iron);
                g.Quad(P(a, .365f, .06f), P(b, .365f, .06f), P(b, .365f, -.06f), P(a, .365f, -.06f), wood);
                g.Quad(P(a, .46f, .065f), P(b, .46f, .065f), P(b, .365f, .06f), P(a, .365f, .06f), wood);
                g.Quad(P(a, .365f, -.06f), P(b, .365f, -.06f), P(b, .46f, -.065f), P(a, .46f, -.065f), wood);
            }
            for (int spoke = 0; spoke < 10; spoke++)
            {
                float a = spoke * Mathf.PI / 5;
                g.Beam(c, c + new Vector3(0, Mathf.Cos(a) * .383f, Mathf.Sin(a) * .383f), .062f, .061f, wood);
            }
            Tube(g, c - Vector3.right * .11f, c + Vector3.right * .11f, .117f, .117f, wood, 12);
            Tube(g, c - Vector3.right * .13f, c + Vector3.right * .13f, .053f, .053f, iron, 10);
        }

        private static void DraftHorse(ArchitecturalGeometry g, Material coat, Material dark, Material pale, Material metal)
        {
            // A compact, workmanlike horse instead of combat cavalry equipment.
            Ovoid(g, new Vector3(0, 1.01f, 2.28f), new Vector3(.26f, .35f, .58f), coat, 12, 8);
            Tube(g, new Vector3(0, 1.12f, 2.57f), new Vector3(0, 1.65f, 2.78f), .175f, .10f, coat, 10);
            Ovoid(g, new Vector3(0, 1.62f, 2.89f), new Vector3(.12f, .17f, .26f), coat, 10, 6);
            Ovoid(g, new Vector3(0, 1.53f, 3.105f), new Vector3(.108f, .09f, .094f), dark, 8, 5);
            for (int side = -1; side <= 1; side += 2)
            {
                Tube(g, new Vector3(side * .08f, 1.74f, 2.79f), new Vector3(side * .11f, 1.94f, 2.75f), .055f, .015f, coat, 6);
                Ovoid(g, new Vector3(side * .114f, 1.685f, 2.91f), new Vector3(.016f, .021f, .03f), dark, 6, 4);
                for (int end = -1; end <= 1; end += 2)
                {
                    float z = 2.28f + end * .39f;
                    var hip = new Vector3(side * .16f, .91f, z);
                    var knee = new Vector3(side * .19f, .43f, z - (end == -1 ? .11f : -.03f));
                    var foot = new Vector3(side * .20f, .11f, z + side * .095f);
                    Tube(g, hip, knee, .095f, .046f, coat, 8);
                    Tube(g, knee, foot, .046f, .034f, coat, 7);
                    Ovoid(g, foot - Vector3.up * .035f + Vector3.forward * .025f, new Vector3(.067f, .071f, .102f), dark, 8, 4);
                }
                // Bridle cheek, forehead band and collar trace.
                g.Beam(new Vector3(side * .113f, 1.68f, 2.84f), new Vector3(side * .107f, 1.54f, 3.075f), .021f, .020f, dark);
                g.Beam(new Vector3(side * .12f, 1.63f, 2.95f), new Vector3(side * .31f, 1.19f, 1.16f), .012f, .012f, dark);
                g.Beam(new Vector3(side * .24f, 1.27f, 2.57f), new Vector3(side * .24f, .83f, 2.60f), .054f, .055f, dark);
            }
            g.Beam(new Vector3(-.105f, 1.61f, 3.04f), new Vector3(.105f, 1.61f, 3.04f), .026f, .024f, dark);
            g.Box(new Vector3(0, 1.265f, 2.60f), new Vector3(.48f, .064f, .12f), dark);
            g.Box(new Vector3(0, .87f, 2.63f), new Vector3(.45f, .11f, .074f), dark);
            g.Box(new Vector3(.249f, 1.08f, 2.63f), new Vector3(.023f, .084f, .066f), metal);
            // Mane follows the back of the raised neck; the tail hangs free.
            Tube(g, new Vector3(0, 1.26f, 2.48f), new Vector3(0, 1.70f, 2.72f), .062f, .026f, dark, 7);
            Tube(g, new Vector3(0, 1.14f, 1.76f), new Vector3(.06f, .50f, 1.66f), .065f, .035f, dark, 7);
            // Small forehead star distinguishes the neutral trader at close zoom.
            g.Triangle(new Vector3(0, 1.764f, 2.995f), new Vector3(-.034f, 1.698f, 3.052f), new Vector3(.034f, 1.698f, 3.052f), pale);
        }

        private static void Barrel(ArchitecturalGeometry g, Vector3 bottom, float radius, float height, Material wood, Material iron)
        {
            Tube(g, bottom, bottom + Vector3.up * height * .48f, radius * .83f, radius, wood, 12);
            Tube(g, bottom + Vector3.up * height * .48f, bottom + Vector3.up * height, radius, radius * .83f, wood, 12);
            foreach (float y in new[] { .14f, .78f })
                Tube(g, bottom + Vector3.up * (height * y), bottom + Vector3.up * (height * y + .034f), radius * .95f, radius * .95f, iron, 12);
        }

        private static void Tube(ArchitecturalGeometry g, Vector3 a, Vector3 b, float radiusA, float radiusB, Material material, int sides)
        {
            var axis = (b - a).normalized;
            var right = Vector3.Cross(Mathf.Abs(axis.y) > .9f ? Vector3.forward : Vector3.up, axis).normalized;
            var forward = Vector3.Cross(axis, right);
            for (int i = 0; i < sides; i++)
            {
                float u = i * Mathf.PI * 2 / sides, v = (i + 1) * Mathf.PI * 2 / sides;
                var p = right * Mathf.Cos(u) + forward * Mathf.Sin(u);
                var q = right * Mathf.Cos(v) + forward * Mathf.Sin(v);
                g.Quad(a + p * radiusA, a + q * radiusA, b + q * radiusB, b + p * radiusB, material);
                g.Triangle(a, a + q * radiusA, a + p * radiusA, material);
                g.Triangle(b, b + p * radiusB, b + q * radiusB, material);
            }
        }

        private static void Ovoid(ArchitecturalGeometry g, Vector3 center, Vector3 radii, Material material, int sides, int rings)
        {
            Vector3 Point(int row, int col)
            {
                float latitude = -Mathf.PI * .5f + row * Mathf.PI / rings, angle = col * Mathf.PI * 2 / sides;
                return center + Vector3.Scale(radii, new Vector3(Mathf.Cos(latitude) * Mathf.Cos(angle), Mathf.Sin(latitude), Mathf.Cos(latitude) * Mathf.Sin(angle)));
            }
            for (int row = 0; row < rings; row++) for (int col = 0; col < sides; col++)
            {
                if (row == 0) g.Triangle(center - Vector3.up * radii.y, Point(1, col), Point(1, col + 1), material);
                else if (row == rings - 1) g.Triangle(Point(row, col + 1), Point(row, col), center + Vector3.up * radii.y, material);
                else g.Quad(Point(row, col + 1), Point(row, col), Point(row + 1, col), Point(row + 1, col + 1), material);
            }
        }

    }
}
