using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace SoccerFight
{
    /// <summary>Genau ein stummer Decoder für den sichtbaren Charakter; Zahlen liegen separat darüber.</summary>
    public sealed class CharacterLoopPlayer
    {
        readonly RawImage view;
        readonly VideoPlayer player;
        RenderTexture surface;
        Texture2D still;
        bool requested, ready, failed;
        public string CharacterId { get; private set; }
        public bool IsPlaying => ready && player.isPlaying;
        public int CompletedLoops { get; private set; }

        public CharacterLoopPlayer(RawImage image, Transform host)
        {
            view = image;
            var go = new GameObject("Charakter-Videoloop");
            go.transform.SetParent(host,false);
            player = go.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.source = VideoSource.Url;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.isLooping = true;
            player.skipOnDrop = true;
            player.waitForFirstFrame = true;
            player.prepareCompleted += Prepared;
            player.errorReceived += Failed;
            player.loopPointReached += p => CompletedLoops++;
        }

        public void Open(string id, Texture2D original)
        {
            Stop();
            CharacterId = id;
            still = original;
            requested = true;
            ready = failed = false;
            CompletedLoops = 0;
            view.texture = original;
            surface = new RenderTexture(1600,900,0,RenderTextureFormat.ARGB32);
            surface.Create();
            // Auch der erste Videoframe kann so niemals schwarz aufblitzen.
            Graphics.Blit(original,surface);
            player.targetTexture = surface;
            player.url = Application.streamingAssetsPath.TrimEnd('/') + "/CharacterLoops/" + id + ".mp4";
            // Neue Bewegungen dürfen im Browser nicht durch den zuvor zwischengespeicherten Clip ersetzt werden.
            if (Application.platform == RuntimePlatform.WebGLPlayer) player.url += "?v=dribble-2";
            player.Prepare();
        }

        void Prepared(VideoPlayer p)
        {
            if (requested && !failed) p.Play();
        }

        void Failed(VideoPlayer p, string message)
        {
            failed = true;
            ready = false;
            view.texture = still;
            p.Stop();
            Debug.LogWarning("Charakter-Videoloop nicht verfügbar ("+CharacterId+"): "+message);
        }

        public void Update(bool visible)
        {
            if (!requested) return;
            if (!visible) { Stop(); return; }
            if (!failed && !ready && player.isPlaying && player.frame >= 0)
            {
                ready = true;
                view.texture = surface;
            }
        }

        public void Stop()
        {
            requested = ready = false;
            player.Stop();
            player.targetTexture = null;
            view.texture = still;
            if (surface != null)
            {
                surface.Release();
                Object.Destroy(surface);
                surface = null;
            }
        }
    }
}
