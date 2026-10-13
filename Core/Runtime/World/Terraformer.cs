using AfterCity.Core.Sim;

namespace AfterCity.Core.World
{
    /// <summary>
    /// Ordres de terraformage (GDD § 4.5). Creuser produit de la terre, remblayer en consomme.
    /// Un niveau de case = une unité de terre.
    /// </summary>
    public sealed class Terraformer
    {
        readonly WorldGrid _grid;
        readonly Stockpile _stock;

        public Terraformer(WorldGrid grid, Stockpile stock)
        {
            _grid = grid;
            _stock = stock;
        }

        public bool CanDig(GridPos p) =>
            _grid.InBounds(p) && _grid.GetLevel(p) > 0 && _grid.GetBiome(p) != Biome.Water && !_grid.HasWall(p);

        public bool Dig(GridPos p)
        {
            if (!CanDig(p)) return false;
            _grid.SetLevel(p, _grid.GetLevel(p) - 1);
            _stock.Add(ResourceType.Earth, 1);
            return true;
        }

        public bool CanRaise(GridPos p) =>
            _grid.InBounds(p) && _grid.GetLevel(p) < WorldGrid.MaxLevel && _grid.GetBiome(p) != Biome.Water
            && !_grid.HasWall(p) && _stock.Get(ResourceType.Earth) >= 1;

        public bool Raise(GridPos p)
        {
            if (!CanRaise(p)) return false;
            _stock.TrySpend(ResourceType.Earth, 1);
            _grid.SetLevel(p, _grid.GetLevel(p) + 1);
            return true;
        }

        /// <summary>
        /// Amène chaque case du rectangle au niveau visé, autant que la terre disponible le permet.
        /// Retourne le nombre de niveaux modifiés.
        /// </summary>
        public int Flatten(GridPos min, GridPos max, int targetLevel)
        {
            int changes = 0;
            for (int y = min.Y; y <= max.Y; y++)
            for (int x = min.X; x <= max.X; x++)
            {
                var p = new GridPos(x, y);
                if (!_grid.InBounds(p)) continue;
                while (_grid.GetLevel(p) > targetLevel && Dig(p)) changes++;
                while (_grid.GetLevel(p) < targetLevel && Raise(p)) changes++;
            }
            return changes;
        }
    }
}
