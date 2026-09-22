using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// What a title-screen page may ask of the menu: register a shootable target, switch pages,
    /// go back, and show a short note near something on screen. Keeps the pages independent of
    /// MainMenu itself.
    /// </summary>
    public sealed class MenuNav
    {
        public System.Action<MenuTarget> Register;
        public System.Action<int> Open;
        public System.Action Back;
        /// <summary>A short note floating above a point in canvas space.</summary>
        public System.Action<string, Vector2> Toast;
        /// <summary>Canvas-space centre of a UI element (for toasts and effects).</summary>
        public System.Func<RectTransform, Vector2> CanvasPos;

        public void Say(string text, RectTransform near) => Toast?.Invoke(text, near != null ? CanvasPos(near) : Vector2.zero);
    }
}
