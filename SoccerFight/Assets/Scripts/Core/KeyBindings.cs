using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SoccerFight
{
    public enum GameAction { Left, Right, Jump, Shoot, Flick }

    /// <summary>A single key or mouse button.</summary>
    public struct Binding
    {
        public bool Mouse;
        public Key Key;
        public int Button;

        public static Binding K(Key k) => new Binding { Key = k };
        public static Binding M(int b) => new Binding { Mouse = true, Button = b };
        public bool SameAs(Binding o) => Mouse == o.Mouse && (Mouse ? Button == o.Button : Key == o.Key);
    }

    /// <summary>Rebindable controls, persisted in PlayerPrefs. Binding an input that is already used swaps them.</summary>
    public static class KeyBindings
    {
        public static readonly GameAction[] All = { GameAction.Left, GameAction.Right, GameAction.Jump, GameAction.Shoot, GameAction.Flick };
        static readonly Binding[] current = new Binding[5];
        static bool loaded;

        public static event System.Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { loaded = false; Changed = null; }

        public static string ActionName(GameAction a)
        {
            switch (a)
            {
                case GameAction.Left: return "NACH LINKS";
                case GameAction.Right: return "NACH RECHTS";
                case GameAction.Jump: return "SPRINGEN";
                case GameAction.Shoot: return "SCHUSS";
                default: return "RAINBOW FLICK";
            }
        }

        static Binding Default(GameAction a)
        {
            switch (a)
            {
                case GameAction.Left: return Binding.K(Key.A);
                case GameAction.Right: return Binding.K(Key.D);
                case GameAction.Jump: return Binding.K(Key.Space);
                case GameAction.Shoot: return Binding.M(0);
                default: return Binding.K(Key.R);
            }
        }

        static string Encode(Binding b) => b.Mouse ? "m:" + b.Button : "k:" + (int)b.Key;

        static bool TryDecode(string s, out Binding b)
        {
            b = default;
            if (string.IsNullOrEmpty(s) || s.Length < 3 || !int.TryParse(s.Substring(2), out int v)) return false;
            if (s[0] == 'm') { b = Binding.M(Mathf.Clamp(v, 0, 4)); return true; }
            if (s[0] == 'k' && System.Enum.IsDefined(typeof(Key), v) && v != (int)Key.None) { b = Binding.K((Key)v); return true; }
            return false;
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            foreach (var a in All)
                current[(int)a] = TryDecode(PlayerPrefs.GetString("sf_bind_" + a, ""), out var b) ? b : Default(a);
        }

        public static Binding Get(GameAction a) { EnsureLoaded(); return current[(int)a]; }

        public static void Set(GameAction a, Binding b)
        {
            EnsureLoaded();
            Binding old = current[(int)a];
            foreach (var other in All)
                if (other != a && current[(int)other].SameAs(b)) current[(int)other] = old;
            current[(int)a] = b;
            Save();
        }

        public static void ResetDefaults()
        {
            foreach (var a in All) current[(int)a] = Default(a);
            loaded = true;
            Save();
        }

        static void Save()
        {
            foreach (var a in All) PlayerPrefs.SetString("sf_bind_" + a, Encode(current[(int)a]));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        static ButtonControl MouseButton(int i)
        {
            var m = Mouse.current;
            if (m == null) return null;
            switch (i)
            {
                case 0: return m.leftButton;
                case 1: return m.rightButton;
                case 2: return m.middleButton;
                case 3: return m.backButton;
                default: return m.forwardButton;
            }
        }

        static ButtonControl Control(Binding b)
        {
            if (b.Mouse) return MouseButton(b.Button);
            var kb = Keyboard.current;
            return kb != null && b.Key != Key.None ? kb[b.Key] : null;
        }

        public static bool IsPressed(GameAction a) { var c = Control(Get(a)); return c != null && c.isPressed; }
        public static bool WasPressed(GameAction a) { var c = Control(Get(a)); return c != null && c.wasPressedThisFrame; }

        /// <summary>While rebinding: returns true once a key or mouse button went down. Escape cancels.</summary>
        public static bool TryCapture(out Binding b, out bool cancelled)
        {
            b = default;
            cancelled = false;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame) { cancelled = true; return false; }
                if (kb.anyKey.wasPressedThisFrame)
                {
                    foreach (var k in kb.allKeys)
                    {
                        if (k == null || !k.wasPressedThisFrame || k.keyCode == Key.Escape) continue;
                        b = Binding.K(k.keyCode);
                        return true;
                    }
                }
            }
            for (int i = 0; i < 5; i++)
            {
                var m = MouseButton(i);
                if (m != null && m.wasPressedThisFrame) { b = Binding.M(i); return true; }
            }
            return false;
        }

        public static string DisplayName(Binding b)
        {
            if (b.Mouse)
            {
                switch (b.Button)
                {
                    case 0: return "LINKSKLICK";
                    case 1: return "RECHTSKLICK";
                    case 2: return "MAUSRAD";
                    case 3: return "MAUS 4";
                    default: return "MAUS 5";
                }
            }
            switch (b.Key)
            {
                case Key.Space: return "LEERTASTE";
                case Key.LeftShift: case Key.RightShift: return "SHIFT";
                case Key.LeftCtrl: case Key.RightCtrl: return "STRG";
                case Key.LeftAlt: case Key.RightAlt: return "ALT";
                case Key.Tab: return "TAB";
                case Key.Enter: case Key.NumpadEnter: return "ENTER";
                case Key.Backspace: return "RÜCKTASTE";
                case Key.LeftArrow: return "PFEIL LINKS";
                case Key.RightArrow: return "PFEIL RECHTS";
                case Key.UpArrow: return "PFEIL HOCH";
                case Key.DownArrow: return "PFEIL RUNTER";
                case Key.CapsLock: return "FESTSTELL";
            }
            // letters/digits: use the active keyboard layout's label (e.g. Y/Z on German keyboards)
            var kb = Keyboard.current;
            if (kb != null)
            {
                string label = kb[b.Key].displayName;
                if (!string.IsNullOrEmpty(label)) return label.ToUpperInvariant();
            }
            return b.Key.ToString().ToUpperInvariant();
        }

        public static string DisplayName(GameAction a) => DisplayName(Get(a));

        /// <summary>Short label for the small key badges in the HUD.</summary>
        public static string ShortName(GameAction a)
        {
            var b = Get(a);
            if (b.Mouse) return b.Button == 0 ? "LMB" : b.Button == 1 ? "RMB" : "M" + (b.Button + 1);
            string n = DisplayName(b);
            return n == "LEERTASTE" ? "SPACE" : n.Length > 5 ? n.Substring(0, 5) : n;
        }
    }
}
