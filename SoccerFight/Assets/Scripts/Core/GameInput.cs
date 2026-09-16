using UnityEngine;
using UnityEngine.InputSystem;

namespace SoccerFight
{
    /// <summary>
    /// Polled once per frame. Everything reads from here so a scripted driver (screenshot capture)
    /// can feed inputs without touching gameplay code. Gameplay actions go through KeyBindings.
    /// Abilities are not bound one by one: four skill keys play whatever sits in slot 1 to 4.
    /// </summary>
    public static class GameInput
    {
        public const int Slots = RunState.MaxSkills;

        public static float MoveX;
        public static bool JumpPressed;
        public static bool JumpHeld;
        public static bool DownPressed;
        public static bool DownHeld;
        public static bool ShootPressed;
        public static bool PowerPressed;
        /// <summary>One flag per skill slot (slot 1 = index 0).</summary>
        public static readonly bool[] SkillPressed = new bool[Slots];
        public static bool RestartPressed;
        public static bool PausePressed;
        public static bool ToggleFps;
        public static bool ToggleVsync;
        /// <summary>Left mouse button, polled even while menus block gameplay (the title screen needs it).</summary>
        public static bool ClickPressed;
        /// <summary>F3: developer panel.</summary>
        public static bool DevPressed;
        public static Vector2 AimScreen;
        public static Vector2 AimWorld;

        /// <summary>When true, device input is ignored and values are written by a driver.</summary>
        public static bool Scripted;

        /// <summary>When true (pause menu open), gameplay actions are suppressed.</summary>
        public static bool Blocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            MoveX = 0f;
            JumpPressed = JumpHeld = DownPressed = DownHeld = ShootPressed = PowerPressed = false;
            RestartPressed = PausePressed = ToggleFps = ToggleVsync = DevPressed = ClickPressed = false;
            ClearSkills();
            AimScreen = AimWorld = Vector2.zero;
            Scripted = Blocked = false;
        }

        static void ClearSkills()
        {
            for (int i = 0; i < SkillPressed.Length; i++) SkillPressed[i] = false;
        }

        /// <summary>Scripted driver: press whichever slot currently holds this ability.</summary>
        public static void PressAbility(Ability a)
        {
            int slot = Game.I != null ? Game.I.Run.SlotOf(a) : -1;
            if (slot >= 0 && slot < SkillPressed.Length) SkillPressed[slot] = true;
            else if (a == Ability.Power) PowerPressed = true;
            else if (a == Ability.Shot) ShootPressed = true;
        }

        public static void Poll(Camera cam)
        {
            if (Scripted) return;

            var kb = Keyboard.current;
            var mouse = Mouse.current;

            PausePressed = kb != null && kb.escapeKey.wasPressedThisFrame;
            RestartPressed = kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
            ToggleFps = kb != null && kb.f1Key.wasPressedThisFrame;
            ToggleVsync = kb != null && kb.f2Key.wasPressedThisFrame;
            DevPressed = kb != null && kb.f3Key.wasPressedThisFrame;
            if (mouse != null)
            {
                AimScreen = mouse.position.ReadValue();
                ClickPressed = mouse.leftButton.wasPressedThisFrame;
            }

            if (Blocked)
            {
                MoveX = 0f;
                JumpPressed = JumpHeld = DownPressed = DownHeld = ShootPressed = PowerPressed = false;
                ClearSkills();
            }
            else
            {
                float move = 0f;
                if (KeyBindings.IsPressed(GameAction.Left)) move -= 1f;
                if (KeyBindings.IsPressed(GameAction.Right)) move += 1f;
                MoveX = move;
                JumpPressed = KeyBindings.WasPressed(GameAction.Jump);
                JumpHeld = KeyBindings.IsPressed(GameAction.Jump);
                DownPressed = KeyBindings.WasPressed(GameAction.Down);
                DownHeld = KeyBindings.IsPressed(GameAction.Down);
                ShootPressed = KeyBindings.WasPressed(GameAction.Shoot);
                PowerPressed = KeyBindings.WasPressed(GameAction.PowerShot);
                for (int i = 0; i < SkillPressed.Length; i++)
                    SkillPressed[i] = KeyBindings.WasPressed((GameAction)((int)GameAction.Skill1 + i));
            }

            if (cam != null)
            {
                Vector3 w = cam.ScreenToWorldPoint(new Vector3(AimScreen.x, AimScreen.y, -cam.transform.position.z));
                AimWorld = w;
            }
        }

        /// <summary>Clears one-frame flags (used by the scripted driver after a frame is consumed).</summary>
        public static void ClearEdges()
        {
            JumpPressed = DownPressed = ShootPressed = PowerPressed = false;
            RestartPressed = PausePressed = ToggleFps = ToggleVsync = DevPressed = ClickPressed = false;
            ClearSkills();
        }
    }
}
