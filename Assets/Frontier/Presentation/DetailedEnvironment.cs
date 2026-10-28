using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Original resource models. Silhouette, grain and botanical details are authored
    // directly as geometry, then combined by material: no colliders, random-state
    // changes or changes to the simulation's resource/picking footprint.
    public static class DetailedEnvironment
    {
        private static readonly Color Bark = new Color(.29f, .205f, .125f);
        private static readonly Color BarkLight = new Color(.43f, .32f, .20f);
        private static readonly Color LeafDark = new Color(.17f, .25f, .095f);
        private static readonly Color Leaf = new Color(.30f, .39f, .15f);
        private static readonly Color LeafLight = new Color(.43f, .49f, .22f);
        private static readonly Color Timber = new Color(.49f, .33f, .17f);
        private static readonly Color EndGrain = new Color(.68f, .51f, .29f);
        private static readonly Color Iron = new Color(.22f, .235f, .21f);

        public static bool TryBuild(Transform root, EntityState entity)
        {
            if (root == null || entity == null || entity.def == null || entity.def.kind != EntityKind.Resource) return false;
            var id = entity.def.id;
            if (id != "tree" && id != "mine" && id != "berry" && id != "deer" &&
                id != "crate_food" && id != "crate_wood" && id != "crate_coin" && id != "trade_socket" &&
                id != "treasure_coin" && id != "treasure_wood" && id != "treasure_xp") return false;
            var model = new Model(root, unchecked((uint)entity.id * 747796405u + 2891336453u));
            switch (id)
            {
                case "tree": Tree(model, entity.id); break;
                case "mine": Mine(model); break;
                case "berry": Berries(model); break;
                case "deer": Deer(model, entity.id); break;
                case "trade_socket": TradeSite(model); break;
                case "treasure_coin": case "treasure_wood": case "treasure_xp": Treasure(model, id); break;
                default: Crate(model, id); break;
            }
            model.Flush();
            return true;
        }

        private static void Tree(Model m, int id)
        {
            int form = (int)((uint)id % 3);
            m.Mark("Trunk", new Vector3(0, 1.6f, 0));
            m.Mark("Crown", new Vector3(0, 4.1f, 0));
            var bark = m.Layer("Forest trunk and branching limbs", Bark);
            var ridges = m.Layer("Raised bark and exposed root ridges", BarkLight);
            var low = m.Layer("Crown shaded foliage", LeafDark);
            var mid = m.Layer("Crown layered foliage", Leaf);
            var high = m.Layer("Crown sunlit leaf edges", LeafLight);
            var lean = new Vector3(m.Range(-.13f, .13f), 0, m.Range(-.12f, .12f));
            // Visible roots anchor each trunk. Their tips stay inside the existing
            // resource ground footprint; the high canopy keeps its old extent.
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3 + .17f;
                Vector3 tip = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * m.Range(.54f, .82f);
                bark.Tube(tip + Vector3.up * .025f, tip * .35f + Vector3.up * .20f, .04f, .13f, 5);
                bark.Tube(tip * .35f + Vector3.up * .20f, Vector3.up * .47f, .13f, .15f, 6);
            }
            var bend = new Vector3(-.035f, 1.35f, .055f) + lean * .3f;
            var fork = new Vector3(.06f, 2.45f, -.045f) + lean;
            bark.Tube(Vector3.zero, bend, .255f, .18f, 10);
            bark.Tube(bend, fork, .18f, .135f, 9);
            bark.Tube(fork, new Vector3(lean.x * 1.8f, 4.25f, lean.z), .135f, .028f, 7);
            for (int i = 0; i < 9; i++)
            {
                float angle = i * Mathf.PI * 2 / 9;
                var a = new Vector3(Mathf.Cos(angle) * .247f, .1f, Mathf.Sin(angle) * .247f);
                var b = bend + new Vector3(Mathf.Cos(angle) * .173f, -.07f, Mathf.Sin(angle) * .173f);
                ridges.Tube(a, b, .019f, .011f, 3);
            }
            if (form == 2)
            {
                m.Mark("Layered evergreen silhouette", Vector3.up * 3.5f);
                // Staggered, scalloped skirts with open seams expose the trunk.
                for (int tier = 0; tier < 5; tier++)
                {
                    float y = 1.90f + tier * .57f;
                    float radius = 1.28f - tier * .205f;
                    var center = new Vector3(lean.x * (y / 2.5f), y, lean.z);
                    for (int b = 0; b < 5; b++)
                    {
                        float angle = b * Mathf.PI * .4f + tier * .77f;
                        var end = center + new Vector3(Mathf.Cos(angle) * radius * .86f, -.05f, Mathf.Sin(angle) * radius * .77f);
                        bark.Tube(center, end, .057f - tier * .007f, .009f, 5);
                    }
                    Foliage(m, low, mid, high, center + Vector3.up * .39f,
                        new Vector3(radius, .60f, radius * .67f), tier * 1.7f, true);
                }
                Foliage(m, low, mid, high, new Vector3(lean.x * 1.9f, 4.78f, lean.z),
                    new Vector3(.29f, .48f, .28f), .6f, true);
            }
            else
            {
                m.Mark(form == 0 ? "Spreading broadleaf silhouette" : "Upright broadleaf silhouette", Vector3.up * 3.5f);
                // Five forks have different elevations and leaf shapes, rather
                // than repeating the same sphere at different positions.
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * Mathf.PI * .4f + .38f;
                    float reach = form == 0 ? m.Range(.65f, .83f) : m.Range(.48f, .74f);
                    float height = i < 3 ? 3.24f + i * .20f : 3.82f + (i - 3) * .22f;
                    var end = new Vector3(Mathf.Cos(angle) * reach, height, Mathf.Sin(angle) * reach * .56f) + lean;
                    var start = fork + Vector3.up * (i * .105f - .20f);
                    var elbow = Vector3.Lerp(start, end, .63f) + Vector3.down * .20f;
                    bark.Tube(start, elbow, .095f - i * .006f, .055f, 7);
                    bark.Tube(elbow, end + Vector3.up * .25f, .055f, .018f, 5);
                    var twig = elbow + new Vector3(-Mathf.Sin(angle) * .31f, .36f, Mathf.Cos(angle) * .25f);
                    bark.Tube(elbow, twig, .033f, .009f, 5);
                    Foliage(m, low, mid, high, end + Vector3.up * .28f,
                        new Vector3(form == 0 ? .74f : .65f, m.Range(.68f, .88f), .45f), angle + .8f, false);
                }
                // Central upper leader preserves selection at the old y=4.1 crown.
                Foliage(m, low, mid, high, new Vector3(lean.x * 1.3f, 4.31f, lean.z),
                    new Vector3(form == 0 ? .73f : .68f, form == 0 ? .82f : 1.03f, .68f), 4.1f, false);
            }
        }

        private static void Foliage(Model m, Batch shade, Batch leaf, Batch light, Vector3 center, Vector3 extent, float phase, bool needle)
        {
            int sides = needle ? 18 : 12;
            float[] heights = needle ? new[] { -.62f, -.40f, .04f, .56f, 1f } : new[] { -.76f, -.36f, .19f, .64f, .94f };
            float[] widths = needle ? new[] { .68f, 1f, .68f, .33f, .025f } : new[] { .24f, .91f, 1f, .71f, .15f };
            var rings = new Vector3[5, sides];
            for (int j = 0; j < 5; j++)
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides;
                    float irregular = 1 + .11f * Mathf.Sin(i * 2.31f + phase) + .065f * Mathf.Cos(i * 3.17f + j * .7f + phase);
                    rings[j, i] = center + Vector3.Scale(extent, new Vector3(Mathf.Cos(a) * widths[j] * irregular,
                        heights[j] + Mathf.Sin(i * 1.93f + phase) * .06f * widths[j], Mathf.Sin(a) * widths[j] * irregular));
                }
            for (int j = 0; j < 4; j++)
                for (int i = 0; i < sides; i++)
                {
                    int n = (i + 1) % sides;
                    var batch = j == 0 ? shade : j == 3 && i % 3 != 0 ? light : leaf;
                    batch.Quad(rings[j, n], rings[j, i], rings[j + 1, i], rings[j + 1, n]);
                }
            for (int i = 0; i < sides; i++)
            {
                int n = (i + 1) % sides;
                shade.Tri(center + Vector3.up * extent.y * heights[0], rings[0, i], rings[0, n]);
                light.Tri(center + Vector3.up * extent.y, rings[4, n], rings[4, i]);
                // Broad leaves catch small highlights. Pine needles use fewer,
                // shorter dark fans: large pale triangles read as loose paper at
                // the strategic camera angle instead of foliage on a branch.
                if (needle && i % 3 != 0) continue;
                var anchor = rings[1 + i % 2, i];
                var outward = new Vector3(anchor.x - center.x, needle ? -.02f : .03f, anchor.z - center.z).normalized;
                var side = Vector3.Cross(Vector3.up, outward) * (needle ? .065f : .13f);
                var tip = anchor + outward * (needle ? .085f : .11f) + Vector3.up * .035f;
                (!needle && i % 3 == 0 ? light : leaf).DoubleTri(anchor - side, tip, anchor + side + Vector3.up * .07f);
            }
        }

        private static void Mine(Model m)
        {
            m.Mark("Ore", new Vector3(.3f, .55f, .3f));
            var rock = m.Layer("Ore fractured stone", new Color(.37f, .38f, .34f));
            var dark = m.Layer("Ore shaded fracture faces", new Color(.255f, .28f, .26f));
            var bright = m.Layer("Ore silver quartz seams", new Color(.72f, .70f, .55f));
            var mineral = m.Layer("Ore exposed mineral", new Color(.54f, .51f, .34f));
            // Angular, tilted strata rather than a row of identical round lumps.
            Stone(rock, dark, new Vector3(-.56f, .02f, .35f), new Vector3(.80f, .97f, .68f), .25f, 0, mineral);
            Stone(rock, dark, new Vector3(.53f, .02f, .24f), new Vector3(.925f, 1.25f, .75f), -.13f, 1, bright);
            Stone(rock, dark, new Vector3(-.02f, .01f, -.35f), new Vector3(.75f, .72f, .37f), .36f, 2, bright);
            Stone(rock, dark, new Vector3(.35f, .02f, .99f), new Vector3(.72f, .65f, .36f), -.21f, 3);
            for (int i = 0; i < 11; i++)
            {
                float a = i * 2.39996f;
                float x = Mathf.Cos(a) * m.Range(.85f, 1.31f), z = .28f + Mathf.Sin(a) * m.Range(.68f, .82f);
                float s = m.Range(.07f, .17f);
                Stone(i % 3 == 0 ? mineral : rock, dark, new Vector3(x, .025f, z), new Vector3(s, s * 1.4f, s * .8f), a, i);
            }
        }

        private static void Stone(Batch rock, Batch dark, Vector3 origin, Vector3 size, float twist, int seed, Batch vein = null)
        {
            const int n = 7;
            var bottom = new Vector3[n]; var shoulder = new Vector3[n]; var top = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2 / n + twist;
                float r = 1 + Mathf.Sin(i * 7.17f + seed * 2.13f) * .12f;
                bottom[i] = origin + new Vector3(Mathf.Cos(a) * size.x * r, 0, Mathf.Sin(a) * size.z * r);
                shoulder[i] = origin + new Vector3(Mathf.Cos(a) * size.x * .89f * r, size.y * (.49f + .09f * Mathf.Sin(a + seed)), Mathf.Sin(a) * size.z * .89f * r);
                top[i] = origin + new Vector3(Mathf.Cos(a) * size.x * .52f * r + size.x * .08f,
                    size.y * (.88f + .12f * Mathf.Sin(a * 2 + seed)), Mathf.Sin(a) * size.z * .53f * r);
            }
            var summit = origin + new Vector3(size.x * .16f, size.y * 1.03f, -size.z * .13f);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                (i % 3 == 0 ? dark : rock).Quad(bottom[j], bottom[i], shoulder[i], shoulder[j]);
                rock.Quad(shoulder[j], shoulder[i], top[i], top[j]);
                rock.Tri(top[j], top[i], summit);
                if (vein != null)
                {
                    // Strips are barycentric to actual exposed triangles, with
                    // seams on opposing faces and several top facets. The ore
                    // remains legible when the camera sees the other side.
                    if (i == 1 || i == 4)
                    {
                        var a = shoulder[j]; var b = shoulder[i]; var c = top[i];
                        var normal = Vector3.Cross(b - a, c - a).normalized * .006f;
                        vein.Quad(Vector3.Lerp(a, b, .29f) + normal, Vector3.Lerp(a, b, .40f) + normal,
                            Vector3.Lerp(a, c, .87f) + normal, Vector3.Lerp(a, c, .75f) + normal);
                    }
                    if (i % 2 == 0)
                    {
                        var topNormal = Vector3.Cross(top[i] - top[j], summit - top[j]).normalized * .007f;
                        vein.Quad(Vector3.Lerp(top[j], top[i], .22f) + topNormal,
                            Vector3.Lerp(top[j], top[i], .37f) + topNormal,
                            Vector3.Lerp(top[j], summit, .91f) + topNormal,
                            Vector3.Lerp(top[j], summit, .77f) + topNormal);
                    }
                }
            }
        }

        private static void Berries(Model m)
        {
            var stems = m.Layer("Berry woody stems", Bark);
            var leaf = m.Layer("Berry pointed foliage", Leaf);
            var shade = m.Layer("Berry shaded foliage", LeafDark);
            var ripe = m.Layer("Clusters of ripe berries", new Color(.49f, .10f, .12f));
            var bloom = m.Layer("Berry sunlit skin", new Color(.69f, .23f, .24f));
            for (int shrub = 0; shrub < 3; shrub++)
            {
                var basePoint = new Vector3((shrub - 1) * .50f, .035f, shrub == 1 ? .16f : -.06f);
                var center = basePoint + new Vector3((shrub - 1) * .04f, .65f + shrub % 2 * .18f, 0);
                stems.Tube(basePoint, center, .058f, .019f, 6);
                m.Mark("Berry bush " + (shrub + 1), center);
                for (int branch = 0; branch < 6; branch++)
                {
                    float a = branch * Mathf.PI / 3 + shrub * .65f;
                    var branchEnd = center + new Vector3(Mathf.Cos(a) * .42f, .08f + (branch % 3) * .13f, Mathf.Sin(a) * .39f);
                    stems.Tube(Vector3.Lerp(basePoint, center, .55f), branchEnd, .025f, .008f, 5);
                    for (int sprig = 0; sprig < 3; sprig++)
                    {
                        var p = Vector3.Lerp(center, branchEnd, .33f + sprig * .30f);
                        var side = new Vector3(-Mathf.Sin(a), .35f, Mathf.Cos(a));
                        LeafBlade(leaf, shade, p, p + side * (.20f + sprig * .015f) + Vector3.up * .11f, .11f);
                        LeafBlade(leaf, shade, p, p - side * .20f + Vector3.up * .15f, .105f);
                    }
                    // Small grapes of three, not a single oversize decorative ball.
                    for (int fruit = 0; fruit < 3; fruit++)
                    {
                        var p = branchEnd + new Vector3((fruit - 1) * .065f, -.03f - (fruit % 2) * .065f, -.028f * fruit);
                        (fruit == 1 ? bloom : ripe).Ovoid(p, new Vector3(.059f, .066f, .057f), 6, 3);
                    }
                }
            }
        }

        private static void LeafBlade(Batch face, Batch underside, Vector3 stem, Vector3 tip, float halfWidth)
        {
            var axis = tip - stem;
            var side = Vector3.Cross(axis, Vector3.up).normalized * halfWidth;
            if (side.sqrMagnitude < .000001f) side = Vector3.right * halfWidth;
            var mid = Vector3.Lerp(stem, tip, .47f) + Vector3.up * .025f;
            var left = Vector3.Lerp(stem, tip, .5f) + side;
            var right = Vector3.Lerp(stem, tip, .5f) - side;
            face.Tri(stem, left, mid); face.Tri(left, tip, mid);
            face.Tri(tip, right, mid); face.Tri(right, stem, mid);
            underside.Tri(stem, mid, left); underside.Tri(left, mid, tip);
            underside.Tri(tip, mid, right); underside.Tri(right, mid, stem);
        }

        private static void Deer(Model m, int id)
        {
            var coat = m.Layer("Deer russet coat", new Color(.49f, .315f, .17f));
            var light = m.Layer("Deer pale throat and underside", new Color(.71f, .58f, .39f));
            var dark = m.Layer("Deer eyes muzzle and cloven hooves", new Color(.135f, .11f, .077f));
            var antler = m.Layer("Deer branching antlers", new Color(.60f, .50f, .34f));
            m.Mark("Deer body", new Vector3(0, .86f, 0));
            m.Mark("Deer head", new Vector3(0, 1.39f, .66f));
            // Tapered rump, barrel, withers and raised neck form a continuous
            // anatomical silhouette. The animal faces the same +Z as before.
            coat.LoftZ(new[] {
                new Section(new Vector3(0,.91f,-.52f),.10f,.14f),
                new Section(new Vector3(0,.89f,-.35f),.205f,.285f),
                new Section(new Vector3(0,.86f,-.02f),.235f,.27f),
                new Section(new Vector3(0,.90f,.29f),.19f,.29f),
                new Section(new Vector3(0,1.02f,.41f),.125f,.17f)}, 10);
            coat.Tube(new Vector3(0, .93f, .32f), new Vector3(.025f, 1.38f, .54f), .158f, .10f, 9);
            light.Tube(new Vector3(.0f, 1.01f, .46f), new Vector3(.025f, 1.33f, .63f), .070f, .06f, 7);
            coat.LoftZ(new[] {
                new Section(new Vector3(.025f,1.42f,.49f),.085f,.10f),
                new Section(new Vector3(.025f,1.42f,.63f),.116f,.14f),
                new Section(new Vector3(.025f,1.34f,.84f),.057f,.060f)}, 8);
            dark.Ovoid(new Vector3(.025f, 1.345f, .86f), new Vector3(.064f, .045f, .035f), 8, 4);
            for (int side = -1; side <= 1; side += 2)
            {
                dark.Ovoid(new Vector3(.025f + side * .105f, 1.46f, .651f), new Vector3(.018f, .025f, .027f), 6, 4);
                var earRoot = new Vector3(.025f + side * .07f, 1.515f, .54f);
                LeafBlade(coat, light, earRoot, earRoot + new Vector3(side * .20f, .17f, -.045f), .061f);
                for (int end = -1; end <= 1; end += 2)
                {
                    bool hind = end == -1;
                    float z = hind ? -.37f : .29f;
                    var hip = new Vector3(side * .135f, hind ? .85f : .90f, z);
                    var joint = new Vector3(side * .16f, .43f, z + (hind ? -.115f : .035f));
                    var foot = new Vector3(side * .17f, .055f, z + (side == 1 ? .055f : -.025f));
                    m.Mark("Deer leg " + side + " " + end, joint);
                    coat.Tube(hip, joint, hind ? .080f : .063f, .030f, 7);
                    coat.Tube(joint, foot + Vector3.up * .04f, .030f, .023f, 6);
                    for (int toe = -1; toe <= 1; toe += 2)
                        dark.Box(foot + new Vector3(toe * .019f, 0, .015f), new Vector3(.027f, .080f, .093f));
                }
                if ((uint)id % 3 != 1)
                {
                    var start = new Vector3(.025f + side * .065f, 1.52f, .58f);
                    var fork = start + new Vector3(side * .115f, .22f, -.075f);
                    var tip = fork + new Vector3(side * .085f, .19f, -.14f);
                    antler.Tube(start, fork, .029f, .021f, 6);
                    antler.Tube(fork, tip, .021f, .005f, 5);
                    antler.Tube(Vector3.Lerp(start, fork, .7f), fork + new Vector3(-side * .016f, .16f, .045f), .015f, .003f, 5);
                    antler.Tube(Vector3.Lerp(fork, tip, .48f), tip + new Vector3(-side * .050f, .075f, .070f), .012f, .003f, 5);
                }
            }
            coat.Tube(new Vector3(0, .98f, -.47f), new Vector3(0, .92f, -.68f), .056f, .019f, 7);
            light.Tube(new Vector3(0, .94f, -.47f), new Vector3(0, .89f, -.62f), .046f, .014f, 6);
            // A light lower belly is narrower than the barrel, not a second body.
            light.Ovoid(new Vector3(0, .675f, -.02f), new Vector3(.155f, .075f, .29f), 10, 4);
        }

        private static void Crate(Model m, string id)
        {
            var timber = m.Layer("Crate individual weathered boards", Timber);
            var cut = m.Layer("Crate pale timber edges", EndGrain);
            var iron = m.Layer("Crate nails and iron binding", Iron);
            var grooves = m.Layer("Crate wood grain and joints", Bark);
            m.Mark("Open supply crate", new Vector3(0, .30f, 0));
            for (int i = 0; i < 5; i++)
                timber.Box(new Vector3((i - 2) * .185f, .125f, 0), new Vector3(.177f, .10f, .81f));
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < 3; row++)
                {
                    float y = .215f + row * .12f;
                    timber.Box(new Vector3(0, y, side * .393f), new Vector3(.99f, .109f, .058f));
                    timber.Box(new Vector3(side * .466f, y, 0), new Vector3(.058f, .109f, .73f));
                    grooves.Box(new Vector3(.045f * side, y + .018f, side * .424f), new Vector3(.71f, .005f, .004f));
                    grooves.Box(new Vector3(side * .497f, y - .015f, .03f), new Vector3(.004f, .004f, .56f));
                }
                for (int end = -1; end <= 1; end += 2)
                {
                    cut.Box(new Vector3(side * .429f, .32f, end * .349f), new Vector3(.085f, .42f, .072f));
                    iron.Box(new Vector3(side * .335f, .325f, end * .426f), new Vector3(.052f, .38f, .014f));
                    for (int nail = 0; nail < 3; nail++)
                        iron.Ovoid(new Vector3(side * .428f, .218f + nail * .12f, end * .426f), new Vector3(.013f, .013f, .008f), 5, 3);
                }
                timber.Box(new Vector3(0, .502f, side * .395f), new Vector3(1.005f, .043f, .078f));
                cut.Box(new Vector3(side * .465f, .502f, 0), new Vector3(.06f, .043f, .72f));
            }
            if (id == "crate_wood")
            {
                for (int i = 0; i < 5; i++)
                {
                    float x = i < 3 ? (i - 1) * .239f : (i - 3.5f) * .24f;
                    float y = i < 3 ? .54f : .741f;
                    var a = new Vector3(x, y, -.43f); var b = new Vector3(x, y + .023f, .41f);
                    m.Mark("Visible timber log " + (i + 1), (a + b) * .5f);
                    grooves.Tube(a, b, .113f, .105f, 9);
                    cut.Disc(a - Vector3.forward * .002f, Vector3.back, .096f, 10);
                    cut.Disc(b + Vector3.forward * .002f, Vector3.forward, .089f, 10);
                    // End rings are actual concentric narrow bands, not painted
                    // duplicate cylinders that multiply renderer count.
                    grooves.Ring(a - Vector3.forward * .003f, Vector3.back, .075f, .004f, 12);
                    grooves.Ring(a - Vector3.forward * .004f, Vector3.back, .043f, .004f, 12);
                    grooves.Tube(a + new Vector3(.027f, -.075f, -.005f), a + new Vector3(.003f, .018f, -.005f), .004f, .002f, 3);
                    for (int ridge = 0; ridge < 3; ridge++)
                    {
                        float angle = ridge * Mathf.PI * 2 / 3 + .4f;
                        var offset = new Vector3(Mathf.Cos(angle) * .108f, Mathf.Sin(angle) * .108f, 0);
                        timber.Tube(a + offset, b + offset * .92f, .008f, .006f, 4);
                    }
                }
            }
            else if (id == "crate_coin")
            {
                var gold = m.Layer("Visible gold ingots", new Color(.73f, .52f, .18f));
                for (int i = 0; i < 6; i++)
                {
                    var p = new Vector3((i % 3 - 1) * .245f, .42f + i / 3 * .125f, i / 3 == 0 ? -.14f : .06f);
                    m.Mark("Visible gold ingot " + (i + 1), p);
                    gold.Trapezoid(p, new Vector3(.215f, .115f, .35f), .78f);
                    // A dark stamped mint groove catches light at close zoom.
                    grooves.Box(p + new Vector3(0, .059f, .015f), new Vector3(.067f, .004f, .019f));
                }
            }
            else
            {
                var sack = m.Layer("Food grain sack woven linen", new Color(.67f, .56f, .37f));
                var produce = m.Layer("Food produce apples", new Color(.57f, .19f, .11f));
                for (int i = 0; i < 3; i++)
                {
                    var p = new Vector3((i - 1) * .255f, .48f + (i % 2) * .025f, .03f);
                    m.Mark("Food grain sack " + (i + 1), p);
                    sack.Ovoid(p, new Vector3(.175f, .235f, .205f), 9, 5);
                    sack.Tube(p + Vector3.up * .14f, p + Vector3.up * .29f, .075f, .045f, 7);
                    grooves.Ring(p + Vector3.up * .244f, Vector3.up, .049f, .009f, 9);
                    // Long seam and two fold lines on the visible sack face.
                    for (int seam = 0; seam < 3; seam++)
                        cut.Tube(p + new Vector3((seam - 1) * .052f, -.08f, -.180f), p + new Vector3((seam - 1) * .029f, .135f, -.139f), .004f, .003f, 3);
                }
                for (int i = 0; i < 4; i++)
                {
                    var p = new Vector3((i - 1.5f) * .14f, .58f + (i % 2) * .03f, -.26f);
                    produce.Ovoid(p, new Vector3(.085f, .08f, .08f), 7, 4);
                    grooves.Tube(p + Vector3.up * .060f, p + new Vector3(.014f, .104f, 0), .009f, .005f, 4);
                }
            }
        }

        private static void TradeSite(Model m)
        {
            var timber = m.Layer("Trade site hewn boundary posts", Timber);
            var cut = m.Layer("Trade site cut timber and rope", EndGrain);
            var iron = m.Layer("Trade site iron nails and route emblem", Iron);
            var stones = m.Layer("Trade site low corner stones", new Color(.46f, .45f, .38f));
            // Exactly the old four-corner footprint. The front stays open and
            // nothing is placed in the central construction/picking space.
            foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
            {
                var p = new Vector3(x * 1.8f, 0, z * 1.8f);
                m.Mark("Site marker " + x + " " + z, p + Vector3.up * .35f);
                stones.Trapezoid(p + Vector3.up * .055f, new Vector3(.17f, .10f, .17f), .85f);
                timber.Trapezoid(p + Vector3.up * .36f, new Vector3(.13f, .60f, .13f), .86f);
                cut.Trapezoid(p + Vector3.up * .665f, new Vector3(.116f, .07f, .116f), .17f);
                iron.Box(p + new Vector3(0, .17f, -.067f), new Vector3(.105f, .038f, .009f));
                iron.Box(p + new Vector3(0, .51f, .067f), new Vector3(.103f, .034f, .009f));
                cut.Ring(p + Vector3.up * .47f, Vector3.up, .073f, .013f, 8);
            }
            foreach (int side in new[] { -1, 1 })
                Rope(cut, new Vector3(side * 1.8f, .49f, -1.8f), new Vector3(side * 1.8f, .49f, 1.8f), .13f);
            Rope(cut, new Vector3(-1.8f, .49f, 1.8f), new Vector3(1.8f, .49f, 1.8f), .13f);
            timber.Box(new Vector3(0, .39f, 1.8f), new Vector3(.50f, .23f, .035f));
            cut.Box(new Vector3(0, .504f, 1.8f), new Vector3(.52f, .025f, .046f));
            // A small wagon wheel on the hanging sign marks a future trading
            // place without pretending that a trading post already exists.
            iron.Ring(new Vector3(-.085f, .39f, 1.776f), Vector3.back, .075f, .012f, 12);
            iron.Tube(new Vector3(-.15f, .39f, 1.774f), new Vector3(-.02f, .39f, 1.774f), .008f, .008f, 4);
            iron.Tube(new Vector3(-.085f, .325f, 1.774f), new Vector3(-.085f, .455f, 1.774f), .008f, .008f, 4);
            iron.DoubleTri(new Vector3(.08f, .345f, 1.773f), new Vector3(.18f, .39f, 1.773f), new Vector3(.08f, .435f, 1.773f));
            iron.Box(new Vector3(.057f, .39f, 1.772f), new Vector3(.09f, .015f, .009f));
        }

        private static void Rope(Batch rope, Vector3 a, Vector3 b, float sag)
        {
            for (int i = 0; i < 12; i++)
            {
                float t = i / 12f, next = (i + 1) / 12f;
                var p = Vector3.Lerp(a, b, t) - Vector3.up * (4 * t * (1 - t) * sag);
                var q = Vector3.Lerp(a, b, next) - Vector3.up * (4 * next * (1 - next) * sag);
                rope.Tube(p, q, .013f, .013f, 5);
            }
        }

        private static void Treasure(Model m, string id)
        {
            var wood = m.Layer("Treasure chest fitted oak panels", new Color(.39f, .24f, .115f));
            var edge = m.Layer("Treasure chest worn edges", BarkLight);
            var iron = m.Layer("Treasure chest forged straps and rivets", Iron);
            var dark = m.Layer("Treasure chest recessed interior", new Color(.16f, .12f, .074f));
            Batch contents, accent;
            if (id == "treasure_coin")
            {
                contents = m.Layer("Treasure exposed coins and gold", new Color(.80f, .59f, .20f));
                accent = contents;
            }
            else if (id == "treasure_wood")
            {
                contents = m.Layer("Treasure exposed timber bark", Bark);
                accent = m.Layer("Treasure exposed timber end grain", EndGrain);
            }
            else
            {
                contents = m.Layer("Treasure books and blue expedition pennant", new Color(.20f, .37f, .49f));
                accent = m.Layer("Treasure parchment maps and book pages", new Color(.79f, .73f, .54f));
            }
            m.Mark("Treasure chest", new Vector3(0, .5f, 0));
            m.Mark("Arched hinged lid", new Vector3(0, 1.13f, -.14f));
            dark.Box(new Vector3(0, .49f, 0), new Vector3(1.10f, .69f, .69f));
            for (int row = 0; row < 4; row++)
            {
                float y = .22f + row * .16f;
                foreach (int side in new[] { -1, 1 })
                {
                    wood.Box(new Vector3(0, y, side * .382f), new Vector3(1.20f, .148f, .068f));
                    wood.Box(new Vector3(side * .568f, y, 0), new Vector3(.07f, .148f, .70f));
                }
            }
            foreach (int side in new[] { -1, 1 })
            {
                edge.Box(new Vector3(0, .84f, side * .391f), new Vector3(1.22f, .041f, .080f));
                edge.Box(new Vector3(side * .591f, .84f, 0), new Vector3(.059f, .041f, .74f));
                foreach (int end in new[] { -1, 1 })
                {
                    iron.Box(new Vector3(side * .415f, .51f, end * .421f), new Vector3(.068f, .68f, .019f));
                    for (int nail = 0; nail < 4; nail++)
                        iron.Ovoid(new Vector3(side * .415f, .225f + nail * .16f, end * .434f), new Vector3(.018f, .018f, .009f), 6, 3);
                }
                iron.Box(new Vector3(side * .611f, .53f, 0), new Vector3(.018f, .062f, .25f));
                iron.Tube(new Vector3(side * .632f, .53f, -.085f), new Vector3(side * .632f, .53f, .085f), .017f, .017f, 6);
            }
            iron.Box(new Vector3(0, .72f, -.430f), new Vector3(.12f, .20f, .026f));
            edge.Disc(new Vector3(0, .737f, -.446f), Vector3.back, .025f, 8);
            // The 35-degree opening exposes cargo while the curved iron-bound
            // lid keeps the silhouette of an expedition chest from every view.
            for (int i = 0; i < 14; i++)
            {
                float a = i * Mathf.PI / 14, b = (i + 1) * Mathf.PI / 14;
                (i % 3 == 0 ? edge : wood).Quad(LidPoint(-.622f, a), LidPoint(.622f, a), LidPoint(.622f, b), LidPoint(-.622f, b));
                wood.Tri(LidPivot(-.622f), LidPoint(-.622f, a), LidPoint(-.622f, b));
                wood.Tri(LidPivot(.622f), LidPoint(.622f, b), LidPoint(.622f, a));
                foreach (float x in new[] { -.415f, .415f })
                    iron.Quad(LidPoint(x - .034f, a, .008f), LidPoint(x + .034f, a, .008f), LidPoint(x + .034f, b, .008f), LidPoint(x - .034f, b, .008f));
            }
            wood.Quad(LidPoint(-.622f, 0), LidPoint(-.622f, Mathf.PI), LidPoint(.622f, Mathf.PI), LidPoint(.622f, 0));
            if (id == "treasure_coin")
            {
                for (int i = 0; i < 18; i++)
                {
                    var p = new Vector3((i % 6 - 2.5f) * .153f, .854f + (i % 3) * .021f, (i / 6 - 1) * .21f);
                    contents.Tube(p, p + Vector3.up * .025f, .074f, .074f, 9);
                    edge.Ring(p + Vector3.up * .026f, Vector3.up, .052f, .004f, 9);
                }
                for (int i = 0; i < 3; i++)
                    contents.Trapezoid(new Vector3((i - 1) * .22f, .93f, .04f), new Vector3(.18f, .10f, .24f), .80f);
            }
            else if (id == "treasure_wood")
            {
                for (int i = 0; i < 5; i++)
                {
                    float z = i < 3 ? (i - 1) * .22f : (i - 3.5f) * .22f;
                    float y = i < 3 ? .94f : 1.115f;
                    var a = new Vector3(-.49f, y, z); var b = new Vector3(.49f, y + .015f, z);
                    contents.Tube(a, b, .103f, .099f, 9);
                    accent.Disc(a - Vector3.right * .002f, Vector3.left, .089f, 10);
                    accent.Disc(b + Vector3.right * .002f, Vector3.right, .087f, 10);
                    contents.Ring(a - Vector3.right * .004f, Vector3.left, .060f, .004f, 10);
                    contents.Ring(b + Vector3.right * .004f, Vector3.right, .057f, .004f, 10);
                }
                iron.Box(new Vector3(-.05f, 1.219f, 0), new Vector3(.043f, .018f, .45f));
            }
            else
            {
                for (int i = 0; i < 2; i++)
                {
                    var p = new Vector3(-.26f, .868f + i * .102f, .02f - i * .045f);
                    accent.Box(p, new Vector3(.35f, .072f, .41f));
                    contents.Box(p + Vector3.up * .043f, new Vector3(.39f, .017f, .45f));
                    contents.Box(p - Vector3.up * .043f, new Vector3(.39f, .017f, .45f));
                    contents.Box(p + Vector3.left * .188f, new Vector3(.022f, .095f, .45f));
                }
                var center = new Vector3(.235f, .96f, 0);
                accent.Box(center, new Vector3(.43f, .018f, .51f));
                accent.Tube(center + new Vector3(-.21f, .035f, -.245f), center + new Vector3(.21f, .035f, -.245f), .038f, .038f, 8);
                accent.Tube(center + new Vector3(-.21f, .035f, .245f), center + new Vector3(.21f, .035f, .245f), .038f, .038f, 8);
                Vector3 Map(float x, float z) => center + new Vector3(x, .012f, z);
                dark.Tube(Map(-.16f, -.12f), Map(-.03f, -.04f), .005f, .005f, 4);
                dark.Tube(Map(-.03f, -.04f), Map(.02f, .13f), .005f, .005f, 4);
                dark.Tube(Map(.02f, .13f), Map(.15f, .06f), .005f, .005f, 4);
                var rose = center + new Vector3(.102f, .014f, -.115f);
                for (int point = 0; point < 4; point++)
                {
                    float angle = point * Mathf.PI * .5f;
                    var tip = rose + new Vector3(Mathf.Cos(angle) * .061f, 0, Mathf.Sin(angle) * .061f);
                    var side = new Vector3(-Mathf.Sin(angle) * .017f, 0, Mathf.Cos(angle) * .017f);
                    contents.DoubleTri(rose - side, tip, rose + side);
                }
            }
            // A short swallowtail replaces the plain vertical stick. Its color
            // repeats the reward, while the old 2.25m marker envelope is kept.
            var pole = new Vector3(-.505f, 0, .31f);
            wood.Tube(pole + Vector3.up * .22f, pole + Vector3.up * 2.18f, .024f, .017f, 7);
            edge.Ovoid(pole + Vector3.up * 2.20f, new Vector3(.03f, .035f, .03f), 7, 4);
            var flag = id == "treasure_xp" ? contents : accent;
            var top = pole + new Vector3(.02f, 2.125f, 0);
            flag.DoubleQuad(top, top + Vector3.right * .30f, top + new Vector3(.23f, -.115f, 0), top + new Vector3(0, -.115f, 0));
            flag.DoubleQuad(top + new Vector3(0, -.115f, 0), top + new Vector3(.23f, -.115f, 0), top + new Vector3(.30f, -.23f, 0), top + new Vector3(0, -.23f, 0));
            m.Mark("Cache marker", pole + Vector3.up * 1.6f);
        }

        private static Vector3 LidPoint(float x, float angle, float surfaceOffset = 0)
        {
            float y = Mathf.Sin(angle) * (.22f + surfaceOffset), z = Mathf.Cos(angle) * (.42f + surfaceOffset) + .42f;
            const float cosine = .81915204f, sine = .57357644f;
            // Half-turn puts the hinge behind the front (-Z) lock, keeping
            // both the geometry winding and the exposed cargo facing the player.
            return new Vector3(-x, .83f + y * cosine + z * sine, .42f + y * sine - z * cosine);
        }
        private static Vector3 LidPivot(float x)
        {
            // Center of the semicircular end plate, transformed with the lid.
            return new Vector3(-x, .83f + .42f * .57357644f, .42f - .42f * .81915204f);
        }

        private readonly struct Section
        {
            public readonly Vector3 center; public readonly float width, height;
            public Section(Vector3 center, float width, float height) { this.center = center; this.width = width; this.height = height; }
        }

        private sealed class Model
        {
            private readonly Transform root;
            private readonly List<Batch> batches = new List<Batch>();
            private uint state;
            public Model(Transform root, uint seed) { this.root = root; state = seed; }
            public float Range(float min, float max)
            {
                state = unchecked(state * 1664525u + 1013904223u);
                return Mathf.Lerp(min, max, (state >> 8) * (1f / 16777216f));
            }
            public Batch Layer(string name, Color color)
            {
                foreach (var b in batches) if (b.color == color) return b;
                var batch = new Batch(name, color); batches.Add(batch); return batch;
            }
            public void Mark(string name, Vector3 position)
            {
                var marker = new GameObject(name).transform; marker.SetParent(root, false); marker.localPosition = position;
            }
            public void Flush()
            {
                foreach (var b in batches)
                {
                    if (b.vertices.Count == 0) continue;
                    // MeshPart also registers ownership, so pooling keeps the
                    // mesh and final world disposal releases its native memory.
                    ProceduralArt.MeshPart(root, b.name, b.vertices.ToArray(), b.triangles.ToArray(), b.color);
                }
            }
        }

        private sealed class Batch
        {
            public readonly string name; public readonly Color color;
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<int> triangles = new List<int>();
            public Batch(string name, Color color) { this.name = name; this.color = color; }
            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                int n = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
                triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int n = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
                triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 3);
            }
            public void DoubleTri(Vector3 a, Vector3 b, Vector3 c) { Tri(a, b, c); Tri(c, b, a); }
            public void DoubleQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Quad(a, b, c, d); Quad(d, c, b, a); }
            public void Box(Vector3 center, Vector3 size) { Trapezoid(center, size, 1); }
            public void Trapezoid(Vector3 center, Vector3 size, float topScale)
            {
                var a = center + Vector3.Scale(size, new Vector3(-.5f, -.5f, -.5f));
                var b = center + Vector3.Scale(size, new Vector3(.5f, -.5f, -.5f));
                var c = center + Vector3.Scale(size, new Vector3(.5f, -.5f, .5f));
                var d = center + Vector3.Scale(size, new Vector3(-.5f, -.5f, .5f));
                var e = center + Vector3.Scale(size, new Vector3(-topScale * .5f, .5f, -topScale * .5f));
                var f = center + Vector3.Scale(size, new Vector3(topScale * .5f, .5f, -topScale * .5f));
                var g = center + Vector3.Scale(size, new Vector3(topScale * .5f, .5f, topScale * .5f));
                var h = center + Vector3.Scale(size, new Vector3(-topScale * .5f, .5f, topScale * .5f));
                Quad(a, e, f, b); Quad(b, f, g, c); Quad(c, g, h, d); Quad(d, h, e, a);
                Quad(e, h, g, f); Quad(d, a, b, c);
            }
            private static void Basis(Vector3 axis, out Vector3 right, out Vector3 forward)
            {
                axis.Normalize();
                right = Vector3.Cross(Mathf.Abs(axis.y) > .9f ? Vector3.forward : Vector3.up, axis).normalized;
                forward = Vector3.Cross(axis, right);
            }
            public void Tube(Vector3 from, Vector3 to, float lowerRadius, float upperRadius, int sides)
            {
                var axis = to - from; Basis(axis, out var right, out var forward);
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                    var da = right * Mathf.Cos(a) + forward * Mathf.Sin(a);
                    var db = right * Mathf.Cos(b) + forward * Mathf.Sin(b);
                    Quad(from + da * lowerRadius, from + db * lowerRadius, to + db * upperRadius, to + da * upperRadius);
                    Tri(from, from + db * lowerRadius, from + da * lowerRadius);
                    Tri(to, to + da * upperRadius, to + db * upperRadius);
                }
            }
            public void Disc(Vector3 center, Vector3 normal, float radius, int sides)
            {
                Basis(normal, out var right, out var forward);
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                    Tri(center, center + (right * Mathf.Cos(a) + forward * Mathf.Sin(a)) * radius,
                        center + (right * Mathf.Cos(b) + forward * Mathf.Sin(b)) * radius);
                }
            }
            public void Ring(Vector3 center, Vector3 normal, float radius, float width, int sides)
            {
                Basis(normal, out var right, out var forward);
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                    var da = right * Mathf.Cos(a) + forward * Mathf.Sin(a);
                    var db = right * Mathf.Cos(b) + forward * Mathf.Sin(b);
                    Quad(center + da * radius, center + db * radius, center + db * (radius - width), center + da * (radius - width));
                }
            }
            public void Ovoid(Vector3 center, Vector3 radius, int sides, int rings)
            {
                Vector3 Point(int row, int column)
                {
                    float latitude = -Mathf.PI * .5f + row * Mathf.PI / rings;
                    float longitude = column * Mathf.PI * 2 / sides;
                    return center + Vector3.Scale(radius, new Vector3(Mathf.Cos(latitude) * Mathf.Cos(longitude), Mathf.Sin(latitude), Mathf.Cos(latitude) * Mathf.Sin(longitude)));
                }
                for (int row = 0; row < rings; row++)
                    for (int col = 0; col < sides; col++)
                    {
                        if (row == 0) Tri(center - Vector3.up * radius.y, Point(1, col), Point(1, col + 1));
                        else if (row == rings - 1) Tri(Point(row, col + 1), Point(row, col), center + Vector3.up * radius.y);
                        else Quad(Point(row, col + 1), Point(row, col), Point(row + 1, col), Point(row + 1, col + 1));
                    }
            }
            public void LoftZ(Section[] sections, int sides)
            {
                Vector3 Point(Section s, int i)
                {
                    float a = i * Mathf.PI * 2 / sides;
                    return s.center + new Vector3(Mathf.Cos(a) * s.width, Mathf.Sin(a) * s.height, 0);
                }
                for (int j = 0; j < sections.Length - 1; j++)
                    for (int i = 0; i < sides; i++)
                        Quad(Point(sections[j], i), Point(sections[j], i + 1), Point(sections[j + 1], i + 1), Point(sections[j + 1], i));
                for (int i = 0; i < sides; i++)
                {
                    Tri(sections[0].center, Point(sections[0], i + 1), Point(sections[0], i));
                    var last = sections[sections.Length - 1]; Tri(last.center, Point(last, i), Point(last, i + 1));
                }
            }
        }
    }
}
