using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace SoccerFight
{
    /// <summary>
    /// Development tool: when Unity is started with "-sfCapture &lt;name&gt; -sfOut &lt;dir&gt;", this plays a
    /// scripted scenario at a fixed 60 fps timestep and writes screenshots plus animation contact
    /// sheets to disk. Used to review visuals and animation without a human at the keyboard.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class CaptureDriver : MonoBehaviour
    {
        public static bool Finished { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Finished = false; }

        public static bool IsRequested() => Arg("-sfCapture") != null;

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        const int W = 1920, H = 1080, Cell = 420;

        string outDir;
        RenderTexture rt, cellRt;
        Texture2D frame;
        string pendingShot;
        bool pendingCell;
        Texture2D sheet;
        int sheetCols, sheetRows, sheetIndex;

        Game G => Game.I;
        Player P => Game.I.Player;
        bool running;

        // Started from Update (not Start) so the scenario restarts cleanly after a script reload.
        void Update()
        {
            if (running || Finished || Game.I == null || Game.I.Player == null) return;
            running = true;
            StartCoroutine(Main());
        }

        IEnumerator Main()
        {
            outDir = Arg("-sfOut") ?? Path.Combine(Application.dataPath, "../Captures");
            Directory.CreateDirectory(outDir);
            Time.captureDeltaTime = 1f / 60f;
            GameInput.Scripted = true;
            rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cellRt = new RenderTexture(Cell, Cell, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            frame = new Texture2D(W, H, TextureFormat.RGBA32, false);
            G.Cam.Cam.targetTexture = rt;
            G.Waves.Enabled = false;
            Debug.Log("[Capture] started → " + outDir);

            string scenario = Arg("-sfCapture");
            yield return Frames(5);
            if (scenario == "quick") yield return Quick();
            else yield return All();

            Debug.Log("[Capture] finished");
            Time.captureDeltaTime = 0f;
            Finished = true;
        }

        // ------------------------------------------------------------------ scenario helpers

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator Seconds(float s) => Frames(Mathf.RoundToInt(s * 60f));

        void Move(float x) => GameInput.MoveX = x;
        void Aim(Vector2 offsetFromPlayer) => GameInput.AimWorld = P.Pos + offsetFromPlayer;

        IEnumerator Shot(string name) { pendingShot = name; yield return null; }

        void BeginSheet(int cols, int rows)
        {
            sheetCols = cols; sheetRows = rows; sheetIndex = 0;
            sheet = new Texture2D(cols * Cell, rows * Cell, TextureFormat.RGBA32, false);
            var fill = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(10, 14, 22, 255);
            sheet.SetPixels32(fill);
            G.Hud.SetVisible(false);
        }

        IEnumerator SheetCell(Vector2 offset, float size)
        {
            G.Cam.SetOverride(P.Pos + offset, size);
            pendingCell = true;
            yield return null;
        }

        void EndSheet(string name)
        {
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), sheet.EncodeToPNG());
            Destroy(sheet);
            G.Cam.ClearOverride();
            G.Hud.SetVisible(true);
            Debug.Log("[Capture] sheet " + name);
        }

        // ------------------------------------------------------------------ scenarios

        IEnumerator Quick()
        {
            Aim(new Vector2(5f, 1.5f));
            yield return Seconds(1.5f);
            yield return Shot("quick_idle");
        }

        IEnumerator All()
        {
            // 1 — idle (foot rests on the ball after a moment)
            Aim(new Vector2(5f, 1.6f));
            yield return Seconds(1.8f);
            yield return Shot("01_idle");

            // 1b — background detail close-ups (plants, ruins, trees)
            G.Hud.SetVisible(false);
            G.Cam.SetOverride(P.Pos + new Vector2(4.5f, 1.6f), 2.3f);
            yield return Frames(3);
            yield return Shot("01b_detail_near");
            G.Cam.SetOverride(P.Pos + new Vector2(-3.5f, 3.6f), 3.2f);
            yield return Frames(3);
            yield return Shot("01c_detail_ruins");
            G.Cam.ClearOverride();
            G.Hud.SetVisible(true);
            yield return Frames(3);

            // 2 — run cycle close-up
            Move(1f);
            yield return Seconds(0.9f);
            BeginSheet(6, 2);
            for (int i = 0; i < 12; i++) { Aim(new Vector2(5f, 1.6f)); yield return SheetCell(new Vector2(0.35f, 0.85f), 1.2f); yield return Frames(1); }
            EndSheet("02_run_sheet");
            // grass bending around the running player's legs
            BeginSheet(4, 2);
            for (int i = 0; i < 8; i++) { yield return SheetCell(new Vector2(0.1f, 0.45f), 0.8f); yield return Frames(3); }
            EndSheet("02b_grass_push");
            yield return Frames(10);
            yield return Shot("03_run_full");

            // 3 — stop, then kick close-up
            Move(0f);
            yield return Seconds(1.0f);
            Aim(new Vector2(7f, 1.4f));
            GameInput.ShootPressed = true;
            BeginSheet(6, 2);
            for (int i = 0; i < 12; i++) { yield return SheetCell(new Vector2(0.3f, 0.8f), 1.25f); yield return Frames(1); }
            EndSheet("04_kick_sheet");

            // 4 — shot in flight (full view)
            yield return Seconds(1.2f);
            Aim(new Vector2(8f, 2.6f));
            GameInput.ShootPressed = true;
            yield return Frames(14);
            yield return Shot("05_shot_full");
            yield return Seconds(1.4f);

            // 5 — rainbow flick close-up
            Aim(new Vector2(6f, 0.5f));
            GameInput.FlickPressed = true;
            BeginSheet(6, 3);
            for (int i = 0; i < 18; i++) { yield return SheetCell(new Vector2(0.1f, 1.0f), 1.45f); yield return Frames(2); }
            EndSheet("06_flick_sheet");

            // 6 — rainbow arc mid-air and the impact (full view)
            for (int i = 0; i < 200 && G.Ball.St != Ball.State.Rainbow; i++) yield return null;
            yield return Frames(12);
            yield return Shot("07_flick_arc");
            for (int i = 0; i < 200 && G.Ball.St == Ball.State.Rainbow; i++) yield return null;
            yield return Frames(2);
            yield return Shot("08_flick_impact");
            yield return Seconds(1.5f);

            // 7 — combat: monsters, hit reactions, damage numbers, HUD cooldowns
            G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(4.5f, 0f));
            G.Waves.SpawnAt(Monster.Kind.Wisp, P.Pos + new Vector2(3.2f, 2.6f));
            G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(-4.8f, 0f));
            yield return Seconds(0.9f);
            Aim(new Vector2(4.5f, 0.45f));
            GameInput.ShootPressed = true;
            yield return Frames(15);
            yield return Shot("09_combat_hit");
            yield return Seconds(0.5f);
            yield return Shot("10_combat_hud");

            // 8 — let a monster reach the player to see the damage feedback
            yield return Seconds(2.5f);
            yield return Shot("11_after_damage");

            // 9 — jump close-up
            G.Waves.Restart(999f);
            yield return Seconds(1.2f);
            GameInput.JumpPressed = true;
            GameInput.JumpHeld = true;
            BeginSheet(6, 2);
            for (int i = 0; i < 12; i++) { yield return SheetCell(new Vector2(0.2f, 1.6f), 1.9f); yield return Frames(3); }
            GameInput.JumpHeld = false;
            EndSheet("12_jump_sheet");

            // 10 — regular wave start with banner
            G.Waves.Enabled = true;
            G.Waves.Restart(0.2f);
            yield return Seconds(0.75f);
            yield return Shot("13_wave_banner");
            yield return Seconds(2.6f);
            yield return Shot("14_wave_fight");

            // 11 — pause menu and settings
            G.Pause.Open();
            yield return Frames(60);
            yield return Shot("15_pause");
            G.Pause.OpenSettings();
            yield return Frames(60);
            yield return Shot("16_settings");
            G.Pause.Close();
            yield return Frames(30);
        }

        // ------------------------------------------------------------------ rendering

        void Render()
        {
            Canvas.ForceUpdateCanvases();
            var req = new RenderPipeline.StandardRequest { destination = rt };
            RenderPipeline.SubmitRenderRequest(G.Cam.Cam, req);
        }

        void LateUpdate()
        {
            if (!running || rt == null || Game.I == null || Game.I.Cam == null) return;
            if (pendingShot != null)
            {
                Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                frame.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                frame.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(outDir, pendingShot + ".png"), frame.EncodeToPNG());
                Debug.Log("[Capture] shot " + pendingShot);
                pendingShot = null;
            }

            if (pendingCell && sheet != null)
            {
                Render();
                float sx = (float)H / W;
                Graphics.Blit(rt, cellRt, new Vector2(sx, 1f), new Vector2((1f - sx) * 0.5f, 0f));
                var prev = RenderTexture.active;
                RenderTexture.active = cellRt;
                int col = sheetIndex % sheetCols, row = sheetIndex / sheetCols;
                if (row < sheetRows) sheet.ReadPixels(new Rect(0, 0, Cell, Cell), col * Cell, (sheetRows - 1 - row) * Cell);
                RenderTexture.active = prev;
                sheetIndex++;
                pendingCell = false;
            }
        }
    }
}
