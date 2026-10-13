using System;
using System.Collections;
using System.Globalization;
using System.IO;
using AfterCity.Core.Sim;
using AfterCity.Core.World;
using AfterCity.Game;
using UnityEngine;

namespace AfterCity.Game.Phase0
{
    /// <summary>
    /// Mode mesure, active par l'argument "-aftercity-benchmark &lt;dossier&gt;" :
    /// donne quelques ordres et amene quelques rodeurs pour avoir de l'action, synchro verticale coupee,
    /// mesure les FPS 10 s avec les effets visuels puis 10 s sans, enregistre screenshot.png
    /// (avec effets), screenshot_sans_effets.png et fps.txt dans le dossier, puis quitte.
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

        /// <summary>Ordres de demonstration et quelques rodeurs a l'oree du bois.</summary>
        static void DemoOrders()
        {
            var game = FindAnyObjectByType<GameRoot>();
            if (game == null) return;
            var colony = game.Colony;
            var farm = game.Map.FarmCenter;
            int trees = 0;
            for (int r = 1; r < 30 && trees < 10; r++)
            for (int dy = -r; dy <= r && trees < 10; dy++)
            for (int dx = -r; dx <= r && trees < 10; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                if (colony.Order(JobType.Chop, new GridPos(farm.X + dx, farm.Y + dy))) trees++;
            }
            for (int x = -2; x <= 2; x++)
                colony.Order(JobType.Dig, new GridPos(farm.X + x, farm.Y - 7));
            for (int x = -2; x <= 2; x++)
                colony.Order(JobType.Barricade, new GridPos(farm.X + x, farm.Y + 7));
            for (int x = -4; x <= 4; x++)
                colony.Order(JobType.Road, new GridPos(farm.X + x, farm.Y - 5));
            colony.Stock.Add(ResourceType.Wood, 35);
            colony.Stock.Add(ResourceType.Stone, 4);
            colony.PlaceBuilding(BuildingType.Shelter, new GridPos(farm.X - 3, farm.Y + 2));
            colony.PlaceBuilding(BuildingType.Watchtower, new GridPos(farm.X + 3, farm.Y - 3));
            colony.PlaceBuilding(BuildingType.ExpeditionPost, new GridPos(farm.X + 1, farm.Y + 2));
            for (int k = 0; k < 4; k++)
                game.Horde.Spawn(new GridPos(farm.X + 12 + k % 2, farm.Y - 3 + k * 2));
            game.Speed = 2;
            var hud = game.GetComponent<Hud>();
            if (hud != null) hud.CloseWelcome();
        }

        IEnumerator Run(string dir)
        {
            Directory.CreateDirectory(dir);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            if (freeCamera != null) freeCamera.edgePan = false;
            var fx = FindAnyObjectByType<AfterCity.Game.PostFxToggle>();
            DemoOrders();
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < warmup) yield return null;

            var results = new float[2];
            var worst = new float[2];
            for (int pass = 0; pass < 2; pass++)
            {
                if (fx != null) fx.Apply(pass == 0);
                yield return null;
                int frames = 0;
                float half = duration / 2f;
                float start = Time.realtimeSinceStartup;
                bool shot = false;
                while (Time.realtimeSinceStartup - start < half)
                {
                    if (freeCamera != null) freeCamera.Yaw += 6f * Time.unscaledDeltaTime;
                    if (!shot && Time.realtimeSinceStartup - start > half * 0.6f)
                    {
                        ScreenCapture.CaptureScreenshot(Path.Combine(dir, pass == 0 ? "screenshot.png" : "screenshot_sans_effets.png"));
                        shot = true;
                    }
                    yield return null;
                    frames++;
                    worst[pass] = Mathf.Max(worst[pass], Time.unscaledDeltaTime);
                }
                results[pass] = frames / (Time.realtimeSinceStartup - start);
            }
            if (fx != null) fx.Apply(true);

            // Capture de nuit : on saute a 21 h, les rodeurs de demonstration sont deja a la ferme
            var game = FindAnyObjectByType<GameRoot>();
            if (game != null)
            {
                game.Colony.SkipToHour(21.5);
                float tn = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - tn < 2.5f) yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "screenshot_nuit.png"));
                yield return null;
                yield return null;
            }

            string report = string.Format(CultureInfo.InvariantCulture,
                "average_fps_with_effects={0:F1}\nworst_frame_ms_with_effects={1:F1}\naverage_fps_without_effects={2:F1}\nworst_frame_ms_without_effects={3:F1}\nvsync=off\nresolution={4}x{5}\ngpu={6}\n",
                results[0], worst[0] * 1000f, results[1], worst[1] * 1000f, Screen.width, Screen.height, SystemInfo.graphicsDeviceName);
            File.WriteAllText(Path.Combine(dir, "fps.txt"), report);
            Debug.Log("[AfterCity] Benchmark\n" + report);
            yield return null;
            yield return null;
            Application.Quit(0);
        }
    }
}
