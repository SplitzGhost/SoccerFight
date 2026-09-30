using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// Die gemalte Figur der Spielerauswahl als Ganzes, über ein feines Gitter weich verformt – das Bild wird nie
    /// zerschnitten. Alle Bewegungen folgen dem Körper: die Sohlen bleiben am Boden, das Gewicht verlagert sich
    /// (oben weiter als unten), der Brustkorb hebt und weitet sich beim Atmen, der Kopf nickt um den Halsansatz,
    /// Zopf und Flechten schwingen um ihre Wurzel nach. Die Werte setzt die Seite pro Bild (<see cref="Pose"/>).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FigureWarp : MaskableGraphic
    {
        const float Cell = 15f;   // Gitterweite in Bildpunkten des Entwurfs

        Texture texture;
        // Gelenke im eigenen Rechteck (Ursprung Mitte, y nach oben)
        float ground, hip, headTop;
        Vector2 chest, neck, head;
        Vector2[] swingRoot, swingTip;
        float[] swingRadius, swingFlex, swingAngle;

        float breath, sway, nod, squash, lean;

        public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

        public int SwingCount => swingAngle != null ? swingAngle.Length : 0;
        /// <summary>Höhe des Kopfes über den Sohlen (Bildpunkte): so weit oben wirkt die Gewichtsverlagerung voll.</summary>
        public float Height => headTop - ground;

        public void Set(Texture tex, PlayerSelectArt.Figure f)
        {
            texture = tex;
            var rt = rectTransform;
            rt.sizeDelta = new Vector2(f.w, f.h);
            rt.anchoredPosition = PlayerSelectArt.Center(f);
            Vector2 c = new Vector2(f.x + f.w * 0.5f, f.y + f.h * 0.5f);
            Vector2 Local(float x, float y) => new Vector2(x - c.x, c.y - y);
            ground = Local(0f, f.ground).y;
            hip = Local(0f, f.hip).y;
            chest = Local(f.chestX, f.chestY);
            neck = Local(f.neckX, f.neckY);
            head = Local(f.headX, f.headY);
            headTop = f.h * 0.5f;
            int n = f.swing != null ? f.swing.Length : 0;
            swingRoot = new Vector2[n]; swingTip = new Vector2[n];
            swingRadius = new float[n]; swingFlex = new float[n]; swingAngle = new float[n];
            for (int i = 0; i < n; i++)
            {
                swingRoot[i] = Local(f.swing[i].rootX, f.swing[i].rootY);
                swingTip[i] = Local(f.swing[i].tipX, f.swing[i].tipY);
                swingRadius[i] = f.swing[i].radius;
                swingFlex[i] = f.swing[i].flex;
            }
            SetMaterialDirty();
            SetVerticesDirty();
        }

        /// <param name="breath">−1 ausgeatmet … 1 eingeatmet</param>
        /// <param name="sway">seitliche Verlagerung des Kopfes in Bildpunkten (die Sohlen bleiben stehen)</param>
        /// <param name="nod">Kopfneigung in Grad um den Halsansatz</param>
        /// <param name="squash">Stauchung von den Sohlen aus (positiv: gedrungener und breiter), z. B. beim Aufsetzen</param>
        /// <param name="lean">Vorlage des Oberkörpers in Bildpunkten an der Schulter (Hüfte bleibt)</param>
        public void Pose(float breath, float sway, float nod, float squash, float lean)
        {
            this.breath = breath; this.sway = sway; this.nod = nod; this.squash = squash; this.lean = lean;
            SetVerticesDirty();
        }

        /// <summary>Auslenkung eines Haarteils in Grad.</summary>
        public void Swing(int i, float degrees) { if (i < SwingCount) swingAngle[i] = degrees; }

        Vector2 Deform(Vector2 p)
        {
            float tall = Mathf.Max(1f, headTop - ground);
            // Haarteile: um ihre Wurzel gedreht, zur Spitze hin stärker; wirkt nur nahe der Strecke Wurzel–Spitze
            for (int i = 0; i < swingAngle.Length; i++)
            {
                Vector2 ab = swingTip[i] - swingRoot[i];
                float len2 = Mathf.Max(1f, ab.sqrMagnitude);
                float t = Mathf.Clamp01(Vector2.Dot(p - swingRoot[i], ab) / len2);
                float d = (p - (swingRoot[i] + ab * t)).magnitude;
                float w = 1f - MathUtil.Smooth01((d - swingRadius[i]) / (swingRadius[i] * 0.7f));
                if (w <= 0f) continue;
                float bend = t * t * (3f - 2f * t);
                p = swingRoot[i] + MathUtil.Rotate(p - swingRoot[i], swingAngle[i] * swingFlex[i] * bend * w);
            }
            // Kopf: nickt um den Halsansatz (unterhalb des Halses nichts)
            float hw = MathUtil.Smooth01((p.y - neck.y + 6f) / 34f);
            if (hw > 0f) p = neck + MathUtil.Rotate(p - neck, nod * hw);
            // Atmen: ab der Hüfte aufwärts hebt sich der Körper, der Brustkorb weitet sich
            float up = MathUtil.Smooth01((p.y - hip) / Mathf.Max(1f, chest.y - hip));
            p.y += breath * 2.8f * up;
            float span = Mathf.Max(20f, (neck.y - hip) * 0.45f);
            float rib = Mathf.Exp(-(p.y - chest.y) * (p.y - chest.y) / (span * span));
            p.x += (p.x - chest.x) * breath * 0.018f * rib;
            // Vorlage: der Oberkörper kippt leicht aus der Hüfte
            p.x += lean * up;
            // Gewicht: die Sohlen bleiben stehen, nach oben hin wandert der Körper zur Seite
            float u = Mathf.Clamp01((p.y - ground) / tall);
            p.x += sway * u * u * (3f - 2f * u);
            // Stauchen und Strecken von den Sohlen aus (Fläche bleibt etwa erhalten)
            p.y = ground + (p.y - ground) * (1f - squash);
            p.x = chest.x + (p.x - chest.x) * (1f + squash * 0.55f);
            return p;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (texture == null || swingAngle == null) return;
            Rect r = rectTransform.rect;
            int nx = Mathf.Max(2, Mathf.CeilToInt(r.width / Cell)), ny = Mathf.Max(2, Mathf.CeilToInt(r.height / Cell));
            Color32 c = color;
            for (int y = 0; y <= ny; y++)
                for (int x = 0; x <= nx; x++)
                {
                    float u = x / (float)nx, v = y / (float)ny;
                    Vector2 p = new Vector2(r.xMin + r.width * u, r.yMin + r.height * v);
                    vh.AddVert(Deform(p), c, new Vector2(u, v));
                }
            int row = nx + 1;
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    int i = y * row + x;
                    vh.AddTriangle(i, i + row, i + row + 1);
                    vh.AddTriangle(i, i + row + 1, i + 1);
                }
        }
    }
}
