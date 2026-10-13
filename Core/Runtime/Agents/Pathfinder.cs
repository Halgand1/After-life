using System;
using System.Collections.Generic;
using AfterCity.Core.World;

namespace AfterCity.Core.Agents
{
    /// <summary>
    /// Recherche de chemin A* pour les survivants : 4 directions, montee limitee, eau et murs infranchissables.
    /// </summary>
    public sealed class Pathfinder
    {
        public const int SurvivorClimb = 1;

        readonly WorldGrid _grid;
        readonly int _maxClimb;
        readonly int[] _gScore;
        readonly int[] _cameFrom;
        readonly int[] _stamp;
        int _run;

        public Pathfinder(WorldGrid grid, int maxClimb = SurvivorClimb)
        {
            _grid = grid;
            _maxClimb = maxClimb;
            int n = grid.Width * grid.Height;
            _gScore = new int[n];
            _cameFrom = new int[n];
            _stamp = new int[n];
        }

        public bool CanWalk(GridPos from, GridPos to) =>
            _grid.CanStep(from, to, _maxClimb) && !_grid.HasWall(to);

        /// <summary>
        /// Chemin de <paramref name="start"/> jusqu'a l'une des cases <paramref name="goals"/> (depart exclu, arrivee incluse).
        /// Liste vide si on y est deja, null si aucune case n'est atteignable.
        /// </summary>
        public List<GridPos> FindPath(GridPos start, IReadOnlyCollection<GridPos> goals, int maxExpanded = 20000)
        {
            if (!_grid.InBounds(start) || goals.Count == 0) return null;
            foreach (var g in goals)
                if (g.Equals(start)) return new List<GridPos>();

            _run++;
            int w = _grid.Width;
            var open = new SortedSet<(int f, int index)>();
            int si = start.Y * w + start.X;
            Touch(si);
            _gScore[si] = 0;
            open.Add((Heuristic(start, goals), si));
            int expanded = 0;

            while (open.Count > 0 && expanded++ < maxExpanded)
            {
                var cur = open.Min;
                open.Remove(cur);
                var c = new GridPos(cur.index % w, cur.index / w);
                foreach (var g in goals)
                    if (g.Equals(c)) return Rebuild(si, cur.index);

                foreach (var d in GridPos.Neighbours4)
                {
                    var n = c + d;
                    if (!_grid.InBounds(n) || !CanWalk(c, n)) continue;
                    int ni = n.Y * w + n.X;
                    // Les routes coutent moins cher : les survivants les empruntent d'eux-memes
                    int tentative = _gScore[cur.index] + (int)Math.Round(10 / Roads.SpeedFactor(_grid.GetRoad(n)));
                    if (_stamp[ni] == _run && tentative >= _gScore[ni]) continue;
                    if (_stamp[ni] == _run) open.Remove((_gScore[ni] + Heuristic(n, goals), ni));
                    Touch(ni);
                    _gScore[ni] = tentative;
                    _cameFrom[ni] = cur.index;
                    open.Add((tentative + Heuristic(n, goals), ni));
                }
            }
            return null;
        }

        void Touch(int i)
        {
            if (_stamp[i] == _run) return;
            _stamp[i] = _run;
            _gScore[i] = int.MaxValue;
            _cameFrom[i] = -1;
        }

        static int Heuristic(GridPos p, IReadOnlyCollection<GridPos> goals)
        {
            int best = int.MaxValue;
            foreach (var g in goals)
                best = Math.Min(best, (Math.Abs(g.X - p.X) + Math.Abs(g.Y - p.Y)) * 5); // admissible meme sur asphalte
            return best;
        }

        List<GridPos> Rebuild(int startIndex, int endIndex)
        {
            var path = new List<GridPos>();
            int w = _grid.Width;
            for (int i = endIndex; i != startIndex; i = _cameFrom[i])
                path.Add(new GridPos(i % w, i / w));
            path.Reverse();
            return path;
        }
    }
}
