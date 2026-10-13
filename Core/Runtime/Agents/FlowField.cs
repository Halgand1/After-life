using System;
using System.Collections.Generic;
using AfterCity.Core.World;

namespace AfterCity.Core.Agents
{
    /// <summary>
    /// Champ de flux : distance de chaque case jusqu'à la cible (le QG), calculée une fois pour toute la horde.
    /// Chaque zombie descend simplement vers la case voisine la plus proche de la cible.
    /// Les murs sont franchissables à coût élevé : la horde les attaque si c'est le chemin le moins cher.
    /// </summary>
    public sealed class FlowField
    {
        public const int Unreachable = int.MaxValue;
        public const int StepCost = 10;
        public const int WallCost = 400;

        readonly WorldGrid _grid;
        readonly int _maxClimb;
        readonly int[] _cost;

        public FlowField(WorldGrid grid, int maxClimb)
        {
            _grid = grid;
            _maxClimb = maxClimb;
            _cost = new int[grid.Width * grid.Height];
        }

        public int CostAt(GridPos p) => _cost[p.Y * _grid.Width + p.X];
        public bool IsReachable(GridPos p) => _grid.InBounds(p) && CostAt(p) != Unreachable;

        /// <summary>Recalcule le champ vers les cases cibles (Dijkstra inversé).</summary>
        public void Build(IEnumerable<GridPos> targets)
        {
            for (int i = 0; i < _cost.Length; i++) _cost[i] = Unreachable;
            var open = new SortedSet<(int cost, int index)>();

            foreach (var t in targets)
            {
                if (!_grid.InBounds(t)) continue;
                int i = t.Y * _grid.Width + t.X;
                _cost[i] = 0;
                open.Add((0, i));
            }

            while (open.Count > 0)
            {
                var current = open.Min;
                open.Remove(current);
                var c = new GridPos(current.index % _grid.Width, current.index / _grid.Width);
                if (current.cost > _cost[current.index]) continue;

                foreach (var d in GridPos.Neighbours4)
                {
                    var n = c + d;
                    // Le marcheur va de n vers c : on vérifie le pas dans ce sens.
                    if (!_grid.InBounds(n) || !_grid.CanStep(n, c, _maxClimb)) continue;
                    int ni = n.Y * _grid.Width + n.X;
                    int newCost = current.cost + (_grid.HasWall(c) ? WallCost : StepCost);
                    if (newCost >= _cost[ni]) continue;
                    if (_cost[ni] != Unreachable) open.Remove((_cost[ni], ni));
                    _cost[ni] = newCost;
                    open.Add((newCost, ni));
                }
            }
        }

        /// <summary>Prochaine case à prendre depuis <paramref name="from"/>, ou null si aucune n'avance.</summary>
        public GridPos? NextStep(GridPos from)
        {
            if (!IsReachable(from)) return null;
            GridPos? best = null;
            int bestCost = CostAt(from);
            foreach (var d in GridPos.Neighbours4)
            {
                var n = from + d;
                if (!_grid.InBounds(n) || !_grid.CanStep(from, n, _maxClimb)) continue;
                int c = CostAt(n);
                if (c < bestCost)
                {
                    bestCost = c;
                    best = n;
                }
            }
            return best;
        }
    }
}
