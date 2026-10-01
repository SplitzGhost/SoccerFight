using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Die Grafik der Spielerauswahl (tools/newdesign/playerselect.js → Resources/Menu/Players): gemeinsame Kulisse,
    /// pro Spieler ein Ausschnitt mit seinem Steinbild am Himmel, die freigestellte Figur und die Einzelteile der
    /// Oberfläche. layout.json sagt, wo alles im 1672 × 941 großen Entwurf liegt (Bildpunkte, Ursprung oben links)
    /// und wo die Gelenke der Figuren sitzen. Figuren und Ausschnitte werden erst geladen, wenn ein Spieler gezeigt
    /// wird, und mit <see cref="Release"/> wieder freigegeben, sobald die Seite zu ist.
    /// </summary>
    public static class PlayerSelectArt
    {
        [System.Serializable] public sealed class Box { public float x, y, w, h; }
        [System.Serializable] public sealed class Swing { public float rootX, rootY, tipX, tipY, radius, flex; }
        [System.Serializable]
        public sealed class Figure
        {
            public string id;
            public float x, y, w, h, ground, hip, chestX, chestY, neckX, neckY, headX, headY;
            public Swing[] swing;
            /// <summary>Eigene Kulisse (die Boxer: "scene_box"); leer: die gemeinsame.</summary>
            public string scene;
        }
        [System.Serializable] public sealed class Part { public string name; public float x, y, w, h; }
        [System.Serializable] sealed class Layout { public int width, height; public Box back; public Figure[] figures; public Part[] parts; }

        const string Folder = "Menu/Players/";
        static Layout layout;
        static readonly Dictionary<string, Part> parts = new Dictionary<string, Part>();
        static readonly Dictionary<string, Figure> figures = new Dictionary<string, Figure>();
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { layout = null; parts.Clear(); figures.Clear(); sprites.Clear(); textures.Clear(); }

        static void Load()
        {
            if (layout != null) return;
            var source = Resources.Load<TextAsset>(Folder + "layout");
            if (source == null) Debug.LogError("Spielerauswahl: Grafik fehlt (tools/newdesign/playerselect.js)");
            layout = source != null ? JsonUtility.FromJson<Layout>(source.text) : new Layout { width = 1672, height = 941, back = new Box(), figures = new Figure[0], parts = new Part[0] };
            foreach (var p in layout.parts) parts[p.name] = p;
            foreach (var f in layout.figures) figures[f.id] = f;
        }

        /// <summary>Größe des Entwurfs; alle Maße der Seite sind seine Bildpunkte.</summary>
        public static Vector2 Size { get { Load(); return new Vector2(layout.width, layout.height); } }
        public static Box Back { get { Load(); return layout.back; } }

        /// <summary>Bildpunkt des Entwurfs (Ursprung oben links) → Position um die Bildmitte, y nach oben.</summary>
        public static Vector2 At(float x, float y) { var s = Size; return new Vector2(x - s.x * 0.5f, s.y * 0.5f - y); }
        public static Vector2 Center(Box b) => At(b.x + b.w * 0.5f, b.y + b.h * 0.5f);
        public static Vector2 Center(Part p) => At(p.x + p.w * 0.5f, p.y + p.h * 0.5f);
        public static Vector2 Center(Figure f) => At(f.x + f.w * 0.5f, f.y + f.h * 0.5f);

        public static Part Get(string name) { Load(); return parts.TryGetValue(name, out var p) ? p : null; }
        public static Figure FigureOf(string id) { Load(); return figures.TryGetValue(id, out var f) ? f : null; }

        /// <summary>Ein Einzelteil als Sprite (bleibt geladen: die Teile sind klein).</summary>
        public static Sprite Sprite(string name)
        {
            if (sprites.TryGetValue(name, out var s) && s != null) return s;
            var tex = Resources.Load<Texture2D>(Folder + name);
            if (tex == null) return null;
            // Crunch streckt die Textur auf Zweierpotenzen: die Größe gibt immer das Layout vor
            s = UnityEngine.Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            s.name = "Spielerauswahl " + name;
            sprites[name] = s;
            return s;
        }

        /// <summary>Kulisse, Ausschnitt oder Figur (groß: nur so lange geladen, wie die Seite offen ist).</summary>
        public static Texture2D Texture(string name)
        {
            if (textures.TryGetValue(name, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>(Folder + name);
            if (t != null) textures[name] = t;
            return t;
        }

        /// <summary>Die großen Bilder wieder freigeben (die Seite ist zu).</summary>
        public static void Release()
        {
            foreach (var t in textures.Values) if (t != null) Resources.UnloadAsset(t);
            textures.Clear();
        }
    }
}
