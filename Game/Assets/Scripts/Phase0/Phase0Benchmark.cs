using System;
using System.Collections;
using System.Globalization;
using System.IO;
using AfterCity.Core.Sim;
using AfterCity.Core.World;
using AfterCity.Game;
using AfterCity.Game.World;
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
            int s = Array.IndexOf(args, "-aftercity-soak");
            if (s >= 0 && s + 1 < args.Length && float.TryParse(args[s + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var soakSeconds))
            {
                StartCoroutine(Soak(soakSeconds));
                return;
            }
            int homesArg = Array.IndexOf(args, "-aftercity-homes");
            if (homesArg >= 0 && homesArg + 1 < args.Length)
            {
                StartCoroutine(HomesTest(args[homesArg + 1]));
                return;
            }
            int huntArg = Array.IndexOf(args, "-aftercity-hunt");
            if (huntArg >= 0 && huntArg + 1 < args.Length)
            {
                StartCoroutine(HuntTest(args[huntArg + 1]));
                return;
            }
            int shotArg = Array.IndexOf(args, "-aftercity-shot");
            if (shotArg >= 0 && shotArg + 1 < args.Length)
            {
                StartCoroutine(FirstLaunchShot(args[shotArg + 1]));
                return;
            }
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
            colony.IgnoreRequirements = true; // la demo montre tous les batiments
            var farm = game.Map.FarmCenter;
            int trees = 0;
            for (int r = 1; r < 30 && trees < 10; r++)
            for (int dy = -r; dy <= r && trees < 10; dy++)
            for (int dx = -r; dx <= r && trees < 10; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                if (colony.Order(JobType.Chop, new GridPos(farm.X + dx, farm.Y + dy))) trees++;
            }
            int gathered = 0, picked = 0;
            for (int dy = -8; dy <= 8; dy++)
            for (int dx = -8; dx <= 8; dx++)
            {
                var p = new GridPos(farm.X + dx, farm.Y + dy);
                if (gathered < 3 && colony.Order(JobType.Gather, p)) gathered++;
                else if (picked < 2 && colony.Order(JobType.Pick, p)) picked++;
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
            colony.PlaceBuilding(BuildingType.ExpeditionPost, new GridPos(farm.X - 1, farm.Y - 4));
            colony.Stock.Add(ResourceType.Wood, 12);
            colony.Stock.Add(ResourceType.Stone, 6);
            colony.Stock.Add(ResourceType.Scrap, 5);
            for (int dx = -4; dx <= 4 && colony.Buildings.Count < 6; dx++)
                colony.PlaceBuilding(BuildingType.Workshop, new GridPos(farm.X + dx, farm.Y + 4 + (dx & 1)));
            for (int k = 0; k < 4; k++)
                game.Horde.Spawn(new GridPos(farm.X + 12 + k % 2, farm.Y - 3 + k * 2));
            game.Speed = 2;
            var hud = game.GetComponent<Hud>();
            if (hud != null) hud.CloseWelcome();
            var tuto = game.GetComponent<Tutorial>();
            if (tuto != null) tuto.Disable();
        }

        /// <summary>Test d'endurance : ordres de demo, vitesse x3, journal d'etat toutes les 10 s, puis on quitte.</summary>
        IEnumerator Soak(float seconds)
        {
            DemoOrders();
            var game = FindAnyObjectByType<GameRoot>();
            game.Speed = 3;
            float start = Time.realtimeSinceStartup, next = 0;
            while (Time.realtimeSinceStartup - start < seconds)
            {
                if (Time.realtimeSinceStartup - start >= next)
                {
                    var c = game.Colony;
                    Debug.Log($"[AfterCity] Soak t={Time.realtimeSinceStartup - start:F0}s jour {c.Day} {c.Hour:F1}h survivants {c.Villagers.Count}/{c.Capacity} rodeurs {game.Horde.Zombies.Count} ordres {c.Jobs.Count} bois {c.Stock.Get(ResourceType.Wood)} batiments {c.Buildings.Count} vitesse {game.Speed} fps {1f / Time.smoothDeltaTime:F0} expeditions {c.Expeditions.Count}");
                    next += 10;
                }
                yield return null;
            }
            Application.Quit(0);
        }

        /// <summary>Capture du premier lancement tel que le joueur le voit (aide affichee), puis on quitte.</summary>
        IEnumerator FirstLaunchShot(string path)
        {
            if (freeCamera != null) freeCamera.edgePan = false;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 3f) yield return null;
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            Application.Quit(0);
        }

        /// <summary>
        /// Essai de chasse : un cerf puis un sanglier (les plus proches de la ferme), camera sur le chasseur,
        /// captures chasse_cerf.png et sanglier_charge.png, verifie qu'aucun animal n'entre dans l'eau ou un mur.
        /// </summary>
        IEnumerator HuntTest(string dir)
        {
            Directory.CreateDirectory(dir);
            QualitySettings.vSyncCount = 0;
            if (freeCamera != null) freeCamera.edgePan = false;
            var game = FindAnyObjectByType<GameRoot>();
            var hud = game.GetComponent<Hud>();
            if (hud != null) hud.CloseWelcome();
            var tuto = game.GetComponent<Tutorial>();
            if (tuto != null) tuto.Disable();
            var colony = game.Colony;
            var w = colony.Wildlife;
            var grid = game.Map.Grid;
            var farm = game.Map.FarmCenter;
            int bad = 0, frames = 0;
            float fpsTime = 0;
            Animal boarAlarm = null;
            w.Alarmed += a => { if (a.Kind == AnimalKind.Boar) boarAlarm = a; };
            Debug.Log($"[AfterCity] Chasse : {w.Animals.Count} animaux");
            foreach (var kind in new[] { AnimalKind.Deer, AnimalKind.Boar })
            {
                Animal target = null;
                double best = double.MaxValue;
                foreach (var a in w.Animals)
                {
                    if (a.Kind != kind || a.IsDead) continue;
                    double d = (a.X - farm.X) * (a.X - farm.X) + (a.Y - farm.Y) * (a.Y - farm.Y);
                    if (d < best) { best = d; target = a; }
                }
                if (target == null) { Debug.Log($"[AfterCity] Chasse : pas de {kind}"); continue; }
                bool ordered = colony.OrderHunt(target);
                Debug.Log($"[AfterCity] Chasse {kind} {target.Id} en {target.Cell} ordre={ordered}");
                game.Speed = 3;
                bool shot = false;
                float t0 = Time.realtimeSinceStartup;
                boarAlarm = null;
                while (Time.realtimeSinceStartup - t0 < 120f && !target.IsDead)
                {
                    // Camera sur le chasseur (ou l'animal)
                    Vector3 focus = W(grid, target.X, target.Y);
                    foreach (var h in colony.Hunts)
                        if (h.Target == target && h.AssignedTo != null)
                        {
                            var v = h.AssignedTo;
                            focus = Vector3.Lerp(W(grid, v.X, v.Y), focus, 0.5f);
                            if (kind == AnimalKind.Boar && v.State == VillagerState.Working) game.Speed = 1;
                            if (!shot && kind == AnimalKind.Deer && v.State == VillagerState.Working)
                            {
                                game.Speed = 1;
                                freeCamera.Distance = 15f;
                                freeCamera.Focus(focus);
                                yield return Wait(0.2f);
                                freeCamera.Focus(Vector3.Lerp(W(grid, v.X, v.Y), W(grid, target.X, target.Y), 0.5f));
                                yield return null;
                                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "chasse_cerf.png"));
                                shot = true;
                                game.Speed = 3;
                                break; // la liste des chasses a pu changer pendant l attente
                            }
                        }
                    if (!shot && kind == AnimalKind.Boar && boarAlarm == target && target.State == AnimalState.Charging)
                    {
                        game.Speed = 1;
                        freeCamera.Distance = 15f;
                        freeCamera.Focus(W(grid, target.X, target.Y));
                        yield return Wait(0.25f);
                        freeCamera.Focus(W(grid, target.X, target.Y));
                        yield return null;
                        ScreenCapture.CaptureScreenshot(Path.Combine(dir, "sanglier_charge.png"));
                        shot = true;
                        game.Speed = 3;
                    }
                    if (freeCamera != null) { freeCamera.Distance = shot ? 30f : 22f; freeCamera.Focus(focus); }
                    foreach (var a in w.Animals)
                        if (!a.IsDead && !w.CanStand(a.Cell)) { bad++; if (bad < 5) Debug.Log($"[AfterCity] ANOMALIE {a.Kind} {a.Id} en {a.Cell} ({grid.GetBiome(a.Cell)})"); }
                    frames++;
                    fpsTime += Time.unscaledDeltaTime;
                    yield return null;
                }
                Debug.Log($"[AfterCity] Chasse {kind} : mort={target.IsDead} en {Time.realtimeSinceStartup - t0:F0} s, vivres {colony.Stock.Get(ResourceType.Food)}");
            }
            string report = $"anomalies={bad} fps={frames / Mathf.Max(0.01f, fpsTime):F0}";
            File.WriteAllText(Path.Combine(dir, "chasse.txt"), report);
            Debug.Log("[AfterCity] Chasse fin : " + report);
            yield return Wait(0.5f);
            Application.Quit(0);
        }

        /// <summary>Premier coin libre pres de la ferme pour ce batiment (avec une marge pour qu'il puisse s'agrandir).</summary>
        static GridPos? FreeSpot(Colony colony, GridPos farm, BuildingType type, int margin)
        {
            int size = BuildingSpec.Of(type).Size + margin;
            for (int r = 6; r < 30; r++)
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                var o = new GridPos(farm.X + dx, farm.Y + dy);
                bool ok = true;
                for (int y = -margin; y < size && ok; y++)
                for (int x = -margin; x < size && ok; x++)
                    ok = colony.CanPlace(BuildingType.Tent, new GridPos(o.X + x, o.Y + y));
                if (ok && colony.CanPlace(type, o)) return o;
            }
            return null;
        }

        /// <summary>
        /// Essai des logements : tente, abri, foyer et puits pres de la ferme avec du stock, vue en coupe, panneau,
        /// jours sautes jusqu'au confort, puis amelioration de l'abri en maison en bois (qui doit s'agrandir).
        /// </summary>
        IEnumerator HomesTest(string dir)
        {
            Directory.CreateDirectory(dir);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            if (freeCamera != null) freeCamera.edgePan = false;
            var game = FindAnyObjectByType<GameRoot>();
            var hud = game.GetComponent<Hud>();
            if (hud != null) hud.CloseWelcome();
            var tuto = game.GetComponent<Tutorial>();
            if (tuto != null) tuto.Disable();
            var colony = game.Colony;
            var farm = game.Map.FarmCenter;
            var grid = game.Map.Grid;
            colony.IgnoreRequirements = true;
            game.Speed = 1;
            yield return Wait(1.5f);
            float fpsBefore = 0; int fb = 0;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 3f) { fpsBefore += Time.unscaledDeltaTime; fb++; yield return null; }

            colony.Stock.Add(ResourceType.Food, 200);
            colony.Stock.Add(ResourceType.Wood, 300);
            colony.Stock.Add(ResourceType.Stone, 100);
            colony.Stock.Add(ResourceType.Water, 120);
            colony.Stock.Add(ResourceType.Medicine, 30);
            var placed = new System.Collections.Generic.List<Building>();
            Building shelter = null;
            foreach (var t in new[] { BuildingType.Shelter, BuildingType.Tent, BuildingType.Hostel, BuildingType.Well })
            {
                var o = FreeSpot(colony, farm, t, t == BuildingType.Shelter ? 1 : 0);
                if (!o.HasValue) { Debug.Log($"[AfterCity] Logements : pas de place pour {t}"); continue; }
                var b = colony.AddPrebuilt(t, o.Value);
                placed.Add(b);
                if (t == BuildingType.Shelter) shelter = b;
                Debug.Log($"[AfterCity] Logements : {BuildingSpec.Of(t).Name} en {o.Value}");
            }
            colony.AssignHomes();
            colony.RefillShelves();
            yield return Wait(1f);

            // Camera sur l'ensemble des logements
            var center = Vector3.zero;
            foreach (var b in placed) center += TerrainMeshBuilder.CellCenter(grid, b.Origin);
            center /= Mathf.Max(1, placed.Count);
            HomeView.RoofsHidden = true;
            freeCamera.Distance = 26f;
            freeCamera.Focus(center);
            yield return Wait(1.2f);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "maisons_coupe.png"));
            yield return Wait(0.3f);
            HomeView.RoofsHidden = false;

            if (shelter == null) { Debug.Log("[AfterCity] Logements : pas d'abri"); Application.Quit(1); yield break; }
            HomeView.Selected = game.HomeViewOf(shelter);
            freeCamera.Distance = 14f;
            freeCamera.Focus(TerrainMeshBuilder.CellCenter(grid, shelter.Origin) + new Vector3(2f, 0, 2f));
            yield return Wait(1f);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "panneau_maison.png"));
            yield return Wait(0.3f);

            // Gros plan sur l'etagere pleine
            var sv = game.HomeViewOf(shelter);
            Debug.Log("[AfterCity] Etagere : " + (sv != null ? sv.DebugShelf() : "pas de vue"));
            if (sv != null && sv.FirstCase.HasValue)
            {
                freeCamera.Distance = 7f;
                freeCamera.Yaw = 35f;
                freeCamera.Focus(sv.FirstCase.Value);
                yield return Wait(0.8f);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "etagere_zoom.png"));
                yield return Wait(0.3f);
            }

            // Jours sautes : remplissage de l'apres-midi, puis le matin (repas, consommation, confort)
            for (int day = 0; day < 6 && !shelter.Comfortable; day++)
            {
                colony.Stock.Add(ResourceType.Food, 20);
                colony.Stock.Add(ResourceType.Wood, 10);
                foreach (var h in new[] { 13.9, 14.2, 5.8, 6.2 })
                {
                    colony.SkipToHour(h);
                    yield return null;
                    yield return null;
                }
                Debug.Log($"[AfterCity] Logements : jour {colony.Day}, abri confort {shelter.ComfortDays}/{Building.DaysToComfort}, manque {GameRoot.MissingText(shelter)}, habitants {shelter.Residents.Count}");
            }
            var before = $"{shelter.Type} {shelter.Origin} taille {shelter.Size}";
            string why = colony.WhyNoUpgrade(shelter);
            bool up = colony.Upgrade(shelter);
            Debug.Log($"[AfterCity] Logements : amelioration {(up ? "OK" : "refusee : " + why)} : {before} -> {shelter.Type} {shelter.Origin} taille {shelter.Size}");
            colony.SkipToHour(10);
            yield return Wait(1.2f);
            freeCamera.Distance = 16f;
            freeCamera.Focus(TerrainMeshBuilder.CellCenter(grid, shelter.Origin) + new Vector3(3f, 0, 3f));
            yield return Wait(1f);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "amelioration.png"));
            yield return Wait(0.3f);

            HomeView.Selected = null;

            // Enclos : chantiers construits par les survivants (les betes arrivent a la fin du chantier)
            var pens = new System.Collections.Generic.List<Building>();
            foreach (var t in new[] { BuildingType.LivestockPen, BuildingType.HorsePen })
            {
                var o = FreeSpot(colony, farm, t, 0);
                var pb = o.HasValue ? colony.PlaceBuilding(t, o.Value) : null;
                Debug.Log($"[AfterCity] Enclos : {t} {(pb != null ? "chantier en " + o.Value : "impossible")}");
                if (pb != null) pens.Add(pb);
            }
            var post = FreeSpot(colony, farm, BuildingType.ExpeditionPost, 0);
            if (post.HasValue) colony.AddPrebuilt(BuildingType.ExpeditionPost, post.Value);
            game.Speed = 3;
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 90f && pens.Exists(pb => !pb.Built)) yield return null;
            game.Speed = 1;
            foreach (var pb in pens) Debug.Log($"[AfterCity] Enclos : {pb.Type} fini={pb.Built} betes={pb.Animals} duree expeditions x{colony.ExpeditionDurationFactor:F2}");
            if (pens.Count > 0)
            {
                var pc = Vector3.zero;
                foreach (var pb in pens) pc += TerrainMeshBuilder.CellCenter(grid, pb.Origin) + new Vector3(3f, 0, 3f);
                freeCamera.Distance = 24f;
                freeCamera.Focus(pc / pens.Count);
                yield return Wait(2f);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "enclos.png"));
                yield return Wait(0.3f);
            }

            float fpsAfter = 0; int fa = 0;
            t0 = Time.realtimeSinceStartup;
            freeCamera.Distance = 36f;
            freeCamera.Focus(TerrainMeshBuilder.CellCenter(grid, farm));
            while (Time.realtimeSinceStartup - t0 < 3f) { fpsAfter += Time.unscaledDeltaTime; fa++; yield return null; }
            string report = $"fps_sans_logements={fb / Mathf.Max(0.01f, fpsBefore):F0} fps_avec_logements={fa / Mathf.Max(0.01f, fpsAfter):F0} amelioration={up}";
            File.WriteAllText(Path.Combine(dir, "logements.txt"), report);
            Debug.Log("[AfterCity] Logements fin : " + report);
            yield return Wait(0.3f);
            Application.Quit(0);
        }

        static Vector3 W(WorldGrid grid, double x, double y)
        {
            var c = new GridPos((int)Math.Floor(x), (int)Math.Floor(y));
            float h = grid.InBounds(c) ? AfterCity.Game.World.TerrainMeshBuilder.TopY(grid.GetLevel(c)) : 0f;
            return new Vector3((float)x * 2f, h, (float)y * 2f);
        }

        static IEnumerator Wait(float seconds)
        {
            float t = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t < seconds) yield return null;
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
                        var g0 = FindAnyObjectByType<GameRoot>();
                        if (freeCamera != null && g0 != null) freeCamera.Focus(AfterCity.Game.World.TerrainMeshBuilder.CellCenter(g0.Map.Grid, g0.Map.FarmCenter));
                        yield return null;
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

            // Captures supplementaires : vue d'ensemble, survivants au travail de pres, foret et animaux
            var game = FindAnyObjectByType<GameRoot>();
            if (game != null && freeCamera != null)
            {
                var farmPos = AfterCity.Game.World.TerrainMeshBuilder.CellCenter(game.Map.Grid, game.Map.FarmCenter);
                float d0 = freeCamera.Distance;
                freeCamera.Distance = 95f;
                freeCamera.Focus(farmPos);
                yield return Wait(1f);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "screenshot_ensemble.png"));
                yield return Wait(0.3f);

                VillagerView worker = null;
                foreach (var v in FindObjectsByType<VillagerView>(FindObjectsSortMode.None))
                    if (v.Villager != null && v.Villager.State == VillagerState.Working) { worker = v; break; }
                if (worker == null) worker = FindAnyObjectByType<VillagerView>();
                if (worker != null)
                {
                    freeCamera.Distance = 9f;
                    freeCamera.Focus(worker.transform.position);
                    yield return Wait(0.4f);
                    freeCamera.Focus(worker.transform.position);
                    yield return null;
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, "screenshot_survivants.png"));
                    yield return Wait(0.3f);
                }
                // Un cerf ou un sanglier du moteur (pas une poule de la ferme)
                Component animal = FindAnyObjectByType<WildlifeView>();
                if (animal == null) animal = FindAnyObjectByType<AnimalView>();
                if (animal != null)
                {
                    freeCamera.Distance = 14f;
                    freeCamera.Focus(animal.transform.position);
                    yield return Wait(0.5f);
                    freeCamera.Focus(animal.transform.position);
                    yield return null;
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, "screenshot_foret.png"));
                    yield return Wait(0.3f);
                }
                freeCamera.Distance = d0;
            }

            // Capture de nuit : on saute a 21 h, les rodeurs de demonstration sont deja a la ferme
            if (game != null)
            {
                game.Colony.SkipToHour(21.5);
                if (freeCamera != null) freeCamera.Focus(AfterCity.Game.World.TerrainMeshBuilder.CellCenter(game.Map.Grid, game.Map.FarmCenter));
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
