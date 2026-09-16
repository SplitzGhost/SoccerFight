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
            /// <summary>How long drawing and encoding took on its thread (ms).</summary>
            public float Ms;
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
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    j.Canvas = j.Build();
                    j.Data = j.Canvas.Encode(j.Linear, j.Dither, j.Premultiply);
                    j.Ms = (float)sw.Elapsed.TotalMilliseconds;
                });
            }
            all = Task.WhenAll(tasks);
        }

        /// <summary>The slowest jobs, for the boot log.</summary>
        public string Slowest(int count)
        {
            var sorted = new List<Job>(jobs);
            sorted.Sort((a, b) => b.Ms.CompareTo(a.Ms));
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Mathf.Min(count, sorted.Count); i++) sb.Append(sorted[i].Name).Append(' ').Append(Mathf.RoundToInt(sorted[i].Ms)).Append("ms  ");
            return sb.ToString();
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
    /// Art generated while the game runs (a new stage's monsters and platforms). With worker threads
    /// Kick() starts everything in the background; without them (WebGL) Pump() works through the
    /// queue a little per frame while a reward screen has the game frozen. Flush() finishes whatever
    /// is left right before the art is needed. Uploads always happen on the main thread.
    /// </summary>
    public static class ArtQueue
    {
        sealed class Item
        {
            public string Name;
            public Func<SdfCanvas> Build;
            public Vector2 Pivot;
            public bool Linear, Dither;
            public Action<Sprite> Done;
            public SdfCanvas Canvas;
            public Color32[] Data;
            public Task Task;
        }

        static readonly List<Item> waiting = new List<Item>();     // not started yet
        static readonly List<Item> started = new List<Item>();     // computing on a thread, or computed and waiting for upload

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { waiting.Clear(); started.Clear(); }

        public static int Count => waiting.Count + started.Count;

        /// <summary>build runs off the main thread (pure math only); done receives the sprite on the main thread.</summary>
        public static void Add(string name, Func<SdfCanvas> build, Vector2 pivot, Action<Sprite> done, bool linear = true, bool dither = false)
            => waiting.Add(new Item { Name = name, Build = build, Pivot = pivot, Done = done, Linear = linear, Dither = dither });

        static void Compute(Item it)
        {
            it.Canvas = it.Build();
            it.Data = it.Canvas.Encode(it.Linear, it.Dither, true);
        }

        /// <summary>Worker threads: start every waiting job now (no-op on WebGL).</summary>
        public static void Kick()
        {
            if (!Par.Threads) return;
            foreach (var it in waiting)
            {
                var item = it;
                item.Task = Task.Run(() => Compute(item));
                started.Add(item);
            }
            waiting.Clear();
        }

        static void Upload(Item it)
        {
            if (it.Task != null)
            {
                try { it.Task.Wait(); }
                catch (AggregateException e) { foreach (var ex in e.InnerExceptions) Debug.LogException(ex); }
            }
            if (it.Data == null || it.Canvas == null) { it.Done?.Invoke(null); return; }
            var tex = SdfCanvas.CreateTexture(it.Name, it.Canvas.Width, it.Canvas.Height, it.Data, it.Linear, true, TextureWrapMode.Clamp);
            var sprite = it.Canvas.CreateSprite(tex, it.Name, it.Pivot);
            it.Data = null;
            it.Canvas = null;
            it.Done?.Invoke(sprite);
        }

        /// <summary>A slice of work: uploads finished thread results, and without threads computes jobs until the budget is used up.</summary>
        public static void Pump(float budgetMs)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < started.Count; i++)
            {
                var it = started[i];
                if (it.Task != null && !it.Task.IsCompleted) continue;
                started.RemoveAt(i--);
                Upload(it);
                if (sw.Elapsed.TotalMilliseconds > budgetMs) return;
            }
            while (waiting.Count > 0 && sw.Elapsed.TotalMilliseconds < budgetMs)
            {
                var it = waiting[0];
                waiting.RemoveAt(0);
                if (Par.Threads) { it.Task = Task.Run(() => Compute(it)); started.Add(it); continue; }
                try { Compute(it); } catch (Exception e) { Debug.LogException(e); }
                Upload(it);
            }
        }

        /// <summary>Finish everything now (blocks until the threads are done).</summary>
        public static void Flush()
        {
            Kick();
            while (waiting.Count > 0)
            {
                var it = waiting[0];
                waiting.RemoveAt(0);
                try { Compute(it); } catch (Exception e) { Debug.LogException(e); }
                Upload(it);
            }
            while (started.Count > 0)
            {
                var it = started[0];
                started.RemoveAt(0);
                Upload(it);
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
