using System;
using System.Collections.Generic;
using AfterCity.Core.Agents;
using AfterCity.Core.Util;
using AfterCity.Core.World;

namespace AfterCity.Core.Sim
{
    public enum ZombieState { Moving, Smashing, Feeding, Trapped }

    /// <summary>Un rodeur : lent, ne monte qu'une marche de 1 niveau, casse les barricades.</summary>
    public sealed class Zombie
    {
        public int Id { get; }
        public double X { get; internal set; }
        public double Y { get; internal set; }
        public GridPos Cell => new GridPos((int)Math.Floor(X), (int)Math.Floor(Y));
        public double Health { get; internal set; } = 60;
        public ZombieState State { get; internal set; }
        public double FacingX { get; internal set; }
        public double FacingY { get; internal set; } = 1;
        public bool IsDead => Health <= 0;

        internal Zombie(int id, GridPos cell)
        {
            Id = id;
            X = cell.X + 0.5;
            Y = cell.Y + 0.5;
        }
    }

    /// <summary>
    /// Les hordes de la nuit (remplacent les raids vikings de K&amp;C). Chaque nuit une vague sort de la foret
    /// et marche vers la ferme en suivant un champ de flux. Les barricades sont frappees jusqu'a ceder,
    /// une fosse de 2 niveaux piege les rodeurs, et les survivants se defendent au corps a corps.
    /// Au lever du jour, les rodeurs restants s'effondrent.
    /// </summary>
    public sealed class Horde
    {
        public const double Speed = 0.7;           // cases par seconde
        public const double WallHealth = 100;
        public const double SmashDamage = 12;      // par seconde, sur une barricade
        public const double BiteDamage = 18;       // par seconde, sur un survivant
        public const double DefenseDamage = 14;    // par seconde, d'un survivant au contact
        public const double ContactRange = 0.7;    // en cases
        public const double TowerRange = 7;        // en cases
        public const double TowerDamage = 15;      // par seconde
        const int Climb = 1;

        public IReadOnlyList<Zombie> Zombies => _zombies;
        /// <summary>Cible actuelle de chaque tour de guet (pour l'affichage des tirs).</summary>
        public IReadOnlyDictionary<Building, Zombie> TowerTargets => _towerTargets;
        readonly Dictionary<Building, Zombie> _towerTargets = new Dictionary<Building, Zombie>();
        public int WavesSpawned { get; private set; }
        /// <summary>Une vague arrive (nombre de rodeurs).</summary>
        public event Action<int> WaveStarted;

        readonly Colony _colony;
        readonly WorldGrid _grid;
        readonly GridPos _target;
        readonly Rng _rng;
        readonly FlowField _flow;
        readonly List<Zombie> _zombies = new List<Zombie>();
        readonly Dictionary<GridPos, double> _wallDamage = new Dictionary<GridPos, double>();
        bool _flowDirty = true;
        bool _wasNight;
        int _nextId;

        public Horde(Colony colony, GridPos target, ulong seed)
        {
            _colony = colony;
            _grid = colony.Grid;
            _target = target;
            _rng = new Rng(seed ^ 0x2041_B1E5UL);
            _flow = new FlowField(_grid, Climb);
            colony.CellChanged += _ => _flowDirty = true;
            _wasNight = colony.IsNight;
        }

        /// <summary>Taille de la vague de la nuit n (1, 2, 3...).</summary>
        public static int WaveSize(int night) => 3 + 2 * (night - 1);

        public double WallDamageAt(GridPos p) => _wallDamage.TryGetValue(p, out var d) ? d : 0;
        /// <summary>Barricades en cours d'attaque et degats recus (0 a WallHealth).</summary>
        public IEnumerable<KeyValuePair<GridPos, double>> DamagedWalls => _wallDamage;

        public void Tick(double dt)
        {
            bool night = _colony.IsNight;
            if (night && !_wasNight) SpawnWave(WaveSize(WavesSpawned + 1));
            if (!night && _wasNight) _zombies.Clear(); // le soleil se leve : la horde s'effondre
            _wasNight = night;
            if (_zombies.Count == 0) return;

            if (_flowDirty)
            {
                _flow.Build(TargetCells());
                _flowDirty = false;
            }

            for (int i = _zombies.Count - 1; i >= 0; i--)
            {
                var z = _zombies[i];
                Step(z, dt);
                if (z.IsDead) _zombies.RemoveAt(i);
            }

            // Les tours de guet tirent sur le rodeur le plus proche
            _towerTargets.Clear();
            foreach (var b in _colony.Buildings)
            {
                if (!b.Built || b.Type != BuildingType.Watchtower) continue;
                double tx = b.Origin.X + 0.5, ty = b.Origin.Y + 0.5;
                Zombie target = null;
                double best = TowerRange;
                foreach (var z in _zombies)
                {
                    double d = Distance(tx, ty, z.X, z.Y);
                    if (d <= best) { best = d; target = z; }
                }
                if (target == null) continue;
                target.Health -= TowerDamage * dt;
                _towerTargets[b] = target;
            }

            // Les survivants au contact rendent les coups
            foreach (var v in _colony.Villagers)
                foreach (var z in _zombies)
                    if (Distance(v.X, v.Y, z.X, z.Y) < ContactRange)
                    {
                        z.Health -= DefenseDamage * dt;
                        break;
                    }
            _zombies.RemoveAll(z => z.IsDead);
        }

        IEnumerable<GridPos> TargetCells()
        {
            int r = MapGenerator.FarmRadius;
            for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
            {
                var p = new GridPos(_target.X + x, _target.Y + y);
                if (_grid.InBounds(p)) yield return p;
            }
        }

        /// <summary>Fait sortir une vague par un bord de la carte choisi au hasard.</summary>
        public void SpawnWave(int count)
        {
            WavesSpawned++;
            int side = _rng.Range(0, 4);
            int spawned = 0;
            for (int attempt = 0; attempt < count * 20 && spawned < count; attempt++)
            {
                int along = _rng.Range(0, side < 2 ? _grid.Width : _grid.Height);
                int depth = _rng.Range(1, 4);
                GridPos p;
                switch (side)
                {
                    case 0: p = new GridPos(along, depth); break;
                    case 1: p = new GridPos(along, _grid.Height - 1 - depth); break;
                    case 2: p = new GridPos(depth, along); break;
                    default: p = new GridPos(_grid.Width - 1 - depth, along); break;
                }
                if (!_grid.InBounds(p) || _grid.GetBiome(p) == Biome.Water || _grid.HasWall(p)) continue;
                _zombies.Add(new Zombie(_nextId++, p));
                spawned++;
            }
            _flowDirty = true;
            WaveStarted?.Invoke(spawned);
        }

        /// <summary>Ajoute un rodeur a un endroit precis (tests, evenements).</summary>
        public Zombie Spawn(GridPos p)
        {
            var z = new Zombie(_nextId++, p);
            _zombies.Add(z);
            _flowDirty = true;
            return z;
        }

        void Step(Zombie z, double dt)
        {
            // 1. Un survivant a portee : on mord
            Villager prey = null;
            double best = double.MaxValue;
            foreach (var v in _colony.Villagers)
            {
                double d = Distance(v.X, v.Y, z.X, z.Y);
                if (d < best) { best = d; prey = v; }
            }
            if (prey != null && best < ContactRange)
            {
                z.State = ZombieState.Feeding;
                Face(z, prey.X - z.X, prey.Y - z.Y);
                _colony.Hurt(prey, BiteDamage * dt);
                return;
            }

            var here = z.Cell;
            GridPos? next;
            // 2. Sur la ferme ou tout pres d'un survivant : on fonce sur lui
            if (prey != null && (best < 6 || _flow.CostAt(here) == 0))
                next = GreedyStep(here, prey.Cell);
            // 3. Sinon on suit le champ de flux ; sans chemin, on marche droit vers la ferme (et on tombe dans les fosses)
            else
                next = _flow.IsReachable(here) && _flow.CostAt(here) > 0 ? _flow.NextStep(here) : GreedyStep(here, _target);

            if (next == null)
            {
                z.State = IsTrapped(here) ? ZombieState.Trapped : ZombieState.Moving;
                return;
            }
            var n = next.Value;
            if (_grid.HasWall(n))
            {
                z.State = ZombieState.Smashing;
                Face(z, n.X + 0.5 - z.X, n.Y + 0.5 - z.Y);
                double dmg = WallDamageAt(n) + SmashDamage * dt;
                if (dmg >= WallHealth)
                {
                    _wallDamage.Remove(n);
                    _grid.SetWall(n, false);
                    _colony.NotifyCellChanged(n);
                }
                else _wallDamage[n] = dmg;
                return;
            }

            z.State = ZombieState.Moving;
            double tx = n.X + 0.5, ty = n.Y + 0.5;
            double dx = tx - z.X, dy = ty - z.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double move = Speed * dt;
            Face(z, dx, dy);
            if (dist <= move) { z.X = tx; z.Y = ty; }
            else { z.X += dx / dist * move; z.Y += dy / dist * move; }
        }

        /// <summary>Case voisine qui rapproche le plus du but, en respectant la montee maximale (les murs sont permis : on les casse).</summary>
        GridPos? GreedyStep(GridPos from, GridPos goal)
        {
            if (from.Equals(goal)) return goal;
            GridPos? best = null;
            int bestD = Math.Abs(goal.X - from.X) + Math.Abs(goal.Y - from.Y);
            foreach (var d in GridPos.Neighbours4)
            {
                var n = from + d;
                if (!_grid.CanStep(from, n, Climb)) continue;
                int dist = Math.Abs(goal.X - n.X) + Math.Abs(goal.Y - n.Y);
                if (dist < bestD) { bestD = dist; best = n; }
            }
            return best;
        }

        /// <summary>Bloque au fond d'une fosse : une paroi trop haute a cote de soi.</summary>
        bool IsTrapped(GridPos p)
        {
            foreach (var d in GridPos.Neighbours4)
            {
                var n = p + d;
                if (_grid.InBounds(n) && _grid.GetLevel(n) - _grid.GetLevel(p) > Climb) return true;
            }
            return false;
        }

        static void Face(Zombie z, double dx, double dy)
        {
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 1e-6) return;
            z.FacingX = dx / d;
            z.FacingY = dy / d;
        }

        static double Distance(double ax, double ay, double bx, double by)
        {
            double dx = ax - bx, dy = ay - by;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
