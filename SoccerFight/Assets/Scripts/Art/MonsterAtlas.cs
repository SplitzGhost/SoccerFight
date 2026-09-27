using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Loads one (stage, look) monster atlas cut by tools/newdesign/monsters.js from
    /// Resources/Monsters/&lt;stage&gt;/&lt;look&gt;.png/.json: named part sprites plus a few anchor points in
    /// world units, ready to slot straight into a <see cref="LookDef"/> exactly where the SDF generator used
    /// to put its own sprites. Cached per (stage, look); <see cref="Unload"/> releases it (called by
    /// <see cref="MonsterArt.Trim"/> for cutout looks instead of the SDF path's <see cref="LookDef.Owned"/>
    /// destroy loop — the PNG/rim here are Resources-loaded assets, freed via Resources.UnloadAsset, not
    /// Object.Destroy; only the Sprite.Create() sprites themselves are runtime objects that need Destroy).
    /// </summary>
    public static class MonsterAtlas
    {
        [System.Serializable] class SpriteEntry { public string name; public float x, y, w, h, px, py; }
        [System.Serializable] class AnchorEntry { public string name; public float x, y; }
        [System.Serializable] class Sheet { public float ppu; public SpriteEntry[] sprites; public AnchorEntry[] anchors; }

        public sealed class Set
        {
            public float Ppu;
            internal readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
            readonly Dictionary<string, Vector2> anchors = new Dictionary<string, Vector2>();
            internal Texture2D Tex, Rim;
            public Sprite Sprite(string name) => sprites.TryGetValue(name, out var s) ? s : null;
            public Vector2 Anchor(string name) => anchors.TryGetValue(name, out var p) ? p : Vector2.zero;
            internal void AddSprite(string name, Sprite s) => sprites[name] = s;
            internal void AddAnchor(string name, Vector2 p) => anchors[name] = p;
        }

        static readonly Dictionary<string, Set> cache = new Dictionary<string, Set>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cache.Clear(); }

        static string Key(string stage, string look) => stage + "/" + look;

        /// <summary>Null if this (stage, look) pair hasn't been cut yet — callers fall back to the SDF body.</summary>
        public static Set Load(string stage, string look)
        {
            string key = Key(stage, look);
            if (cache.TryGetValue(key, out var cached)) return cached;
            string path = "Monsters/" + stage + "/" + look;
            var json = Resources.Load<TextAsset>(path);
            var tex = Resources.Load<Texture2D>(path);
            if (json == null || tex == null) return null;
            tex.wrapMode = TextureWrapMode.Clamp;
            // the whole cut-out's silhouette in the same layout: the shared character shader lights only
            // the true outline, not the seams between separately cut parts
            var rim = Resources.Load<Texture2D>(path + "_rim");
            var secondary = rim != null ? new[] { new SecondarySpriteTexture { name = "_RimMask", texture = rim } } : null;
            var sheet = JsonUtility.FromJson<Sheet>(json.text);
            var set = new Set { Ppu = sheet.ppu, Tex = tex, Rim = rim };
            foreach (var e in sheet.sprites)
            {
                var s = UnityEngine.Sprite.Create(tex, new Rect(e.x, e.y, e.w, e.h), new Vector2(e.px / e.w, e.py / e.h),
                    sheet.ppu, 0, SpriteMeshType.FullRect, Vector4.zero, false, secondary);
                s.name = look + " " + e.name;
                set.AddSprite(e.name, s);
            }
            foreach (var a in sheet.anchors) set.AddAnchor(a.name, new Vector2(a.x, a.y));
            cache[key] = set;
            return set;
        }

        /// <summary>Releases a loaded atlas (no-op if it was never loaded). Safe to call even mid-game since
        /// callers only hold onto the Sprite references while their LookDef is still the active one.</summary>
        public static void Unload(string stage, string look)
        {
            string key = Key(stage, look);
            if (!cache.TryGetValue(key, out var set)) return;
            cache.Remove(key);
            foreach (var s in set.sprites.Values) if (s != null) Object.Destroy(s);
            if (set.Tex != null) Resources.UnloadAsset(set.Tex);
            if (set.Rim != null) Resources.UnloadAsset(set.Rim);
        }
    }
}
