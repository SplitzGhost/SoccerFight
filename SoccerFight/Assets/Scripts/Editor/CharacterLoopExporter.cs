using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SoccerFight.EditorTools
{
    /// <summary>Rendert die Originalansichten als verlustarme, stumme MP4-Schleifen.</summary>
    public static class CharacterLoopExporter
    {
        const int Width = 1600, Height = 900, Fps = 30, Frames = 240;
        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i=0; i<args.Length-1; i++) if (args[i] == name) return args[i+1];
            throw new ArgumentException("Fehlendes Exportargument: " + name);
        }

        public static void Export()
        {
            int exit = 0;
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                string output = Arg("-sfLoopOut"), encoder = Arg("-sfEncoder"), sourceRoot = Arg("-sfSourceRoot");
                Directory.CreateDirectory(output);
                string scratch = Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName,"character-loop-export");
                Directory.CreateDirectory(scratch);
                var rt = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rt.Create();
                var frame = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                byte[] pixels = new byte[Width*Height*4];
                string[] names = { "rio", "bruno", "mira", "dre", "titan", "nova" };
                string selected = null;
                var arguments = Environment.GetCommandLineArgs();
                for (int a=0; a<arguments.Length-1; a++) if (arguments[a]=="-sfCharacter") selected=arguments[a+1];
                for (int index=0; index<names.Length; index++)
                {
                    string name = names[index];
                    if (selected != null && selected != name) continue;
                    var source = Resources.Load<Texture2D>("Menu/CharacterDetails/"+name);
                    using var animation = new DribbleLoopScene(name,index,source,sourceRoot,Width,Height);
                    string file = Path.Combine(output,name+".mp4");
                    string pending = Path.Combine(scratch,name+".pending.mp4");
                    var start = new ProcessStartInfo(encoder,
                        "-hide_banner -loglevel error -y -f rawvideo -pixel_format rgba -video_size 1600x900 -framerate 30 -i pipe:0 " +
                        "-vf vflip -c:v libx264 -threads 4 -preset medium -crf 18 -pix_fmt yuv420p -an -g 240 -keyint_min 240 -sc_threshold 0 -movflags +faststart \""+pending+"\"")
                    { UseShellExecute=false, CreateNoWindow=true, RedirectStandardInput=true, RedirectStandardError=true };
                    using (var process = Process.Start(start))
                    {
                        var encoderErrors = process.StandardError.ReadToEndAsync();
                        for (int f=0; f<Frames; f++)
                        {
                            animation.Render(f/(float)Fps,rt);
                            RenderTexture.active = rt;
                            frame.ReadPixels(new Rect(0,0,Width,Height),0,0,false);
                            RenderTexture.active = null;
                            frame.GetRawTextureData<byte>().CopyTo(pixels);
                            process.StandardInput.BaseStream.Write(pixels,0,pixels.Length);
                        }
                        process.StandardInput.Close();
                        process.WaitForExit();
                        if (process.ExitCode != 0) throw new Exception("Videoencoder fehlgeschlagen für "+name+": "+encoderErrors.GetAwaiter().GetResult());
                    }
                    File.Copy(pending,file,true);
                    File.Delete(pending);
                    Resources.UnloadAsset(source);
                    Debug.Log("[CharacterLoops] exportiert: "+name+" (8 s, 30 FPS, 1600 × 900)");
                }
                UnityEngine.Object.DestroyImmediate(frame);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                Debug.Log("[CharacterLoops] alle sechs Clips fertig");
            }
            catch (Exception e) { Debug.LogException(e); exit=1; }
            EditorApplication.Exit(exit);
        }
    }
}
