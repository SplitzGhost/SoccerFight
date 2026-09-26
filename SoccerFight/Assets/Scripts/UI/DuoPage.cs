using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// DUO: play a run together. One player opens a room and gets a six-letter code, the other types
    /// the code in and joins. In the room both see each other's player; the host kicks off the run.
    /// The room stays open between runs until someone leaves it.
    /// </summary>
    public sealed class DuoPage
    {
        enum View { Choose, Opening, Hosting, Joining, Lobby }

        MenuNav nav;
        SubPage page;
        CanvasGroup choose, hosting, joining, lobby;
        float chooseA, hostingA, joiningA, lobbyA;
        View view = View.Choose;

        // choose
        ChunkButton createButton, joinButton;
        readonly Image[] joinBoxes = new Image[CodeLength];
        readonly TextMeshProUGUI[] joinLetters = new TextMeshProUGUI[CodeLength];
        Image caret;
        string typed = "";
        // hosting
        readonly Image[] codeBoxes = new Image[CodeLength];
        readonly TextMeshProUGUI[] codeLetters = new TextMeshProUGUI[CodeLength];
        TextMeshProUGUI hostStatus;
        ChunkButton closeButton;
        // joining
        TextMeshProUGUI joinStatus;
        ChunkButton cancelButton;
        // lobby
        MenuFigure me, partner;
        int meShown = -1, partnerShown = -1;
        TextMeshProUGUI meName, meRole, partnerName, partnerRole, lobbyCode, lobbyHint;
        ChunkButton startButton, leaveButton;
        // messages
        TextMeshProUGUI message;
        float messageT = 99f;
        float time;
        bool typing;

        const int CodeLength = 6;
        static readonly Color Mint = Palette.DashMint;

        public SubPage Page => page;

        public void Build(RectTransform parent, MenuNav menu)
        {
            nav = menu;
            page = new SubPage(parent, MenuPage.Friends, "DUO", "ZU ZWEIT SPIELEN  ·  RAUM ERSTELLEN ODER BEITRETEN", Mint, nav.Register, nav.Back);
            var c = page.Content;
            BuildChoose(c);
            BuildHosting(c);
            BuildJoining(c);
            BuildLobby(c);
            message = MetaUi.Text(c, "Message", "", 20f, MetaUi.Danger, new Vector2(0f, -420f), new Vector2(1300f, 30f));
            message.alpha = 0f;
        }

        CanvasGroup Panel(RectTransform parent, string name)
        {
            var rt = UiKit.Node(name, parent, Vector2.zero, new Vector2(1600f, 900f));
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            return g;
        }

        MenuTarget Target(string id, ChunkButton b, System.Action action, System.Func<float> visible)
        {
            var t = new MenuTarget { Id = id, Root = b.Root, Size = b.Size, Page = MenuPage.Friends, Button = b, Action = action, Accent = b.Color, Visible = visible };
            nav.Register(t);
            return t;
        }

        void Boxes(Transform parent, Image[] boxes, TextMeshProUGUI[] letters, Vector2 center, Vector2 box, float gap, float font)
        {
            float step = box.x + gap;
            for (int i = 0; i < CodeLength; i++)
            {
                Vector2 p = center + new Vector2((i - (CodeLength - 1) * 0.5f) * step, 0f);
                UiKit.Img("BoxShade", parent, UiArt.Glow, new Color(0f, 0.01f, 0.03f, 0.4f), p + new Vector2(0f, -6f), box * 1.3f);
                boxes[i] = UiKit.Img("Box" + i, parent, ExactButtonArt.Get("code-cell"), Color.white, p, box, Image.Type.Sliced);
                letters[i] = MenuArt.Label("Letter" + i, parent, "", font, Color.white, p + new Vector2(0f, 2f), box, TextAlignmentOptions.Center, 0f);
            }
        }

        // ------------------------------------------------------------------ build

        void BuildChoose(RectTransform c)
        {
            choose = Panel(c, "Choose");
            var root = choose.transform;

            createButton = new ChunkButton(root, "Create", new Vector2(-380f, 30f), new Vector2(714f, 439f), Mint, "RAUM ERSTELLEN", 40f, MenuArt.IconFriends, 120f)
                .IconTop("DU BIST HOST  ·  DEIN FREUND TRITT MIT DEINEM CODE BEI");
            Target("duoCreate", createButton, Create, () => chooseA);

            var join = UiKit.Node("Join", root, new Vector2(380f, 30f), new Vector2(700f, 439f));
            UiKit.Img("Plate", join, ExactButtonArt.Get("duo-join-panel"), Color.white, Vector2.zero, new Vector2(700f, 439f));
            Boxes(join, joinBoxes, joinLetters, new Vector2(0f, 6f), new Vector2(90f, 109f), 10f, 42f);
            caret = UiKit.Img("Caret", join, null, Mint, Vector2.zero, new Vector2(34f, 4f));
            joinButton = new ChunkButton(join, "JoinGo", new Vector2(0f, -124f), new Vector2(450f, 107f), MenuArt.Accent, "BEITRETEN", 32f, MenuArt.IconPlay, 28f, true);
            joinButton.IconLeft(40f);
            Target("duoJoin", joinButton, Join, () => chooseA);

            MetaUi.Text(root, "Hint", "Jeder spielt mit seinem eigenen Spieler, Ball und seinen eigenen Upgrades.  ·  Wer ausgeschaltet wird, ist nach 30 Sekunden wieder dabei.",
                17f, MetaUi.Muted, new Vector2(0f, -300f), new Vector2(1400f, 28f));
        }

        void BuildHosting(RectTransform c)
        {
            hosting = Panel(c, "Hosting");
            var root = hosting.transform;
            UiKit.Img("Plate", root, ExactButtonArt.Get("duo-host-panel"), Color.white, new Vector2(0f, 40f), new Vector2(980f, 376f));
            MenuArt.Label("Over", root, "DEIN RAUM-CODE", 22f, Mint, new Vector2(0f, 160f), new Vector2(900f, 30f), TextAlignmentOptions.Center, 10f, MenuArt.TextHeavySoft);
            Boxes(root, codeBoxes, codeLetters, new Vector2(0f, 70f), new Vector2(124f, 122f), 16f, 64f);
            MetaUi.Text(root, "Sub", "Gib den Code deinem Freund. Er wählt DUO  ·  RAUM BEITRETEN und tippt ihn ein.", 20f, MetaUi.Body, new Vector2(0f, -20f), new Vector2(900f, 30f));
            hostStatus = MenuArt.Label("Status", root, "", 24f, MetaUi.Gold, new Vector2(0f, -90f), new Vector2(900f, 34f), TextAlignmentOptions.Center, 6f, MenuArt.TextHeavySoft);
            closeButton = new ChunkButton(root, "Close", new Vector2(0f, -218f), new Vector2(400f, 89f), MetaUi.Danger, "RAUM SCHLIESSEN", 26f);
            Target("duoClose", closeButton, Leave, () => hostingA);
        }

        void BuildJoining(RectTransform c)
        {
            joining = Panel(c, "Joining");
            var root = joining.transform;
            UiKit.Img("Plate", root, ExactButtonArt.Get("duo-connect-panel"), Color.white, new Vector2(0f, 40f), new Vector2(810f, 480f));
            UiKit.Img("Verbindung", root, ExactButtonArt.Get("duo-spinner"), Color.white, new Vector2(0f, -8f), new Vector2(150f, 148f));
            joinStatus = MenuArt.Label("Status", root, "", 22f, MetaUi.Gold, new Vector2(0f, 112f), new Vector2(860f, 34f), TextAlignmentOptions.Center, 6f, MenuArt.TextHeavySoft);
            MetaUi.Text(root, "Sub", "Das dauert meistens nur ein paar Sekunden.", 18f, MetaUi.Muted, new Vector2(0f, -113f), new Vector2(860f, 28f));
            cancelButton = new ChunkButton(root, "Cancel", new Vector2(0f, -285f), new Vector2(390f, 114f), MetaUi.Danger, "ABBRECHEN", 26f);
            Target("duoCancel", cancelButton, Leave, () => joiningA);
        }

        void BuildLobby(RectTransform c)
        {
            lobby = Panel(c, "Lobby");
            var root = lobby.transform;
            lobbyCode = MenuArt.Label("Code", root, "", 20f, Mint, new Vector2(0f, 330f), new Vector2(900f, 30f), TextAlignmentOptions.Center, 10f, MenuArt.TextHeavySoft);

            // two pools of moonlight with a player standing in each
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -330f : 330f;
                UiKit.Img("Pool" + i, root, UiArt.Glow, new Color(0.75f, 0.95f, 1f, 0.16f), new Vector2(x, -150f), new Vector2(520f, 110f));
            }
            me = new MenuFigure();
            me.Build(root, new Vector2(-330f, -150f), 190f, PlayerArt.Get(Characters.Index), Characters.Current);
            partner = new MenuFigure();
            partner.Build(root, new Vector2(330f, -150f), 190f, PlayerArt.Get(Characters.Index), Characters.Current);
            MenuArt.Label("And", root, "&", 70f, Color.white.WithAlpha(0.55f), new Vector2(0f, 60f), new Vector2(120f, 90f), TextAlignmentOptions.Center, 0f);

            meName = MenuArt.Label("MeName", root, "", 34f, Color.white, new Vector2(-330f, -176f), new Vector2(500f, 44f), TextAlignmentOptions.Center, 6f);
            meRole = MenuArt.Label("MeRole", root, "", 17f, MetaUi.Muted, new Vector2(-330f, -206f), new Vector2(500f, 24f), TextAlignmentOptions.Center, 5f, MenuArt.TextHeavySoft);
            partnerName = MenuArt.Label("PartnerName", root, "", 34f, Color.white, new Vector2(330f, -176f), new Vector2(500f, 44f), TextAlignmentOptions.Center, 6f);
            partnerRole = MenuArt.Label("PartnerRole", root, "", 17f, MetaUi.Muted, new Vector2(330f, -206f), new Vector2(500f, 24f), TextAlignmentOptions.Center, 5f, MenuArt.TextHeavySoft);

            startButton = new ChunkButton(root, "Start", new Vector2(0f, -325f), new Vector2(420f, 176f), MetaUi.Gold, "DUO STARTEN", 42f, MenuArt.IconPlay, 36f, true, true);
            startButton.IconLeft(52f);
            Target("duoStart", startButton, StartRun, () => lobbyA * (IsHost ? 1f : 0f));
            lobbyHint = MenuArt.Label("Hint", root, "", 22f, MetaUi.Gold, new Vector2(0f, -330f), new Vector2(900f, 34f), TextAlignmentOptions.Center, 6f, MenuArt.TextHeavySoft);
            leaveButton = new ChunkButton(root, "Leave", new Vector2(-620f, -330f), new Vector2(300f, 91f), MetaUi.Danger, "VERLASSEN", 24f);
            Target("duoLeave", leaveButton, Leave, () => lobbyA);
        }

        static bool IsHost => Coop.S != null && Coop.S.Link.IsHost;

        // ------------------------------------------------------------------ actions

        void Create()
        {
            Coop.HostRoom();
            typing = false;
        }

        void Join()
        {
            if (typed.Length < CodeLength) { Say("Der Code hat " + CodeLength + " Zeichen."); return; }
            Coop.JoinRoom(typed);
        }

        void Leave()
        {
            Coop.Leave();
            typed = "";
        }

        void StartRun()
        {
            if (!IsHost || Coop.S == null || !Coop.S.PartnerHello) return;
            nav.Open(MenuPage.Main);
            // the title screen's own kick-off: the same animation as SPIELEN
            StartRequested?.Invoke();
        }

        /// <summary>The host kicked DUO STARTEN: the menu plays its kick-off, then the run starts for both.</summary>
        public System.Action StartRequested;

        void Say(string text)
        {
            message.text = text;
            messageT = 0f;
        }

        // ------------------------------------------------------------------ typing

        public void OnOpened()
        {
            // a failed room is gone once the page is looked at again
            var s = Coop.S;
            if (s != null && (s.Link.St == NetLink.State.Failed || s.Link.St == NetLink.State.Closed))
            {
                Say(s.Link.Error ?? s.Notice ?? "Die Verbindung ist beendet.");
                Coop.Leave();
            }
        }

        void Type()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.backspaceKey.wasPressedThisFrame && typed.Length > 0) typed = typed.Substring(0, typed.Length - 1);
            if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && typed.Length == CodeLength) Join();
            // letters and digits arrive as typed characters (the keyboard layout is the system's business)
            if (textKeyboard != kb)
            {
                if (textKeyboard != null) textKeyboard.onTextInput -= OnText;
                textKeyboard = kb;
                kb.onTextInput += OnText;
            }
        }

        Keyboard textKeyboard;

        void OnText(char ch)
        {
            if (!typing || typed.Length >= CodeLength) return;
            ch = char.ToUpperInvariant(ch);
            if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) typed += ch;
        }

        // ------------------------------------------------------------------ update

        View Current()
        {
            var s = Coop.S;
            if (s == null) return View.Choose;
            var st = s.Link.St;
            if (st == NetLink.State.Failed || st == NetLink.State.Closed)
            {
                Say(s.Link.Error ?? s.Notice ?? "Die Verbindung ist beendet.");
                Coop.Leave();
                return View.Choose;
            }
            if (st == NetLink.State.Connected && s.PartnerHello) return View.Lobby;
            if (s.Link.IsHost) return s.Link.JoinCode != null ? View.Hosting : View.Opening;
            return View.Joining;
        }

        public void Update(float udt, Vector2 aim)
        {
            page.Update(udt);
            time += udt;
            bool open = page.T > 0.01f;
            if (!open) return;
            view = Current();
            var s = Coop.S;
            if (s != null && !string.IsNullOrEmpty(s.Notice)) { Say(s.Notice); s.Notice = null; }

            chooseA = Mathf.MoveTowards(chooseA, view == View.Choose ? 1f : 0f, udt * 5f);
            hostingA = Mathf.MoveTowards(hostingA, view == View.Hosting || view == View.Opening ? 1f : 0f, udt * 5f);
            joiningA = Mathf.MoveTowards(joiningA, view == View.Joining ? 1f : 0f, udt * 5f);
            lobbyA = Mathf.MoveTowards(lobbyA, view == View.Lobby ? 1f : 0f, udt * 5f);
            Show(choose, chooseA);
            Show(hosting, hostingA);
            Show(joining, joiningA);
            Show(lobby, lobbyA);

            typing = view == View.Choose && page.T > 0.5f;
            if (typing) Type();
            for (int i = 0; i < CodeLength; i++)
            {
                joinLetters[i].text = i < typed.Length ? typed[i].ToString() : "";
                joinBoxes[i].color = Color.white;
            }
            if (typed.Length < CodeLength)
            {
                var box = joinBoxes[typed.Length].rectTransform;
                caret.rectTransform.anchoredPosition = box.anchoredPosition + new Vector2(0f, -30f);
                caret.color = Mint.WithAlpha(0.5f + 0.5f * Mathf.Sin(time * 8f));
            }
            else caret.color = Color.clear;
            joinButton.Disabled = typed.Length < CodeLength;

            if (view == View.Hosting || view == View.Opening)
            {
                string code = s?.Link.JoinCode ?? "";
                for (int i = 0; i < CodeLength; i++) codeLetters[i].text = i < code.Length ? code[i].ToString() : "";
                string dots = new string('.', 1 + (int)(time * 2.5f) % 3);
                hostStatus.text = view == View.Opening ? "RAUM WIRD ERSTELLT " + dots
                    : s.Link.Connected ? "MITSPIELER KOMMT REIN " + dots : "WARTE AUF DEINEN MITSPIELER " + dots;
            }
            if (view == View.Joining)
            {
                string dots = new string('.', 1 + (int)(time * 2.5f) % 3);
                joinStatus.text = (s?.Link.JoinCode != null ? "RAUM " + s.Link.JoinCode + "  " : "") + dots;
            }
            if (view == View.Lobby) UpdateLobby(udt, aim, s);

            messageT += udt;
            message.alpha = messageT < 5f ? 1f - MathUtil.Smooth01((messageT - 4.2f) / 0.8f) : 0f;
        }

        void UpdateLobby(float udt, Vector2 aim, CoopSession s)
        {
            if (meShown != Characters.Index)
            {
                meShown = Characters.Index;
                me.SetLook(PlayerArt.Get(meShown), Characters.Current);
            }
            int pi = Mathf.Clamp(s.PartnerCharacter, 0, Characters.All.Length - 1);
            if (partnerShown != pi)
            {
                partnerShown = pi;
                partner.SetLook(PlayerArt.Get(pi), Characters.All[pi]);
            }
            var mine = Characters.Current;
            var theirs = Characters.All[pi];
            meName.text = mine.Name + "  (DU)";
            meName.color = Color.Lerp(mine.Accent, Color.white, 0.5f);
            meRole.text = mine.Role.ToUpperInvariant() + (IsHost ? "  ·  HOST" : "");
            partnerName.text = theirs.Name;
            partnerName.color = Color.Lerp(theirs.Accent, Color.white, 0.5f);
            partnerRole.text = theirs.Role.ToUpperInvariant() + (IsHost ? "" : "  ·  HOST");
            lobbyCode.text = "RAUM " + (s.Link.JoinCode ?? "");
            me.Update(udt, MenuFigure.Mode.Juggle, new Vector2(1f, 0.1f));
            partner.Update(udt, MenuFigure.Mode.Juggle, new Vector2(-1f, 0.1f));
            startButton.Root.gameObject.SetActive(IsHost);
            lobbyHint.gameObject.SetActive(!IsHost);
            if (!IsHost) lobbyHint.text = "DER HOST STARTET DEN LAUF " + new string('.', 1 + (int)(time * 2.5f) % 3);
        }

        /// <summary>Capture tool: type a room code as if on the keyboard.</summary>
        public void DebugType(string code) => typed = code.ToUpperInvariant();

        static void Show(CanvasGroup g, float a)
        {
            g.alpha = a;
            bool on = a > 0.001f;
            if (g.gameObject.activeSelf != on) g.gameObject.SetActive(on);
        }
    }
}
