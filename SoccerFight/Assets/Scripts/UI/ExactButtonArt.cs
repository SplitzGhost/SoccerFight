using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>Unveränderte Ausschnitte der freigegebenen Probebilder, mit Herkunft je Element.</summary>
    public static class ExactButtonArt
    {
        [Serializable] sealed class Layout { public Entry[] items; public int pages; }
        [Serializable] sealed class Entry
        {
            public string name;
            public int page;
            public float x, y, width, height, border;
            public float[] sourceRect;
        }
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Rect> references = new Dictionary<string, Rect>();
        static bool built;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { built = false; sprites.Clear(); references.Clear(); }
        public static void Build()
        {
            if (built) return;
            built = true;
            var source = Resources.Load<TextAsset>("UiButtons/Exact/layout");
            if (source == null) { Debug.LogError("Ausschnitte der Probebilder fehlen"); return; }
            var layout = JsonUtility.FromJson<Layout>(source.text);
            var pages = new Texture2D[layout.pages];
            for (int i = 0; i < pages.Length; i++) pages[i] = Resources.Load<Texture2D>("UiButtons/Exact/atlas-" + i);
            foreach (var e in layout.items)
            {
                var s = Sprite.Create(pages[e.page], new Rect(e.x, e.y, e.width, e.height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.one * e.border);
                s.name = "Probebild " + e.name;
                sprites[e.name] = s;
                references[e.name] = new Rect(e.sourceRect[0], e.sourceRect[1], e.sourceRect[2] - e.sourceRect[0], e.sourceRect[3] - e.sourceRect[1]);
            }
        }
        public static Sprite Get(string key)
        {
            Build(); return key != null && sprites.TryGetValue(key, out var s) ? s : null;
        }
        public static Sprite Action(string label) => Get("action-" + label);
        public static Rect ReferenceRect(string key) { Build(); return references[key]; }
        public static Sprite Plate(bool gold = false, bool danger = false) => Get(danger ? "plate-coral" : gold ? "plate-gold" : "plate-petrol");
        public static Sprite Navigation(int page) => Get("nav-" + (page == MenuPage.Characters ? "chars" : page == MenuPage.Shop ? "shop" :
            page == MenuPage.Events ? "events" : page == MenuPage.Ranking ? "ranking" : page == MenuPage.Settings ? "settings" : "home"));
    }
}
