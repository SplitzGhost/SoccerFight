using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SoccerFight
{
    /// <summary>
    /// A static batch of plants baked into one mesh (plus an optional additive glow mesh). Each plant
    /// is a subdivided quad whose vertices carry bend weights, so the foliage shader can bend it
    /// smoothly in the wind. Plants are drawn in the order they were added (back to front).
    /// </summary>
    public sealed class FoliageLayer
    {
        struct Plant
        {
            public FoliageArt.Variant V;
            public Vector2 Pos;
            public float Scale, Rotation, Stiffness, Push, Phase;
            public bool Flip;
            public Color Tint;
        }

        readonly List<Plant> plants = new List<Plant>();
        public Transform Root { get; private set; }

        public int Count => plants.Count;

        public void Add(FoliageArt.Variant v, Vector2 pos, float scale, Color tint, float stiffness = 1f, float push = 0f,
            bool flip = false, float rotation = 0f, float phase = -1f)
        {
            plants.Add(new Plant
            {
                V = v, Pos = pos, Scale = scale, Tint = tint, Stiffness = stiffness, Push = push, Flip = flip,
                Rotation = rotation, Phase = phase >= 0f ? phase : Mathf.Abs(MathUtil.Hash((int)(pos.x * 97f) + (int)(pos.y * 31f) + plants.Count * 7)) * 10f
            });
        }

        static Color32 Linear32(Color c)
        {
            Color l = c.linear;
            return new Color32((byte)(Mathf.Clamp01(l.r) * 255f), (byte)(Mathf.Clamp01(l.g) * 255f), (byte)(Mathf.Clamp01(l.b) * 255f), (byte)(Mathf.Clamp01(c.a) * 255f));
        }

        float Weight(FoliageArt.Variant v, float yLocal)
        {
            Rect u = v.Units;
            switch (v.Mode)
            {
                case FoliageArt.Mode.Rooted:
                    return Mathf.Pow(Mathf.Clamp01(yLocal / Mathf.Max(0.01f, u.yMax)), 1.5f);
                case FoliageArt.Mode.Hanging:
                    return Mathf.Pow(Mathf.Clamp01(-yLocal / Mathf.Max(0.01f, -u.yMin)), 1.3f);
                default:
                    return 0f;
            }
        }

        Vector2 Place(in Plant p, Vector2 local)
        {
            if (p.Flip) local.x = -local.x;
            local *= p.Scale;
            if (p.Rotation != 0f) local = MathUtil.Rotate(local, p.Rotation);
            return p.Pos + local;
        }

        public void Build(Transform parent, string name, int order, Material mat, Material glowMat = null, int glowOrder = 0)
        {
            Root = new GameObject(name).transform;
            Root.SetParent(parent, false);

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var bend = new List<Vector4>();
            var cols = new List<Color32>();
            var tris = new List<int>();

            var gVerts = new List<Vector3>();
            var gUvs = new List<Vector2>();
            var gBend = new List<Vector4>();
            var gCols = new List<Color32>();
            var gTris = new List<int>();
            Rect glowUv = FoliageArt.Glow.Uv;

            foreach (var p in plants)
            {
                var v = p.V;
                Rect u = v.Units, uv = v.Uv;
                float height = u.height * p.Scale;
                int rows = v.Mode == FoliageArt.Mode.Static ? 1 : (height > 1.4f ? 6 : height > 0.6f ? 4 : 3);
                float stiff = v.Sway * p.Stiffness * height * 0.35f;
                Color32 col = Linear32(p.Tint);
                int start = verts.Count;
                for (int r = 0; r <= rows; r++)
                {
                    float ty = r / (float)rows;
                    float yl = Mathf.Lerp(u.yMin, u.yMax, ty);
                    float w = Weight(v, yl);
                    for (int c = 0; c < 2; c++)
                    {
                        float xl = c == 0 ? u.xMin : u.xMax;
                        verts.Add(Place(p, new Vector2(xl, yl)));
                        uvs.Add(new Vector2(c == 0 ? uv.xMin : uv.xMax, Mathf.Lerp(uv.yMin, uv.yMax, ty)));
                        bend.Add(new Vector4(w, p.Phase, stiff, p.Push));
                        cols.Add(col);
                    }
                }
                for (int r = 0; r < rows; r++)
                {
                    int i0 = start + r * 2;
                    tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 3);
                    tris.Add(i0); tris.Add(i0 + 3); tris.Add(i0 + 1);
                }

                if (glowMat == null) continue;
                foreach (var g in v.Glows)
                {
                    Vector2 c = Place(p, g.Pos);
                    float s = g.Size * p.Scale * 0.5f;
                    float w = Weight(v, g.Pos.y);
                    Color gc = g.Color;
                    gc.a = 0.42f * p.Tint.a;
                    Color32 gcol = Linear32(gc);
                    int gs = gVerts.Count;
                    gVerts.Add(c + new Vector2(-s, -s)); gVerts.Add(c + new Vector2(s, -s));
                    gVerts.Add(c + new Vector2(-s, s)); gVerts.Add(c + new Vector2(s, s));
                    gUvs.Add(new Vector2(glowUv.xMin, glowUv.yMin)); gUvs.Add(new Vector2(glowUv.xMax, glowUv.yMin));
                    gUvs.Add(new Vector2(glowUv.xMin, glowUv.yMax)); gUvs.Add(new Vector2(glowUv.xMax, glowUv.yMax));
                    for (int k = 0; k < 4; k++) { gBend.Add(new Vector4(w, p.Phase, stiff, p.Push)); gCols.Add(gcol); }
                    gTris.Add(gs); gTris.Add(gs + 2); gTris.Add(gs + 3);
                    gTris.Add(gs); gTris.Add(gs + 3); gTris.Add(gs + 1);
                }
            }

            MakeRenderer(Root, name, order, mat, verts, uvs, bend, cols, tris);
            if (glowMat != null && gVerts.Count > 0) MakeRenderer(Root, name + " Glow", glowOrder, glowMat, gVerts, gUvs, gBend, gCols, gTris);
            plants.Clear();
        }

        static void MakeRenderer(Transform parent, string name, int order, Material mat, List<Vector3> verts, List<Vector2> uvs,
            List<Vector4> bend, List<Color32> cols, List<int> tris)
        {
            var go = new GameObject(name + " Mesh");
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, bend);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0, true);
            var b = mesh.bounds;
            b.Expand(new Vector3(3f, 3f, 0f));
            mesh.bounds = b;
            mesh.UploadMeshData(true);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingOrder = order;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        /// <summary>Foliage material for one layer: fog, depth darkening and wind scale are per layer.</summary>
        public static Material MakeMaterial(string name, float fog, float windScale, bool glow = false, float intensity = 1f, Color? tint = null)
        {
            var m = new Material(Shader.Find("SoccerFight/Foliage")) { name = name };
            m.mainTexture = FoliageArt.Atlas;
            m.SetFloat("_Intensity", glow ? 2.4f * intensity : intensity);
            m.SetFloat("_Glow", glow ? 1f : 0f);
            m.SetFloat("_FogAmount", fog);
            m.SetColor("_FogColor", Color.Lerp(Palette.Fog, Palette.MidBottom, 0.35f));
            m.SetColor("_Tint", tint ?? Color.white);
            m.SetFloat("_WindScale", windScale);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", glow ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            return m;
        }
    }
}
