using System;
using System.Collections.Generic;
using AfterCity.Core.Util;
using AfterCity.Core.World;

namespace AfterCity.Core.Sim
{
    public enum AnimalKind { Deer, Boar }

    public enum AnimalState { Idle, Walking, Fleeing, Charging, Dead }

    /// <summary>Un cerf ou un sanglier. Position continue en cases, comme les survivants.</summary>
    public sealed class Animal
    {
        public int Id { get; }
        public AnimalKind Kind { get; }
        public double X { get; internal set; }
        public double Y { get; internal set; }
        public GridPos Cell => new GridPos((int)Math.Floor(X), (int)Math.Floor(Y));
        public AnimalState State { get; internal set; }
        public double Health { get; internal set; }
        public bool IsDead => State == AnimalState.Dead;
        /// <summary>Centre de son territoire : il erre autour.</summary>
        public GridPos Home { get; internal set; }
        public double FacingX { get; internal set; }
        public double FacingY { get; internal set; } = 1;
        /// <summary>Survivant qui l'a attaque (le sanglier le charge, le cerf le fuit).</summary>
        public Villager Attacker { get; internal set; }

        /// <summary>Temps restant pendant lequel l'animal reste affole apres une attaque.</summary>
        internal double Alarm;
        internal double Wait;
        internal double Cooldown;
        internal double TargetX, TargetY;

        internal Animal(int id, AnimalKind kind, GridPos cell)
        {
            Id = id;
            Kind = kind;
            Home = cell;
            X = cell.X + 0.5;
            Y = cell.Y + 0.5;
            Health = kind == AnimalKind.Deer ? Wildlife.DeerHealth : Wildlife.BoarHealth;
        }
    }

    /// <summary>
    /// Faune de foret dense (GDD 4.6) : neutre tant qu'on ne l'attaque pas.
    /// Attaque, le cerf fuit ; le sanglier charge l'attaquant et peut le blesser.
    /// </summary>
    public sealed class Wildlife
    {
        public const double DeerHealth = 30;
        public const double BoarHealth = 60;
        public const double WanderSpeed = 0.7;      // cases par seconde
        public const double DeerFleeSpeed = 3.2;    // plus rapide qu'un survivant (1,6)
        public const double BoarChargeSpeed = 2.4;
        public const int WanderRadius = 5;
        public const double DeerAlarm = 8;          // secondes de fuite apres une attaque
        public const double BoarAlarm = 12;         // secondes de colere apres une attaque
        public const double BoarGiveUpDistance = 12;
        public const double ChargeHitRange = 0.6;
        public const double ChargeCooldown = 2.5;
        public const double ChargeInjuryChance = 0.35;
        public const double SevereShare = 0.2;
        public const double ChargeDamage = 25;
        public const double SevereChargeDamage = 50;
        /// <summary>A trois survivants ou plus autour de la cible, la charge blesse deux fois moins souvent.</summary>
        public const int GroupSize = 3;
        public const int DeerMeat = 8;
        public const int BoarMeat = 6;

        readonly WorldGrid _grid;
        readonly List<Animal> _animals = new List<Animal>();
        int _nextId;

        public Rng Random { get; set; }
        public IReadOnlyList<Animal> Animals => _animals;

        /// <summary>Un animal vient de mourir (animal, survivant qui l'a abattu).</summary>
        public event Action<Animal, Villager> Killed;
        /// <summary>Un sanglier a percute un survivant (sanglier, survivant, degats ; 0 si esquive).</summary>
        public event Action<Animal, Villager, double> Charged;
        /// <summary>Un animal vient d'etre attaque ou effraye (pour les messages et les sons).</summary>
        public event Action<Animal> Alarmed;

        public Wildlife(WorldGrid grid, Rng random)
        {
            _grid = grid;
            Random = random;
        }

        public Animal Spawn(AnimalKind kind, GridPos cell)
        {
            var a = new Animal(_nextId++, kind, cell);
            a.Wait = Random.NextDouble() * 4;
            _animals.Add(a);
            return a;
        }

        /// <summary>
        /// Troupeaux en lisiere de foret dense, loin de la ferme (decor/peuplement.json) :
        /// cerfs par 2 a 4, sangliers par 1 a 3.
        /// </summary>
        public static Wildlife Populate(GeneratedMap map, Rng random)
        {
            var w = new Wildlife(map.Grid, random);
            var g = map.Grid;
            var edges = new List<GridPos>();
            int forestCells = 0;
            for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
            {
                var p = new GridPos(x, y);
                if (g.GetBiome(p) != Biome.DenseForest) continue;
                forestCells++;
                if (!w.CanStand(p)) continue;
                foreach (var d in GridPos.Neighbours4)
                {
                    var n = p + d;
                    if (g.InBounds(n) && g.GetBiome(n) != Biome.DenseForest && g.GetBiome(n) != Biome.Water) { edges.Add(p); break; }
                }
            }
            if (edges.Count == 0) return w;
            int deerHerds = Math.Clamp(forestCells / 400, 2, 12);
            int boarGroups = Math.Clamp(forestCells / 600, 1, 8);
            w.PlaceGroups(edges, map.FarmCenter, AnimalKind.Deer, deerHerds, 2, 4, 25);
            w.PlaceGroups(edges, map.FarmCenter, AnimalKind.Boar, boarGroups, 1, 3, 35);
            return w;
        }

        void PlaceGroups(List<GridPos> edges, GridPos farm, AnimalKind kind, int groups, int min, int max, int minDistance)
        {
            var far = new List<GridPos>();
            // Sur une petite carte, on rapproche les troupeaux plutot que de ne rien faire apparaitre
            for (int dist = minDistance; dist >= 10 && far.Count == 0; dist -= 5)
                foreach (var p in edges)
                    if (Math.Abs(p.X - farm.X) + Math.Abs(p.Y - farm.Y) >= dist) far.Add(p);
            if (far.Count == 0) return;
            for (int h = 0; h < groups; h++)
            {
                var home = far[Random.Range(0, far.Count)];
                int n = Random.Range(min, max + 1);
                for (int i = 0; i < n; i++)
                {
                    var cell = home;
                    for (int k = 0; k < 8; k++)
                    {
                        var c = new GridPos(home.X + Random.Range(-1, 2), home.Y + Random.Range(-1, 2));
                        if (CanStand(c)) { cell = c; break; }
                    }
                    var a = Spawn(kind, cell);
                    a.Home = home;
                }
            }
        }

        /// <summary>Case ou un animal peut se tenir : ni eau, ni mur, ni batiment, ni tronc, ni rocher.</summary>
        public bool CanStand(GridPos p)
        {
            if (!_grid.InBounds(p) || _grid.GetBiome(p) == Biome.Water || _grid.HasWall(p) || _grid.HasStructure(p)) return false;
            var f = _grid.GetFeature(p);
            return f != CellFeature.Tree && f != CellFeature.Rock;
        }

        bool CanEnter(GridPos from, GridPos to) =>
            to.Equals(from) || (CanStand(to) && _grid.CanStep(from, to, 1));

        /// <summary>
        /// Un survivant attaque l'animal. Le cerf s'enfuit, le sanglier charge.
        /// Retourne vrai si l'animal meurt.
        /// </summary>
        public bool Attack(Animal a, Villager by, double damage)
        {
            if (a.IsDead) return false;
            a.Health -= damage;
            if (a.Health <= 0)
            {
                a.Health = 0;
                a.State = AnimalState.Dead;
                a.Attacker = by;
                Killed?.Invoke(a, by);
                return true;
            }
            Provoke(a, by);
            return false;
        }

        /// <summary>L'animal est effraye ou provoque sans etre blesse (tir manque, coup rate).</summary>
        public void Provoke(Animal a, Villager by)
        {
            if (a.IsDead) return;
            a.Attacker = by;
            if (a.Kind == AnimalKind.Deer)
            {
                a.Alarm = DeerAlarm;
                a.State = AnimalState.Fleeing;
            }
            else
            {
                a.Alarm = BoarAlarm;
                if (a.Cooldown <= 0) a.State = AnimalState.Charging;
            }
            Alarmed?.Invoke(a);
        }

        public Animal NearestAlive(double x, double y, double maxDistance)
        {
            Animal best = null;
            double bd = maxDistance * maxDistance;
            foreach (var a in _animals)
            {
                if (a.IsDead) continue;
                double dx = a.X - x, dy = a.Y - y, d = dx * dx + dy * dy;
                if (d <= bd) { bd = d; best = a; }
            }
            return best;
        }

        public void Tick(double dt, Colony colony)
        {
            foreach (var a in _animals)
            {
                if (a.IsDead) continue;
                if (a.Cooldown > 0) a.Cooldown -= dt;
                if (a.Alarm > 0)
                {
                    a.Alarm -= dt;
                    if (a.Alarm <= 0 || a.Attacker == null || a.Attacker.IsDead || a.Attacker.Away) Calm(a);
                }
                switch (a.State)
                {
                    case AnimalState.Fleeing: Flee(a, dt); break;
                    case AnimalState.Charging: Charge(a, dt, colony); break;
                    case AnimalState.Walking: Stroll(a, dt); break;
                    default:
                        a.Wait -= dt;
                        if (a.Wait > 0) break;
                        if (a.Alarm > 0 && a.Kind == AnimalKind.Boar && a.Cooldown <= 0) { a.State = AnimalState.Charging; break; }
                        PickWanderTarget(a);
                        break;
                }
            }
        }

        void Calm(Animal a)
        {
            a.Alarm = 0;
            a.Attacker = null;
            if (a.State == AnimalState.Fleeing || a.State == AnimalState.Charging)
            {
                a.State = AnimalState.Idle;
                a.Wait = 2 + Random.NextDouble() * 3;
                // Il s'installe la ou sa fuite l'a mene
                if (CanStand(a.Cell)) a.Home = a.Cell;
            }
        }

        void PickWanderTarget(Animal a)
        {
            for (int k = 0; k < 8; k++)
            {
                var c = new GridPos(a.Home.X + Random.Range(-WanderRadius, WanderRadius + 1), a.Home.Y + Random.Range(-WanderRadius, WanderRadius + 1));
                if (!CanStand(c)) continue;
                a.TargetX = c.X + 0.2 + Random.NextDouble() * 0.6;
                a.TargetY = c.Y + 0.2 + Random.NextDouble() * 0.6;
                a.State = AnimalState.Walking;
                return;
            }
            a.Wait = 1;
        }

        void Stroll(Animal a, double dt)
        {
            double dx = a.TargetX - a.X, dy = a.TargetY - a.Y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 0.05 || !Move(a, dx / d, dy / d, Math.Min(d, WanderSpeed * dt)))
            {
                a.State = AnimalState.Idle;
                a.Wait = 3 + Random.NextDouble() * 6;
            }
        }

        void Flee(Animal a, double dt)
        {
            var from = a.Attacker;
            double dx = a.X - from.X, dy = a.Y - from.Y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 1e-6) { dx = a.FacingX; dy = a.FacingY; d = 1; }
            dx /= d; dy /= d;
            double step = DeerFleeSpeed * dt;
            // Droit devant, sinon en biais : un cerf ne reste pas coince contre la riviere
            double[] angles = { 0, 0.6, -0.6, 1.2, -1.2, 1.8, -1.8 };
            foreach (var t in angles)
            {
                double c = Math.Cos(t), s = Math.Sin(t);
                if (Move(a, dx * c - dy * s, dx * s + dy * c, step)) return;
            }
        }

        void Charge(Animal a, double dt, Colony colony)
        {
            var v = a.Attacker;
            if (v == null || v.IsDead || v.Away) { Calm(a); return; }
            double dx = v.X - a.X, dy = v.Y - a.Y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d > BoarGiveUpDistance) { Calm(a); return; }
            if (d <= ChargeHitRange)
            {
                Hit(a, v, colony);
                return;
            }
            if (!Move(a, dx / d, dy / d, Math.Min(d, BoarChargeSpeed * dt)))
            {
                // Bloque (mur, eau, fosse) : il souffle un peu puis recommence
                a.State = AnimalState.Idle;
                a.Wait = ChargeCooldown;
                a.Cooldown = ChargeCooldown;
            }
        }

        void Hit(Animal a, Villager v, Colony colony)
        {
            double chance = ChargeInjuryChance;
            if (colony != null && GroupAround(colony, v) >= GroupSize) chance /= 2;
            double damage = 0;
            if (Random.NextDouble() < chance)
                damage = Random.NextDouble() < SevereShare ? SevereChargeDamage : ChargeDamage;
            if (damage > 0 && colony != null) colony.Hurt(v, damage);
            Charged?.Invoke(a, v, damage);
            // Le sanglier recule et reprend son souffle avant une nouvelle charge
            a.State = AnimalState.Idle;
            a.Cooldown = ChargeCooldown;
            a.Wait = ChargeCooldown;
        }

        static int GroupAround(Colony colony, Villager v)
        {
            int n = 0;
            foreach (var o in colony.Villagers)
            {
                if (o.Away) continue;
                double dx = o.X - v.X, dy = o.Y - v.Y;
                if (dx * dx + dy * dy <= 9) n++;
            }
            return n;
        }

        /// <summary>Avance d'un pas dans la direction donnee ; faux si la case d'arrivee est interdite.</summary>
        bool Move(Animal a, double dirX, double dirY, double step)
        {
            double nx = a.X + dirX * step, ny = a.Y + dirY * step;
            var to = new GridPos((int)Math.Floor(nx), (int)Math.Floor(ny));
            if (!CanEnter(a.Cell, to)) return false;
            a.X = nx;
            a.Y = ny;
            a.FacingX = dirX;
            a.FacingY = dirY;
            return true;
        }
    }
}
