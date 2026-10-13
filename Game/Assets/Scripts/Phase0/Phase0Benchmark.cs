using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace AfterCity.Game.Phase0
{
    /// <summary>
    /// Mode mesure, active par l'argument "-aftercity-benchmark &lt;dossier&gt;" :
    /// la camera tourne lentement autour de la ferme, on mesure les FPS pendant 20 s,
    /// on enregistre screenshot.png et fps.txt dans le dossier, puis on quitte.
    /// </summary>
    public sealed class Phase0Benchmark : MonoBehaviour
    {
        public FreeCamera freeCamera;
        public float warmup = 2f;
        public float duration = 20f;

        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-aftercity-benchmark");
            if (i < 0) return;
            string dir = i + 1 < args.Length ? args[i + 1] : Application.persistentDataPath;
            StartCoroutine(Run(dir));
        }

        /// <summary>Donne quelques ordres pour que la mesure et la capture montrent des survivants au travail.</summary>
        static void DemoOrders()
        {
            var game = FindAnyObjectByType<AfterCity.Game.GameRoot>();
            if (game == null) return;
            var colony = game.Colony;
            var farm = game.Map.FarmCenter;
            int trees = 0;
            for (int r = 1; r < 30 && trees < 10; r++)
            for (int dy = -r; dy <= r && trees < 10; dy++)
            for (int dx = -r; dx <= r && trees < 10; dx++)
            {
                if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != r) continue;
                if (colony.Order(AfterCity.Core.Sim.JobType.Chop, new AfterCity.Core.World.GridPos(farm.X + dx, farm.Y + dy))) trees++;
            }
            for (int x = -2; x <= 2; x++)
                colony.Order(AfterCity.Core.Sim.JobType.Dig, new AfterCity.Core.World.GridPos(farm.X + x, farm.Y - 7));
            for (int x = -2; x <= 2; x++)
                colony.Order(AfterCity.Core.Sim.JobType.Barricade, new AfterCity.Core.World.GridPos(farm.X + x, farm.Y + 7));
            game.Speed = 2;
        }

        IEnumerator Run(string dir)
        {
            Directory.CreateDirectory(dir);
            if (freeCamera != null) freeCamera.edgePan = false;
            DemoOrders();
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < warmup) yield return null;

            int frames = 0;
            float worst = 0;
            float start = Time.realtimeSinceStartup;
            bool shot = false;
            while (Time.realtimeSinceStartup - start < duration)
            {
                if (freeCamera != null) freeCamera.Yaw += 6f * Time.unscaledDeltaTime;
                if (!shot && Time.realtimeSinceStartup - start > duration * 0.5f)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, "screenshot.png"));
                    shot = true;
                }
                yield return null;
                frames++;
                worst = Mathf.Max(worst, Time.unscaledDeltaTime);
            }
            float elapsed = Time.realtimeSinceStartup - start;
            float avg = frames / elapsed;
            string report = string.Format(CultureInfo.InvariantCulture,
                "average_fps={0:F1}\nframes={1}\nseconds={2:F2}\nworst_frame_ms={3:F1}\nresolution={4}x{5}\ngpu={6}\n",
                avg, frames, elapsed, worst * 1000f, Screen.width, Screen.height, SystemInfo.graphicsDeviceName);
            File.WriteAllText(Path.Combine(dir, "fps.txt"), report);
            Debug.Log("[AfterCity] Benchmark\n" + report);
            yield return null;
            yield return null;
            Application.Quit(0);
        }
    }
}
