using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Thread helpers that fall back to inline execution where there are no worker threads (WebGL):
    /// Task.Run / Parallel.For would never run there, so Wait() would freeze the page.
    /// </summary>
    public static class Par
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        public static readonly bool Threads = false;
#else
        public static readonly bool Threads = true;
#endif

        public static Task Run(Action work)
        {
            if (Threads) return Task.Run(work);
            try { work(); return Task.CompletedTask; }
            catch (Exception e) { return Task.FromException(e); }
        }

        public static void For(int from, int to, Action<int> body)
        {
            if (Threads) Parallel.For(from, to, body);
            else for (int i = from; i < to; i++) body(i);
        }
    }

    /// <summary>
    /// Runs canvas generation on worker threads (pure C# math) and uploads the results on the main
    /// thread. Lets the heavy backdrop layers generate while the main thread builds everything else.
    /// Build functions must not touch Unity objects — only math, colors and SdfCanvas.
    /// </summary>
    public sealed class ArtJobs
    {
        public sealed class Job
        {
            public string Name;
            public Func<SdfCanvas> Build;
            public bool Linear;
            public bool Dither = true;
            public bool Premultiply = true;
            public bool Mips = true;
            public TextureWrapMode Wrap = TextureWrapMode.Clamp;
            public Vector2 Pivot;
            public bool MakeSprite = true;

            public SdfCanvas Canvas;
            public Color32[] Data;
            public Texture2D Texture;
            public Sprite Sprite;
        }

        readonly List<Job> jobs = new List<Job>();
        Task all;

        public Job Add(string name, Func<SdfCanvas> build, Vector2 pivot, bool dither = true,
            TextureWrapMode wrap = TextureWrapMode.Clamp, bool linear = false)
        {
            var j = new Job { Name = name, Build = build, Pivot = pivot, Dither = dither, Wrap = wrap, Linear = linear };
            jobs.Add(j);
            return j;
        }

        public void Start()
        {
            var tasks = new Task[jobs.Count];
            for (int i = 0; i < jobs.Count; i++)
            {
                var j = jobs[i];
                tasks[i] = Par.Run(() =>
                {
                    j.Canvas = j.Build();
                    j.Data = j.Canvas.Encode(j.Linear, j.Dither, j.Premultiply);
                });
            }
            all = Task.WhenAll(tasks);
        }

        public void Complete()
        {
            try { all?.Wait(); }
            catch (AggregateException e)
            {
                foreach (var ex in e.InnerExceptions) Debug.LogException(ex);
            }
            foreach (var j in jobs)
            {
                if (j.Data == null || j.Canvas == null) continue;
                j.Texture = SdfCanvas.CreateTexture(j.Name, j.Canvas.Width, j.Canvas.Height, j.Data, j.Linear, j.Mips, j.Wrap);
                if (j.MakeSprite) j.Sprite = j.Canvas.CreateSprite(j.Texture, j.Name, j.Pivot);
                j.Data = null;
            }
        }
    }

    /// <summary>
    /// Packs many small canvases (generated in parallel) into one texture atlas with shelf packing.
    /// Every entry keeps its pixel rect, uv rect and its size/origin in world units.
    /// </summary>
    public sealed class AtlasBuilder
    {
        public sealed class Entry
        {
            public string Name;
            public Func<SdfCanvas> Build;
            public SdfCanvas Canvas;
            public Color32[] Data;
            public RectInt Pixels;
            public Rect Uv;
            public Rect Units;     // canvas rect in units, relative to the plant's pivot
        }

        const int Pad = 6;
        readonly List<Entry> entries = new List<Entry>();
        Task task;
        Color32[] atlas;
        int atlasW, atlasH;
        public Texture2D Texture { get; private set; }

        public Entry Add(string name, Func<SdfCanvas> build)
        {
            var e = new Entry { Name = name, Build = build };
            entries.Add(e);
            return e;
        }

        public void Start(int width)
        {
            atlasW = width;
            var tasks = new Task[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                tasks[i] = Par.Run(() =>
                {
                    e.Canvas = e.Build();
                    e.Data = e.Canvas.Encode(false, false, true);
                    e.Units = e.Canvas.UnitRect;
                });
            }
            var all = Task.WhenAll(tasks);
            task = Par.Threads ? all.ContinueWith(Finish) : Par.Run(() => Finish(all));
        }

        void Finish(Task all)
        {
            if (all.IsFaulted) throw all.Exception;
            Pack();
        }

        void Pack()
        {
            var order = new List<Entry>(entries);
            order.Sort((a, b) => b.Canvas.Height.CompareTo(a.Canvas.Height));
            int x = Pad, y = Pad, shelf = 0;
            foreach (var e in order)
            {
                int w = e.Canvas.Width, h = e.Canvas.Height;
                if (x + w + Pad > atlasW) { x = Pad; y += shelf + Pad; shelf = 0; }
                e.Pixels = new RectInt(x, y, w, h);
                x += w + Pad;
                shelf = Mathf.Max(shelf, h);
            }
            int needed = y + shelf + Pad;
            atlasH = Mathf.NextPowerOfTwo(needed);
            atlas = new Color32[atlasW * atlasH];
            foreach (var e in entries)
            {
                int w = e.Pixels.width;
                for (int row = 0; row < e.Pixels.height; row++)
                    Array.Copy(e.Data, row * w, atlas, (e.Pixels.y + row) * atlasW + e.Pixels.x, w);
                e.Uv = new Rect((float)e.Pixels.x / atlasW, (float)e.Pixels.y / atlasH, (float)w / atlasW, (float)e.Pixels.height / atlasH);
                e.Data = null;
            }
        }

        public void Complete(string name)
        {
            try { task?.Wait(); }
            catch (AggregateException e)
            {
                foreach (var ex in e.Flatten().InnerExceptions) Debug.LogException(ex);
            }
            if (atlas == null) return;
            Texture = SdfCanvas.CreateTexture(name, atlasW, atlasH, atlas, false, true, TextureWrapMode.Clamp);
            atlas = null;
            foreach (var e in entries) e.Canvas = null;
        }
    }
}
