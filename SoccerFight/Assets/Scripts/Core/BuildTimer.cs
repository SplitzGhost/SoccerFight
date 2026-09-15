using System.Diagnostics;
using System.Text;

namespace SoccerFight
{
    /// <summary>Tiny sectional stopwatch for the startup log (where does generation time go?).</summary>
    public static class BuildTimer
    {
        static Stopwatch sw;
        static StringBuilder sb;

        public static void Begin()
        {
            sw = Stopwatch.StartNew();
            sb = new StringBuilder();
        }

        public static void Mark(string section)
        {
            if (sw == null) return;
            sb.Append(section).Append(' ').Append(sw.ElapsedMilliseconds).Append("ms · ");
            sw.Restart();
        }

        public static string Report() => sb != null ? sb.ToString() : "";
    }
}
