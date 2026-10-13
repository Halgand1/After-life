using System;
using System.Collections.Generic;
using AfterCity.Core.Agents;
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
        Barricade   // barricade de bois sur la case
    }

    public sealed class Job
    {
        public JobType Type { get; }
        public GridPos Cell { get; }
        public Villager AssignedTo { get; internal set; }
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
        /// <summary>Direction du dernier deplacement (pour orienter le modele).</summary>
        public double FacingX { get; internal set; }
        public double FacingY { get; internal set; } = 1;
        public double Health { get; internal set; } = MaxHealth;
        public const double MaxHealth = 100;
        public bool IsDead => Health <= 0;

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
            var c = new Colony(map.Grid);
            for (int i = 0; i < survivors; i++)
            {
                var p = new GridPos(map.FarmCenter.X - 1 + i % 3, map.FarmCenter.Y - 1 + i / 3);
                c.AddVillager(p);
            }
            c.Stock.Add(ResourceType.Wood, 10);
            c.Stock.Add(ResourceType.Food, 30);
            return c;
        }

        public Villager AddVillager(GridPos cell)
        {
            var v = new Villager(_villagers.Count, Names[_villagers.Count % Names.Length], cell);
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

        /// <summary>Signale un changement de case fait hors de la colonie (ex. barricade detruite par la horde).</summary>
        public void NotifyCellChanged(GridPos p) => CellChanged?.Invoke(p);

        public Job JobAt(GridPos p) => _jobs.TryGetValue(p, out var j) ? j : null;

        /// <summary>Peut-on donner cet ordre sur cette case maintenant ?</summary>
        public bool CanOrder(JobType type, GridPos p)
        {
            if (!Grid.InBounds(p) || _jobs.ContainsKey(p)) return false;
            var feature = Grid.GetFeature(p);
            switch (type)
            {
                case JobType.Chop: return feature == CellFeature.Tree;
                case JobType.Mine: return feature == CellFeature.Rock;
                case JobType.Salvage: return feature == CellFeature.Wreck;
                case JobType.Dig: return feature == CellFeature.None && Terraformer.CanDig(p);
                case JobType.Raise:
                    return feature == CellFeature.None && Grid.GetBiome(p) != Biome.Water && !Grid.HasWall(p)
                           && Grid.GetLevel(p) < WorldGrid.MaxLevel && Stock.Get(ResourceType.Earth) > PendingCount(JobType.Raise);
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
            _jobs[p] = new Job(type, p);
            return true;
        }

        public bool Cancel(GridPos p)
        {
            if (!_jobs.TryGetValue(p, out var job)) return false;
            if (job.Type == JobType.Barricade) Stock.Add(ResourceType.Wood, BarricadeWoodCost);
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
            foreach (var v in _villagers) if (v.Cell.Equals(p)) return true;
            return false;
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
                default: return 3.0;
            }
        }

        public void Tick(double dt)
        {
            Time += dt;
            foreach (var j in _jobs.Values)
                if (j.BlockedFor > 0) j.BlockedFor -= dt;

            foreach (var v in _villagers)
            {
                switch (v.State)
                {
                    case VillagerState.Idle: TryAssign(v); break;
                    case VillagerState.Walking: Walk(v, dt); break;
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

        /// <summary>
        /// Cases d'ou l'on peut travailler : une voisine, ou la case elle-meme pour la recolte.
        /// On ne creuse jamais sous ses pieds (sinon on se piege dans sa propre fosse).
        /// </summary>
        List<GridPos> WorkSpots(Job job)
        {
            var spots = new List<GridPos>(5);
            bool onCell = job.Type == JobType.Chop || job.Type == JobType.Mine || job.Type == JobType.Salvage;
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
            double remaining = WalkSpeed * dt;
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
            v.WorkProgress += dt / WorkDuration(job.Type);
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
                    return true;
                case JobType.Mine:
                    if (Grid.GetFeature(p) != CellFeature.Rock) return false;
                    Grid.SetFeature(p, CellFeature.None);
                    Stock.Add(ResourceType.Stone, 3);
                    return true;
                case JobType.Salvage:
                    if (Grid.GetFeature(p) != CellFeature.Wreck) return false;
                    Grid.SetFeature(p, CellFeature.None);
                    Stock.Add(ResourceType.Scrap, 4);
                    return true;
                case JobType.Dig:
                    return Terraformer.Dig(p);
                case JobType.Raise:
                    return Terraformer.Raise(p);
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
