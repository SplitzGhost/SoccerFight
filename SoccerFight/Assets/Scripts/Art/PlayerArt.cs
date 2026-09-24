using System;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>The rig parts, so a character swap can re-bind every sprite.</summary>
    public enum PlayerPart { Torso, Pelvis, Neck, Head, HairTuft, Thigh, Shin, Boot, BootGlow, UpperArm, Forearm, Hand, UpperArmNear, ForearmNear }

    /// <summary>Skeleton defaults shared by the art and the procedural rig (world units).</summary>
    public static class PlayerDims
    {
        public const float ThighLen = 0.28f;
        public const float ShinLen = 0.44f;
        public const float UpperArmLen = 0.3f;
        public const float ForearmLen = 0.19f;
        /// <summary>Ankle joint above the ground with the boot flat (the cut-out tool puts every ankle pivot this high).</summary>
        public const float AnkleHeight = 0.13f;
        /// <summary>Hip height of the reference body: gameplay contact points (juggle, header) are measured from it.</summary>
        public const float StandHip = 0.855f;
    }

    /// <summary>One character's finished body sprites, with the build and sport they were drawn for.</summary>
    public sealed class PlayerLook
    {
        public Sprite Torso, Pelvis, Neck, Head, HairTuft;
        public Sprite Thigh, Shin, Boot, BootGlow;
        public Sprite UpperArm, Forearm, Hand;
        /// <summary>The front arm when it differs (a compression sleeve on the throwing arm); null: same as the back arm.</summary>
        public Sprite UpperArmNear, ForearmNear;
        public PlayerBody Body = PlayerBody.Soccer;
        public Sport Sport;
    }

    /// <summary>
    /// The players' bodies, cut from the character sheets of the design (tools/newdesign/characters.js →
    /// Resources/Characters/&lt;id&gt;.png + .json). Every part pivots at its joint and its bone points straight
    /// down, so the procedural rig poses them like before; the json also carries the skeleton measured on the
    /// sheet (bone lengths, shoulder, neck, head and hair pivots), which is written into the character's
    /// PlayerBody. The moonlit rim and the grass bounce still come from the character shader at runtime.
    /// </summary>
    public static class PlayerArt
    {
        [Serializable]
        sealed class SpriteEntry
        {
            public string name;
            public int x, y, w, h;
            public float px, py;
        }

        [Serializable]
        sealed class BodyEntry
        {
            public float thigh, shin, upperArm, forearm, hipHeight, headTop, tuftFlex;
            public float[] shoulder, neck, head, tuft;
        }

        [Serializable]
        sealed class Sheet
        {
            public string id;
            public float ppu;
            public BodyEntry body;
            public SpriteEntry[] sprites;
        }

        static readonly PlayerLook[] looks = new PlayerLook[Characters.All.Length];
        static readonly bool[] measured = new bool[Characters.All.Length];
        static readonly System.Collections.Generic.List<int> requested = new System.Collections.Generic.List<int>();

        /// <summary>The look of the selected character (what a freshly built rig should wear).</summary>
        public static PlayerLook Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            for (int i = 0; i < looks.Length; i++) { looks[i] = null; measured[i] = false; }
            requested.Clear();
            Current = null;
        }

        public static bool Ready(int index) => index >= 0 && index < looks.Length && looks[index] != null;

        /// <summary>The look if it is loaded, else null (and it is queued).</summary>
        public static PlayerLook Peek(int index)
        {
            if (Ready(index)) return looks[index];
            Request(index);
            return null;
        }

        /// <summary>Queue a character's body; Pump loads it on a later frame.</summary>
        public static void Request(int index)
        {
            if (index < 0 || index >= looks.Length || Ready(index) || requested.Contains(index)) return;
            requested.Add(index);
        }

        /// <summary>Loads the next queued body (one per frame, so a page full of new characters never stalls). Call while a menu waits.</summary>
        public static void Pump()
        {
            while (requested.Count > 0 && Ready(requested[0])) requested.RemoveAt(0);
            if (requested.Count == 0) return;
            int i = requested[0];
            requested.RemoveAt(0);
            Get(i);
        }

        /// <summary>Measures every body (cheap: only the json) and loads the chosen character's sprites.</summary>
        public static void Build()
        {
            for (int i = 0; i < Characters.All.Length; i++) Measure(i);
            Characters.Load();
            Use(Characters.Index);
        }

        /// <summary>Writes the skeleton measured on the sheet into the character's PlayerBody.</summary>
        static Sheet Measure(int index)
        {
            var def = Characters.All[index];
            var json = Resources.Load<TextAsset>("Characters/" + def.Id);
            if (json == null) { Debug.LogError("[SoccerFight] Resources/Characters/" + def.Id + ".json fehlt (tools/newdesign/characters.js)"); return null; }
            var sheet = JsonUtility.FromJson<Sheet>(json.text);
            if (!measured[index])
            {
                measured[index] = true;
                var b = sheet.body;
                var body = def.Body;
                body.ThighLen = b.thigh; body.ShinLen = b.shin;
                body.UpperArmLen = b.upperArm; body.ForearmLen = b.forearm;
                body.Shoulder = V(b.shoulder); body.Neck = V(b.neck); body.Head = V(b.head); body.Tuft = V(b.tuft);
                body.TuftFlex = b.tuftFlex;
                body.HipHeight = b.hipHeight; body.HeadTop = b.headTop;
            }
            return sheet;
        }

        static Vector2 V(float[] a) => a != null && a.Length == 2 ? new Vector2(a[0], a[1]) : Vector2.zero;

        /// <summary>The finished sprite set of a character — loaded once, then cached.</summary>
        public static PlayerLook Get(int index)
        {
            index = Mathf.Clamp(index, 0, Characters.All.Length - 1);
            if (looks[index] != null) return looks[index];
            requested.Remove(index);

            var def = Characters.All[index];
            var look = new PlayerLook { Body = def.Body, Sport = def.Sport };
            looks[index] = look;
            var sheet = Measure(index);
            var tex = Resources.Load<Texture2D>("Characters/" + def.Id);
            if (sheet == null || tex == null) { Debug.LogError("[SoccerFight] Figurenbild fehlt: " + def.Id); return look; }
            tex.wrapMode = TextureWrapMode.Clamp;

            Sprite upperFar = null, foreFar = null;
            foreach (var e in sheet.sprites)
            {
                var s = Sprite.Create(tex, new Rect(e.x, e.y, e.w, e.h), new Vector2(e.px / e.w, e.py / e.h), sheet.ppu, 0, SpriteMeshType.FullRect);
                s.name = def.Id + " " + e.name;
                switch (e.name)
                {
                    case "Torso": look.Torso = s; break;
                    case "Pelvis": look.Pelvis = s; break;
                    case "Neck": look.Neck = s; break;
                    case "Head": look.Head = s; break;
                    case "HairTuft": look.HairTuft = s; break;
                    case "Thigh": look.Thigh = s; break;
                    case "Shin": look.Shin = s; break;
                    case "Boot": look.Boot = s; break;
                    case "UpperArm": look.UpperArm = s; break;
                    case "Forearm": look.Forearm = s; break;
                    case "Hand": look.Hand = s; break;
                    case "UpperArmFar": upperFar = s; break;
                    case "ForearmFar": foreFar = s; break;
                }
            }
            // a sleeve on the front arm only: the sheet's arm is the front one, the bare copy goes behind
            if (upperFar != null && foreFar != null)
            {
                look.UpperArmNear = look.UpperArm; look.ForearmNear = look.Forearm;
                look.UpperArm = upperFar; look.Forearm = foreFar;
            }
            look.BootGlow = BootGlow(look.Boot, def.Kit.Neon);
            return look;
        }

        /// <summary>A soft neon line under the sole (the speed upgrades turn it up), as long as the boot.</summary>
        static Sprite BootGlow(Sprite boot, Color neon)
        {
            float heel = -0.14f, toe = 0.2f;
            if (boot != null)
            {
                heel = -boot.pivot.x / boot.pixelsPerUnit;
                toe = (boot.rect.width - boot.pivot.x) / boot.pixelsPerUnit;
            }
            float y = -PlayerDims.AnkleHeight + 0.01f;
            var g = new SdfCanvas(new Rect(heel - 0.08f, y - 0.1f, toe - heel + 0.16f, 0.2f), 180f);
            g.Fill(p => Sdf.Capsule(p, new Vector2(heel + 0.05f, y), new Vector2(toe - 0.05f, y), 0.004f), neon, 0.07f);
            return g.ToSprite("BootGlow", Vector2.zero);
        }

        /// <summary>Makes a character's sprites the ones a new rig wears.</summary>
        public static void Use(int index) => Current = Get(index);

        public static Sprite SpriteOf(PlayerPart part) => SpriteOf(Current, part);

        public static Sprite SpriteOf(PlayerLook look, PlayerPart part)
        {
            if (look == null) return null;
            switch (part)
            {
                case PlayerPart.Torso: return look.Torso;
                case PlayerPart.Pelvis: return look.Pelvis;
                case PlayerPart.Neck: return look.Neck;
                case PlayerPart.Head: return look.Head;
                case PlayerPart.HairTuft: return look.HairTuft;
                case PlayerPart.Thigh: return look.Thigh;
                case PlayerPart.Shin: return look.Shin;
                case PlayerPart.Boot: return look.Boot;
                case PlayerPart.BootGlow: return look.BootGlow;
                case PlayerPart.UpperArm: return look.UpperArm;
                case PlayerPart.Forearm: return look.Forearm;
                case PlayerPart.UpperArmNear: return look.UpperArmNear != null ? look.UpperArmNear : look.UpperArm;
                case PlayerPart.ForearmNear: return look.ForearmNear != null ? look.ForearmNear : look.Forearm;
                default: return look.Hand;
            }
        }
    }
}
