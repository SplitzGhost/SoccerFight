using UnityEngine;
using UnityEngine.InputSystem;

namespace SoccerFight
{
    /// <summary>
    /// Polled once per frame. Everything reads from here so a scripted driver (screenshot capture)
    /// can feed inputs without touching gameplay code. Gameplay actions go through KeyBindings.
    /// </summary>
    public static class GameInput
    {
        public static float MoveX;
        public static bool JumpPressed;
        public static bool JumpHeld;
        public static bool ShootPressed;
        public static bool FlickPressed;
        public static bool RestartPressed;
        public static bool PausePressed;
        public static bool ToggleFps;
        public static bool ToggleVsync;
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
            JumpPressed = JumpHeld = ShootPressed = FlickPressed = RestartPressed = PausePressed = ToggleFps = ToggleVsync = false;
            AimScreen = AimWorld = Vector2.zero;
            Scripted = Blocked = false;
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
            if (mouse != null) AimScreen = mouse.position.ReadValue();

            if (Blocked)
            {
                MoveX = 0f;
                JumpPressed = JumpHeld = ShootPressed = FlickPressed = false;
            }
            else
            {
                float move = 0f;
                if (KeyBindings.IsPressed(GameAction.Left)) move -= 1f;
                if (KeyBindings.IsPressed(GameAction.Right)) move += 1f;
                MoveX = move;
                JumpPressed = KeyBindings.WasPressed(GameAction.Jump);
                JumpHeld = KeyBindings.IsPressed(GameAction.Jump);
                ShootPressed = KeyBindings.WasPressed(GameAction.Shoot);
                FlickPressed = KeyBindings.WasPressed(GameAction.Flick);
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
            JumpPressed = ShootPressed = FlickPressed = RestartPressed = PausePressed = ToggleFps = ToggleVsync = false;
        }
    }
}
