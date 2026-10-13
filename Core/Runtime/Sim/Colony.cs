using System;
using System.Collections.Generic;
using AfterCity.Core.Agents;
using AfterCity.Core.Util;
using AfterCity.Core.World;

namespace AfterCity.Core.Sim
{
    /// <summary>Ordres que le joueur donne sur une case.</summary>
    public enum JobType
    {
        Chop,       // couper un arbre : bois
        Mine,       // tailler un rocher : pierre
        Salvage,    // demonter une epave : ferraille
        Dig,        // abaisser le terrain d'un niveau : terre (douves, pieges)
        Raise,      // remblayer d'un niveau : consomme de la terre
        Barricade,  // barricade de bois sur la case
        Road,       // tracer un chemin de terre
        Gather,     // ramasser des cailloux (4 cailloux = 1 pierre)
        Pick,       // cueillir des baies : vivres
        Search,     // fouiller une epave : petit butin rapide (sans la demonter)
        Asphalt,    // goudronner (technologie Asphalte, 1 pierre par case)
        Construct   // chantier d'un batiment (donne par PlaceBuilding)
    }

    public sealed class Job
    {
        public JobType Type { get; }
        public GridPos Cell { get; }
        public Villager AssignedTo { get; internal set; }
        /// <summary>Batiment a construire (ordre Construct uniquement).</summary>
        public Building Building { get; internal set; }
        /// <summary>Temps (s) pendant lequel l'ordre est ignore apres un echec de chemin.</summary>
        internal double BlockedFor;

        internal Job(JobType type, GridPos cell)
        {
            Type = type;
            Cell = cell;
        }
    }

    public enum VillagerState { Idle, Walking, Working }

    /// <summary>Un survivant. Position continue en cases (centre de la case x = x + 0,5).</summary>
    public sealed class Villager
    {
        public int Id { get; }
        public string Name { get; }
        public double X { get; internal set; }
        public double Y { get; internal set; }
        public GridPos Cell => new GridPos((int)Math.Floor(X), (int)Math.Floor(Y));
        public VillagerState State { get; internal set; }
        public Job Job { get; internal set; }
        /// <summary>Progression du travail en cours, de 0 a 1.</summary>
        public double WorkProgress { get; internal set; }
        internal double IdleTime;
        /// <summary>Direction du dernier deplacement (pour orienter le modele).</summary>
        public double FacingX { get; internal set; }
        public double FacingY { get; internal set; } = 1;
        public double Health { get; internal set; } = MaxHealth;
        public const double MaxHealth = 100;
        public bool IsDead => Health <= 0;
        /// <summary>Parti en expedition : absent de la carte.</summary>
        public bool Away { get; internal set; }

        internal readonly Queue<GridPos> Path = new Queue<GridPos>();

        internal Villager(int id, string name, GridPos cell)
        {
            Id = id;
            Name = name;
            X = cell.X + 0.5;
            Y = cell.Y + 0.5;
        }
    }

    /// <summary>
    /// La colonie : stocks, survivants et file d'ordres. Tout avance avec <see cref="Tick"/>,
    /// sans dependance a Unity (l'affichage lit l'etat et ecoute <see cref="CellChanged"/>).
    /// </summary>
    public sealed class Colony
    {
        public const double WalkSpeed = 1.6;   // cases par seconde (3,2 m/s)
        public const int BarricadeWoodCost = 2;
        /// <summary>Duree d'une journee complete en secondes de jeu.</summary>
        public const double DayLength = 240;
        public const double StartHour = 8;

        static readonly string[] Names =
            { "Marc", "Lea", "Yanis", "Ines", "Hugo", "Nora", "Tom", "Sarah", "Malik", "Jade", "Paul", "Lina" };

        public WorldGrid Grid { get; }
        public Stockpile Stock { get; } = new Stockpile();
        public Terraformer Terraformer { get; }
        public IReadOnlyList<Villager> Villagers => _villagers;
        public IReadOnlyCollection<Job> Jobs => _jobs.Values;
        /// <summary>Temps de jeu ecoule en secondes.</summary>
        public double Time { get; private set; }

        /// <summary>Heure du jour (0 a 24). La partie commence a 8 h.</summary>
        public double Hour => (Time / DayLength * 24.0 + StartHour) % 24.0;
        /// <summary>Numero du jour, a partir de 1.</summary>
        public int Day => 1 + (int)((Time / DayLength * 24.0 + StartHour) / 24.0);
        public bool IsNight => Hour >= 20.5 || Hour < 6.0;

        /// <summary>Un survivant est mort (deja retire de la liste).</summary>
        public event Action<Villager> VillagerDied;

        /// <summary>Avance l'horloge jusqu'a l'heure donnee (jour courant ou suivant), sans simuler l'intervalle.</summary>
        public void SkipToHour(double hour)
        {
            double delta = (hour - Hour + 24.0) % 24.0;
            Time += delta / 24.0 * DayLength;
        }

        /// <summary>Une case a change (relief, decor ou mur) : l'affichage doit la redessiner.</summary>
        public event Action<GridPos> CellChanged;

        /// <summary>Places de depart (la ferme et le camion).</summary>
        public const int BaseCapacity = 5;

        /// <summary>Un nouveau survivant a rejoint la colonie (a l'aube, s'il y avait de la place).</summary>
        public event Action<Villager> SurvivorArrived;

        public IReadOnlyList<Building> Buildings => _buildings;

        /// <summary>Nombre de survivants que la colonie peut loger.</summary>
        public int Capacity
        {
            get
            {
                int c = BaseCapacity;
                foreach (var b in _buildings) if (b.Built && b.Type == BuildingType.Shelter) c += 2;
                return c;
            }
        }

        /// <summary>Les survivants sans ordre flanent autour de la ferme (desactive dans les tests unitaires).</summary>
        public bool IdleWander { get; set; }

        /// <summary>Case ou arrivent les nouveaux survivants (la ferme).</summary>
        public GridPos Home { get; set; }

        readonly List<Building> _buildings = new List<Building>();
        readonly List<Expedition> _expeditions = new List<Expedition>();
        public IReadOnlyList<Expedition> Expeditions => _expeditions;
        /// <summary>Une expedition est rentree (resultat dans l'objet).</summary>
        public event Action<Expedition> ExpeditionReturned;
        public Rng Random { get; set; } = new Rng(0xC0105EED);
        /// <summary>Une epave a ete demontee (case, pieces obtenues).</summary>
        public event Action<GridPos, Dictionary<ResourceType, int>> Salvaged;
        /// <summary>Un arbre vient d'etre abattu sur cette case.</summary>
        public event Action<GridPos> TreeFelled;
        /// <summary>Une epave a ete fouillee (case, trouvailles).</summary>
        public event Action<GridPos, Dictionary<ResourceType, int>> Searched;

        public const int PebblesPerStone = 4;
        public const int BerryFood = 3;

        /// <summary>Ajoute des cailloux ; chaque tas de 4 devient automatiquement 1 pierre.</summary>
        public void AddPebbles(int n)
        {
            Stock.Add(ResourceType.Pebble, n);
            while (Stock.Get(ResourceType.Pebble) >= PebblesPerStone)
            {
                Stock.TrySpend(ResourceType.Pebble, PebblesPerStone);
                Stock.Add(ResourceType.Stone, 1);
            }
        }

        /// <summary>Recolte de la ferme de depart et de chaque champ, chaque soir a 18 h.</summary>
        public const int FarmYield = 4;
        public const int FieldYield = 4;
        public const double HarvestHour = 18;
        public const double StarvationDamage = 25;

        /// <summary>Recolte du soir (vivres gagnes).</summary>
        public event Action<int> Harvested;
        /// <summary>Repas du matin : vivres manges, nombre de survivants qui n'ont rien eu.</summary>
        public event Action<int, int> Ate;
        /// <summary>La colonie a une ferme de depart (recolte de base).</summary>
        public bool HasFarm { get; set; }
        double _lastHour = -1;

        /// <summary>Vivres recoltes chaque soir.</summary>
        public int DailyHarvest
        {
            get
            {
                int n = HasFarm ? FarmYield : 0;
                foreach (var b in _buildings) if (b.Built && b.Type == BuildingType.Field) n += FieldYield;
                return n;
            }
        }

        static bool Crossed(double prev, double now, double h) =>
            prev >= 0 && (prev <= now ? prev < h && now >= h : prev < h || now >= h);

        void TickFood()
        {
            double h = Hour;
            if (Crossed(_lastHour, h, HarvestHour))
            {
                int n = DailyHarvest;
                Stock.Add(ResourceType.Food, n);
                Harvested?.Invoke(n);
            }
            if (Crossed(_lastHour, h, 6.0))
            {
                int eaters = PresentCount, eaten = 0, hungry = 0;
                var list = new List<Villager>(_villagers);
                foreach (var v in list)
                {
                    if (v.Away) continue;
                    if (Stock.TrySpend(ResourceType.Food, 1)) { eaten++; Heal(v, 5); }
                    else { hungry++; Hurt(v, StarvationDamage); }
                }
                Ate?.Invoke(eaten, hungry);
            }
            _lastHour = h;
        }

        /// <summary>Atelier : demonte 5 ferrailles en 3 pieces tirees selon leur rarete. Null si impossible.</summary>
        public Dictionary<ResourceType, int> RefineScrap()
        {
            if (!HasBuilt(BuildingType.Workshop) || !Stock.TrySpend(ResourceType.Scrap, AfterCity.Core.Sim.Salvage.ScrapPerBatch)) return null;
            var parts = AfterCity.Core.Sim.Salvage.Roll(Random, AfterCity.Core.Sim.Salvage.PartsPerBatch);
            foreach (var kv in parts) Stock.Add(kv.Key, kv.Value);
            return parts;
        }
        bool _wasNight;
        int _nextVillagerId;
        readonly List<Villager> _villagers = new List<Villager>();
        readonly Dictionary<GridPos, Job> _jobs = new Dictionary<GridPos, Job>();
        readonly Pathfinder _pathfinder;

        public Colony(WorldGrid grid)
        {
            Grid = grid;
            Terraformer = new Terraformer(grid, Stock);
            _pathfinder = new Pathfinder(grid);
        }

        /// <summary>Colonie de depart sur la ferme : survivants et vivres du camion (GDD 4.2).</summary>
        public static Colony StartAtFarm(GeneratedMap map, int survivors = 5)
        {
            var c = new Colony(map.Grid) { Home = map.FarmCenter, HasFarm = true, IdleWander = true, Random = new Rng(map.Seed ^ 0xE4BED17105UL) };
            for (int i = 0; i < survivors; i++)
            {
                var p = new GridPos(map.FarmCenter.X - 1 + i % 3, map.FarmCenter.Y - 1 + i / 3);
                c.AddVillager(p);
            }
            c.AddPrebuilt(BuildingType.Farmhouse, new GridPos(map.FarmCenter.X + 2, map.FarmCenter.Y + 2));
            c.Stock.Add(ResourceType.Wood, 10);
            c.Stock.Add(ResourceType.Food, 30);
            return c;
        }

        public Villager AddVillager(GridPos cell)
        {
            int id = _nextVillagerId++;
            var v = new Villager(id, Names[id % Names.Length], cell);
            _villagers.Add(v);
            return v;
        }

/// <summary>Blesse un survivant ; a 0 il meurt et quitte la colonie.</summary>
        public void Hurt(Villager v, double damage)
        {
            if (v.IsDead) return;
            v.Health -= damage;
            if (!v.IsDead) return;
            if (v.Job != null) Release(v.Job);
            _villagers.Remove(v);
            VillagerDied?.Invoke(v);
        }

        /// <summary>Soigne un survivant (sans depasser sa sante maximale).</summary>
        public void Heal(Villager v, double amount = Villager.MaxHealth)
        {
            if (v.IsDead) return;
            v.Health = Math.Min(Villager.MaxHealth, v.Health + amount);
        }

        /// <summary>Signale un changement de case fait hors de la colonie (ex. barricade detruite par la horde).</summary>
        public void NotifyCellChanged(GridPos p) => CellChanged?.Invoke(p);

        public Job JobAt(GridPos p) => _jobs.TryGetValue(p, out var j) ? j : null;

        /// <summary>Peut-on donner cet ordre sur cette case maintenant ?</summary>
        public bool CanOrder(JobType type, GridPos p)
        {
            if (!Grid.InBounds(p) || _jobs.ContainsKey(p) || Grid.HasStructure(p) || PlannedAt(p) != null) return false;
            var feature = Grid.GetFeature(p);
            switch (type)
            {
                case JobType.Chop: return feature == CellFeature.Tree;
                case JobType.Mine: return feature == CellFeature.Rock;
                case JobType.Salvage: return feature == CellFeature.Wreck || feature == CellFeature.WreckSearched;
                case JobType.Gather: return feature == CellFeature.Pebbles;
                case JobType.Pick: return feature == CellFeature.Berries;
                case JobType.Search: return feature == CellFeature.Wreck;
                case JobType.Dig: return feature == CellFeature.None && Terraformer.CanDig(p);
                case JobType.Raise:
                    return feature == CellFeature.None && Grid.GetBiome(p) != Biome.Water && !Grid.HasWall(p)
                           && Grid.GetLevel(p) < WorldGrid.MaxLevel && Stock.Get(ResourceType.Earth) > PendingCount(JobType.Raise);
                case JobType.Asphalt:
                    return HasTech(TechType.Asphalt) && feature == CellFeature.None && Grid.GetBiome(p) != Biome.Water && !Grid.HasWall(p)
                           && Grid.GetRoad(p) != RoadType.Asphalt && Stock.Get(ResourceType.Stone) >= 1;
                case JobType.Road:
                    return feature == CellFeature.None && Grid.GetBiome(p) != Biome.Water && !Grid.HasWall(p)
                           && Grid.GetRoad(p) == RoadType.None;
                case JobType.Barricade:
                    return feature == CellFeature.None && Grid.GetBiome(p) != Biome.Water && !Grid.HasWall(p)
                           && Stock.Get(ResourceType.Wood) >= BarricadeWoodCost && !IsOccupied(p);
                default: return false;
            }
        }

        /// <summary>Donne un ordre. La barricade paie son bois tout de suite (rendu si l'ordre est annule).</summary>
        public bool Order(JobType type, GridPos p)
        {
            if (!CanOrder(type, p)) return false;
            if (type == JobType.Barricade) Stock.TrySpend(ResourceType.Wood, BarricadeWoodCost);
            if (type == JobType.Asphalt) Stock.TrySpend(ResourceType.Stone, 1);
            _jobs[p] = new Job(type, p);
            return true;
        }

        /// <summary>Batiment (prevu ou construit) qui couvre cette case.</summary>
        public Building BuildingAt(GridPos p)
        {
            foreach (var b in _buildings) if (b.Covers(p)) return b;
            return null;
        }

        Building PlannedAt(GridPos p)
        {
            var b = BuildingAt(p);
            return b != null && !b.Built ? b : null;
        }

        /// <summary>Peut-on poser ce batiment avec ce coin bas-gauche ? Terrain plat, sec, degage, et assez de ressources.</summary>
        public bool CanPlace(BuildingType type, GridPos origin)
        {
            var spec = BuildingSpec.Of(type);
            if (!IsUnlocked(type)) return false;
            if (Stock.Get(ResourceType.Wood) < spec.Wood || Stock.Get(ResourceType.Stone) < spec.Stone) return false;
            if (!Grid.InBounds(origin)) return false;
            int level = Grid.GetLevel(origin);
            for (int y = 0; y < spec.Size; y++)
            for (int x = 0; x < spec.Size; x++)
            {
                var p = new GridPos(origin.X + x, origin.Y + y);
                if (!Grid.InBounds(p) || Grid.GetLevel(p) != level || Grid.GetBiome(p) == Biome.Water) return false;
                if (Grid.GetFeature(p) != CellFeature.None || Grid.HasWall(p) || Grid.HasStructure(p)) return false;
                if (_jobs.ContainsKey(p) || BuildingAt(p) != null || IsOccupied(p)) return false;
            }
            return true;
        }

        /// <summary>Ajoute un batiment deja construit (decor de depart).</summary>
        public Building AddPrebuilt(BuildingType type, GridPos origin)
        {
            var b = new Building(type, origin) { Built = true };
            for (int y = 0; y < b.Size; y++)
            for (int x = 0; x < b.Size; x++)
            {
                var p = new GridPos(origin.X + x, origin.Y + y);
                if (Grid.InBounds(p)) Grid.SetStructure(p, Building.StructureCode(type, x == 0 && y == 0));
            }
            _buildings.Add(b);
            return b;
        }

        /// <summary>Pose un chantier : les ressources sont payees tout de suite (rendues si on annule).</summary>
        public Building PlaceBuilding(BuildingType type, GridPos origin)
        {
            if (!CanPlace(type, origin)) return null;
            var spec = BuildingSpec.Of(type);
            Stock.TrySpend(ResourceType.Wood, spec.Wood);
            Stock.TrySpend(ResourceType.Stone, spec.Stone);
            var b = new Building(type, origin);
            _buildings.Add(b);
            _jobs[origin] = new Job(JobType.Construct, origin) { Building = b };
            return b;
        }

        public bool Cancel(GridPos p)
        {
            var planned = PlannedAt(p);
            if (planned != null) p = planned.Origin;
            if (!_jobs.TryGetValue(p, out var job)) return false;
            if (job.Type == JobType.Barricade) Stock.Add(ResourceType.Wood, BarricadeWoodCost);
            if (job.Type == JobType.Asphalt) Stock.Add(ResourceType.Stone, 1);
            if (job.Type == JobType.Construct && job.Building != null)
            {
                var spec = BuildingSpec.Of(job.Building.Type);
                Stock.Add(ResourceType.Wood, spec.Wood);
                Stock.Add(ResourceType.Stone, spec.Stone);
                _buildings.Remove(job.Building);
            }
            Release(job);
            _jobs.Remove(p);
            return true;
        }

        int PendingCount(JobType type)
        {
            int n = 0;
            foreach (var j in _jobs.Values) if (j.Type == type) n++;
            return n;
        }

        bool IsOccupied(GridPos p)
        {
            foreach (var v in _villagers) if (!v.Away && v.Cell.Equals(p)) return true;
            return false;
        }

        /// <summary>Ignore l'arbre de progression (demo, tests visuels).</summary>
        public bool IgnoreRequirements { get; set; }
        readonly HashSet<TechType> _techs = new HashSet<TechType>();

        public bool IsUnlocked(BuildingType type)
        {
            var req = BuildingSpec.Of(type).Requires;
            return IgnoreRequirements || !req.HasValue || HasBuilt(req.Value);
        }

        public bool HasTech(TechType t) => IgnoreRequirements || _techs.Contains(t);

        public bool CanResearch(TechType t)
        {
            var s = TechSpec.Of(t);
            return !_techs.Contains(t) && HasBuilt(s.RequiresBuilding)
                   && Stock.Get(ResourceType.Scrap) >= s.Scrap && Stock.Get(ResourceType.Stone) >= s.Stone;
        }

        /// <summary>Recherche une technologie (immediat pour l'instant) en payant son cout.</summary>
        public bool Research(TechType t)
        {
            if (!CanResearch(t)) return false;
            var s = TechSpec.Of(t);
            Stock.TrySpend(ResourceType.Scrap, s.Scrap);
            Stock.TrySpend(ResourceType.Stone, s.Stone);
            _techs.Add(t);
            return true;
        }

        public bool HasBuilt(BuildingType type)
        {
            foreach (var b in _buildings) if (b.Built && b.Type == type) return true;
            return false;
        }

        /// <summary>Survivants presents (pas en expedition).</summary>
        public int PresentCount
        {
            get { int n = 0; foreach (var v in _villagers) if (!v.Away) n++; return n; }
        }

        /// <summary>Chances de reussite si on envoyait <paramref name="count"/> survivants maintenant.</summary>
        public int PreviewChance(ExpeditionTarget target, int count) => Expedition.ComputeChance(target, PickMembers(count));

        public bool CanSendExpedition(int count) =>
            HasBuilt(BuildingType.ExpeditionPost) && count >= 1 && count <= Expedition.MaxMembers && PresentCount - count >= 1;

        /// <summary>Envoie les survivants les plus en forme (on en garde toujours un a la ferme).</summary>
        public Expedition SendExpedition(ExpeditionTarget target, int count)
        {
            if (!CanSendExpedition(count)) return null;
            var members = PickMembers(count);
            foreach (var m in members)
            {
                if (m.Job != null) Release(m.Job);
                m.Path.Clear();
                m.State = VillagerState.Idle;
                m.Away = true;
            }
            var e = new Expedition(target, members);
            _expeditions.Add(e);
            return e;
        }

        List<Villager> PickMembers(int count)
        {
            var list = new List<Villager>();
            foreach (var v in _villagers) if (!v.Away) list.Add(v);
            list.Sort((a, b) => b.Health.CompareTo(a.Health));
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
            return list;
        }

        void TickExpeditions(double dt)
        {
            for (int i = _expeditions.Count - 1; i >= 0; i--)
            {
                var e = _expeditions[i];
                e.Remaining -= dt;
                if (!e.Done) continue;
                e.Resolve(Random);
                foreach (var kv in e.Gains) Stock.Add(kv.Key, kv.Value);
                int k = 0;
                foreach (var m in e.Members)
                {
                    if (m.IsDead) continue;
                    m.Health = Math.Max(10, m.Health - e.DamageTaken * (0.6 + 0.8 * Random.NextDouble()));
                    m.Away = false;
                    m.X = Home.X + 0.5 + (k % 2);
                    m.Y = Home.Y + 0.5 + (k / 2);
                    k++;
                }
                _expeditions.RemoveAt(i);
                ExpeditionReturned?.Invoke(e);
            }
        }

        public static double WorkDuration(JobType type)
        {
            switch (type)
            {
                case JobType.Chop: return 3.0;
                case JobType.Mine: return 4.5;
                case JobType.Salvage: return 5.0;
                case JobType.Dig: return 2.5;
                case JobType.Raise: return 2.5;
                case JobType.Barricade: return 4.0;
                case JobType.Road: return 1.5;
                case JobType.Asphalt: return 2.5;
                case JobType.Gather: return 1.6;
                case JobType.Pick: return 2.0;
                case JobType.Search: return 2.5;
                default: return 3.0;
            }
        }

        double JobDuration(Job job) =>
            job.Type == JobType.Construct && job.Building != null ? BuildingSpec.Of(job.Building.Type).BuildTime : WorkDuration(job.Type);

        public void Tick(double dt)
        {
            Time += dt;
            bool night = IsNight;
            if (_wasNight && !night && _villagers.Count < Capacity && Grid.InBounds(Home))
            {
                var v = AddVillager(Home);
                SurvivorArrived?.Invoke(v);
            }
            _wasNight = night;
            TickExpeditions(dt);
            TickFood();
            foreach (var j in _jobs.Values)
                if (j.BlockedFor > 0) j.BlockedFor -= dt;

            foreach (var v in _villagers)
            {
                if (v.Away) continue;
                switch (v.State)
                {
                    case VillagerState.Idle:
                        TryAssign(v);
                        if (v.State == VillagerState.Idle && IdleWander) Wander(v, dt);
                        break;
                    case VillagerState.Walking:
                        if (v.Job == null) TryAssign(v); // une balade s'interrompt pour un ordre
                        if (v.State == VillagerState.Walking) Walk(v, dt);
                        break;
                    case VillagerState.Working: Work(v, dt); break;
                }
            }
        }

        void TryAssign(Villager v)
        {
            // L'ordre libre le plus proche (distance a vol d'oiseau), puis on verifie qu'un chemin existe.
            var candidates = new List<Job>();
            foreach (var j in _jobs.Values)
                if (j.AssignedTo == null && j.BlockedFor <= 0) candidates.Add(j);
            if (candidates.Count == 0) return;
            var here = v.Cell;
            candidates.Sort((a, b) => Dist(here, a.Cell).CompareTo(Dist(here, b.Cell)));

            for (int k = 0; k < candidates.Count && k < 4; k++)
            {
                var job = candidates[k];
                var path = _pathfinder.FindPath(here, WorkSpots(job));
                if (path == null)
                {
                    job.BlockedFor = 5;
                    continue;
                }
                job.AssignedTo = v;
                v.Job = job;
                v.Path.Clear();
                foreach (var p in path) v.Path.Enqueue(p);
                v.State = v.Path.Count > 0 ? VillagerState.Walking : VillagerState.Working;
                v.WorkProgress = 0;
                return;
            }
        }

        /// <summary>De temps en temps, un survivant inoccupe va faire quelques pas pres de la ferme.</summary>
        void Wander(Villager v, double dt)
        {
            v.IdleTime += dt;
            if (v.IdleTime < 4 + (v.Id % 4)) return;
            v.IdleTime = 0;
            if (!Grid.InBounds(Home)) return;
            int r = IsNight ? 2 : MapGenerator.FarmRadius;
            var target = new GridPos(Home.X + Random.Range(-r, r + 1), Home.Y + Random.Range(-r, r + 1));
            if (!Grid.InBounds(target) || Grid.GetLevel(target) != Grid.GetLevel(Home) || Grid.HasStructure(target) || Grid.HasWall(target)) return;
            var path = _pathfinder.FindPath(v.Cell, new[] { target }, 2000);
            if (path == null || path.Count == 0 || path.Count > 12) return;
            foreach (var p in path) v.Path.Enqueue(p);
            v.State = VillagerState.Walking;
        }

        /// <summary>
        /// Cases d'ou l'on peut travailler : une voisine, ou la case elle-meme pour la recolte.
        /// On ne creuse jamais sous ses pieds (sinon on se piege dans sa propre fosse).
        /// </summary>
        List<GridPos> WorkSpots(Job job)
        {
            var spots = new List<GridPos>(8);
            if (job.Type == JobType.Construct && job.Building != null)
            {
                // Autour de l'emprise du batiment
                var b = job.Building;
                for (int y = -1; y <= b.Size; y++)
                for (int x = -1; x <= b.Size; x++)
                {
                    if (x >= 0 && y >= 0 && x < b.Size && y < b.Size) continue;
                    if (Math.Abs(x - (b.Size - 1) / 2.0) + Math.Abs(y - (b.Size - 1) / 2.0) > b.Size) continue; // pas les coins
                    var n = new GridPos(b.Origin.X + x, b.Origin.Y + y);
                    if (Grid.InBounds(n) && Grid.GetBiome(n) != Biome.Water && !Grid.HasWall(n) && !Grid.HasStructure(n)) spots.Add(n);
                }
                return spots;
            }
            bool onCell = job.Type == JobType.Chop || job.Type == JobType.Mine || job.Type == JobType.Salvage || job.Type == JobType.Road || job.Type == JobType.Asphalt
                          || job.Type == JobType.Gather || job.Type == JobType.Pick || job.Type == JobType.Search;
            if (onCell && Grid.GetBiome(job.Cell) != Biome.Water) spots.Add(job.Cell);
            foreach (var d in GridPos.Neighbours4)
            {
                var n = job.Cell + d;
                if (Grid.InBounds(n) && Grid.GetBiome(n) != Biome.Water && !Grid.HasWall(n)) spots.Add(n);
            }
            return spots;
        }

        static int Dist(GridPos a, GridPos b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        void Walk(Villager v, double dt)
        {
            double remaining = WalkSpeed * Roads.SpeedFactor(Grid.GetRoad(v.Cell)) * dt;
            while (remaining > 0 && v.Path.Count > 0)
            {
                var next = v.Path.Peek();
                // Le terrain a pu changer depuis le calcul du chemin
                if (!next.Equals(v.Cell) && !_pathfinder.CanWalk(v.Cell, next))
                {
                    Abort(v);
                    return;
                }
                double tx = next.X + 0.5, ty = next.Y + 0.5;
                double dx = tx - v.X, dy = ty - v.Y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d > 1e-6) { v.FacingX = dx / d; v.FacingY = dy / d; }
                if (d <= remaining)
                {
                    v.X = tx; v.Y = ty;
                    remaining -= d;
                    v.Path.Dequeue();
                }
                else
                {
                    v.X += dx / d * remaining;
                    v.Y += dy / d * remaining;
                    remaining = 0;
                }
            }
            if (v.Path.Count == 0)
            {
                if (v.Job == null) { v.State = VillagerState.Idle; return; }
                v.State = VillagerState.Working;
                v.WorkProgress = 0;
                double fx = v.Job.Cell.X + 0.5 - v.X, fy = v.Job.Cell.Y + 0.5 - v.Y;
                double fd = Math.Sqrt(fx * fx + fy * fy);
                if (fd > 1e-6) { v.FacingX = fx / fd; v.FacingY = fy / fd; }
            }
        }

        void Work(Villager v, double dt)
        {
            var job = v.Job;
            if (job == null || !_jobs.ContainsKey(job.Cell)) { Abort(v); return; }
            v.WorkProgress += dt / JobDuration(job);
            if (v.WorkProgress < 1) return;

            bool done = Complete(job);
            _jobs.Remove(job.Cell);
            v.Job = null;
            v.State = VillagerState.Idle;
            v.WorkProgress = 0;
            if (done) CellChanged?.Invoke(job.Cell);
        }

        bool Complete(Job job)
        {
            var p = job.Cell;
            switch (job.Type)
            {
                case JobType.Chop:
                    if (Grid.GetFeature(p) != CellFeature.Tree) return false;
                    Grid.SetFeature(p, CellFeature.None);
                    Stock.Add(ResourceType.Wood, 3);
                    TreeFelled?.Invoke(p);
                    return true;
                case JobType.Mine:
                    if (Grid.GetFeature(p) != CellFeature.Rock) return false;
                    Grid.SetFeature(p, CellFeature.None);
                    Stock.Add(ResourceType.Stone, 3);
                    return true;
                case JobType.Gather:
                    if (Grid.GetFeature(p) != CellFeature.Pebbles) return false;
                    Grid.SetFeature(p, CellFeature.None);
                    AddPebbles(2 + Random.Range(0, 2));
                    return true;
                case JobType.Pick:
                    if (Grid.GetFeature(p) != CellFeature.Berries) return false;
                    Grid.SetFeature(p, CellFeature.None);
                    Stock.Add(ResourceType.Food, BerryFood);
                    return true;
                case JobType.Search:
                {
                    if (Grid.GetFeature(p) != CellFeature.Wreck) return false;
                    Grid.SetFeature(p, CellFeature.WreckSearched);
                    var found = new Dictionary<ResourceType, int>();
                    double roll = Random.NextDouble();
                    if (roll < 0.45) found[ResourceType.Food] = 1 + Random.Range(0, 2);
                    else if (roll < 0.85) { var part = AfterCity.Core.Sim.Salvage.Roll(Random); found[part] = 1; }
                    else found[ResourceType.Medicine] = 1;
                    if (Random.NextDouble() < 0.4) found[ResourceType.Scrap] = 1;
                    foreach (var kv in found) Stock.Add(kv.Key, kv.Value);
                    Searched?.Invoke(p, found);
                    return true;
                }
                case JobType.Salvage:
                {
                    var wf = Grid.GetFeature(p);
                    if (wf != CellFeature.Wreck && wf != CellFeature.WreckSearched) return false;
                    Grid.SetFeature(p, CellFeature.None);
                    var loot = AfterCity.Core.Sim.Salvage.Wreck(Random);
                    foreach (var kv in loot) Stock.Add(kv.Key, kv.Value);
                    Salvaged?.Invoke(p, loot);
                    return true;
                }
                case JobType.Dig:
                    return Terraformer.Dig(p);
                case JobType.Raise:
                    return Terraformer.Raise(p);
                case JobType.Asphalt:
                    Grid.SetRoad(p, RoadType.Asphalt);
                    return true;
                case JobType.Road:
                    if (Grid.GetRoad(p) != RoadType.None) return false;
                    Grid.SetRoad(p, RoadType.Dirt);
                    return true;
                case JobType.Construct:
                {
                    var b = job.Building;
                    if (b == null) return false;
                    for (int y = 0; y < b.Size; y++)
                    for (int x = 0; x < b.Size; x++)
                    {
                        var c = new GridPos(b.Origin.X + x, b.Origin.Y + y);
                        Grid.SetStructure(c, Building.StructureCode(b.Type, x == 0 && y == 0));
                        if (x != 0 || y != 0) CellChanged?.Invoke(c);
                    }
                    b.Built = true;
                    return true;
                }
                case JobType.Barricade:
                    if (Grid.HasWall(p) || IsOccupied(p)) { Stock.Add(ResourceType.Wood, BarricadeWoodCost); return false; }
                    Grid.SetWall(p, true);
                    return true;
                default:
                    return false;
            }
        }

        void Abort(Villager v)
        {
            if (v.Job != null)
            {
                v.Job.BlockedFor = 2;
                Release(v.Job);
            }
            v.Path.Clear();
            v.State = VillagerState.Idle;
            v.WorkProgress = 0;
            // Recentrer sur la case pour repartir proprement
            v.X = v.Cell.X + 0.5;
            v.Y = v.Cell.Y + 0.5;
        }

        static void Release(Job job)
        {
            if (job.AssignedTo != null)
            {
                var v = job.AssignedTo;
                v.Job = null;
                v.Path.Clear();
                v.State = VillagerState.Idle;
                v.WorkProgress = 0;
            }
            job.AssignedTo = null;
        }
    }
}
