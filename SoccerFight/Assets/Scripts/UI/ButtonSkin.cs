using UnityEngine;

namespace SoccerFight
{
    /// <summary>Gemalte Originalplatten mit gravierten Spiralen und plastischen Menü-Symbolen.</summary>
    public static class ButtonSkin
    {
        public static Sprite Plate, Panel, Frame, Socket;
        public static readonly Color Slate = new Color(0.36f, 0.59f, 0.67f, 1f);
        public static readonly Color Gold = new Color(0.86f, 0.65f, 0.30f, 1f);
        static readonly Sprite[] menu = new Sprite[16], sport = new Sprite[16];
        static bool built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { built = false; }

        public static void Build()
        {
            if (built) return;
            built = true;
            ExactButtonArt.Build();
            Plate = ExactButtonArt.Plate();
            Panel = ExactButtonArt.Get("panel-border");
            Frame = ExactButtonArt.Get("frame-only");
            Socket = Panel;
            for (int i = 0; i < menu.Length; i++) menu[i] = ExactButtonArt.Get("menu-icon-" + i);
            for (int i = 0; i < sport.Length; i++) sport[i] = ExactButtonArt.Get("sport-icon-" + i);
            sport[12] = UiArt.IconWhistle;
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
