using UnityEngine;

namespace SoccerFight
{
    /// <summary>Gemeinsame, ruhige Facetten und gemalte Symbole für alle Menübuttons.</summary>
    public static class ButtonSkin
    {
        public static Sprite Plate, Panel, Frame, Socket;
        public static readonly Color Slate = new Color(0.24f, 0.40f, 0.47f, 1f);
        public static readonly Color Gold = new Color(0.86f, 0.65f, 0.30f, 1f);
        static readonly Sprite[] menu = new Sprite[16], sport = new Sprite[16];
        static bool built;
        [System.Serializable] sealed class AtlasLayout { public AtlasRect[] icons; }
        [System.Serializable] sealed class AtlasRect { public float x, y, width, height; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { built = false; }

        public static void Build()
        {
            if (built) return;
            built = true;
            Plate = Surface("ButtonPlate", false, false);
            Panel = Surface("ButtonPanel", true, false);
            Frame = Surface("ButtonFrame", false, true);
            Socket = Panel;
            Load("menu-icons", menu);
            Load("sport-icons", sport);
        }

        static float Shape(Vector2 p, float half, float cut) => Mathf.Max(Mathf.Max(Mathf.Abs(p.x) - half,
            Mathf.Abs(p.y) - half), (Mathf.Abs(p.x) + Mathf.Abs(p.y) - half * 2f + cut) * 0.7071f);

        static Sprite Surface(string name, bool inset, bool frame)
        {
            var c = new SdfCanvas(new Rect(-40f, -40f, 80f, 80f), 2f);
            c.Fill(p => Shape(p, 39f, 10f), p =>
            {
                float edge = -Shape(p, 39f, 10f);
                float shade = p.y > 0f ? 0.96f : 0.46f;
                if (Mathf.Abs(p.x) > Mathf.Abs(p.y)) shade = p.x < 0f ? 0.82f : 0.61f;
                if (edge > 5f) shade = inset ? 0.21f : 0.77f + p.y * 0.0013f;
                float alpha = frame && edge > 3f ? 0f : 1f;
                return new Color(shade, shade, shade, alpha);
            });
            return UiArt.ToUi(c, name, new Vector4(36f, 36f, 36f, 36f));
        }

        static void Load(string name, Sprite[] icons)
        {
            var tex = Resources.Load<Texture2D>("UiButtons/" + name);
            if (tex == null) { Debug.LogError("Buttonatlas fehlt: " + name); return; }
            var data = Resources.Load<TextAsset>("UiButtons/" + name + "-layout");
            var layout = data == null ? null : JsonUtility.FromJson<AtlasLayout>(data.text);
            int cell = tex.width / 4;
            for (int i = 0; i < icons.Length; i++)
            {
                var rect = new Rect(i % 4 * cell, (3 - i / 4) * cell, cell, cell);
                if (layout != null && layout.icons != null && i < layout.icons.Length)
                {
                    var r = layout.icons[i];
                    rect = new Rect(r.x, r.y, r.width, r.height);
                }
                icons[i] = Sprite.Create(tex, rect,
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                icons[i].name = name + "-" + i;
            }
        }

        public static Sprite Menu(int i) { Build(); return menu[i]; }
        public static Sprite Sport(int i) { Build(); return sport[i]; }
        public static Sprite Painted(Sprite original)
        {
            Build();
            if (original == UiArt.IconShot) return menu[14];
            if (original == UiArt.IconThrow) return menu[15];
            if (original == UiArt.IconPower) return sport[3];
            if (original == UiArt.IconFlick || original == UiArt.IconPunt) return sport[0];
            if (original == UiArt.IconBicycle || original == UiArt.IconAirKick) return sport[14];
            if (original == UiArt.IconWhistle) return sport[12];
            if (original == UiArt.IconWall || original == UiArt.IconBlock) return sport[1];
            if (original == UiArt.IconDash || original == UiArt.IconFastBreak) return sport[8];
            return original;
        }
        public static Sprite Nav(int page) => Menu(page == MenuPage.Characters ? 1 : page == MenuPage.Shop ? 2 :
            page == MenuPage.Events ? 3 : page == MenuPage.Ranking ? 4 : page == MenuPage.Settings ? 5 : 0);

        public static void ApplyMenuArt()
        {
            Build();
            MenuArt.Body = Plate; MenuArt.CardBody = Panel; MenuArt.Frame = Frame;
            MenuArt.IconPlay = menu[0]; MenuArt.IconShop = menu[2]; MenuArt.IconEvents = menu[3];
            MenuArt.IconTrophy = menu[4]; MenuArt.IconGear = menu[5]; MenuArt.IconFriends = menu[6];
            MenuArt.IconInfo = menu[7]; MenuArt.IconPower = menu[8]; MenuArt.IconBack = menu[9];
            MenuArt.IconSwap = menu[10]; MenuArt.IconCheck = menu[11]; MenuArt.IconLock = menu[12];
            MenuArt.IconPlus = menu[13]; MenuArt.IconSoccer = menu[14]; MenuArt.IconHoops = menu[15];
            MenuArt.IconStriker = sport[0]; MenuArt.IconDefender = sport[1]; MenuArt.IconSkiller = sport[2];
            MenuArt.IconStar = sport[2]; MenuArt.IconSkills = sport[3]; MenuArt.IconBoxing = sport[4];
            MenuArt.IconTennis = sport[5]; MenuArt.IconMode = sport[6];
        }
    }
}
