using AfterCity.Core.Agents;
using AfterCity.Core.World;
using NUnit.Framework;

namespace AfterCity.Core.Tests
{
    /// <summary>Vérifie la règle centrale de défense : une douve de 2 niveaux arrête les rôdeurs.</summary>
    public class FlowFieldTests
    {
        const int Size = 21;
        static readonly GridPos Hq = new GridPos(10, 10);

        static WorldGrid FlatGrid(int level = 4)
        {
            var g = new WorldGrid(Size, Size);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                g.SetLevel(new GridPos(x, y), level);
            return g;
        }

        /// <summary>Creuse un anneau carré de profondeur donnée autour du QG.</summary>
        static void DigMoatRing(WorldGrid g, int radius, int depth)
        {
            for (int y = Hq.Y - radius; y <= Hq.Y + radius; y++)
            for (int x = Hq.X - radius; x <= Hq.X + radius; x++)
            {
                bool onRing = System.Math.Max(System.Math.Abs(x - Hq.X), System.Math.Abs(y - Hq.Y)) == radius;
                if (onRing) g.SetLevel(new GridPos(x, y), g.GetLevel(new GridPos(x, y)) - depth);
            }
        }

        [Test]
        public void OnFlatGroundEveryCellReachesHq()
        {
            var ff = new FlowField(FlatGrid(), maxClimb: 1);
            ff.Build(new[] { Hq });
            Assert.That(ff.IsReachable(new GridPos(0, 0)), Is.True);
            Assert.That(ff.NextStep(new GridPos(0, 10)), Is.EqualTo(new GridPos(1, 10)));
        }

        [Test]
        public void TwoLevelMoatStopsWalkers()
        {
            var g = FlatGrid();
            DigMoatRing(g, radius: 5, depth: 2);
            var ff = new FlowField(g, maxClimb: 1);
            ff.Build(new[] { Hq });
            Assert.That(ff.IsReachable(new GridPos(0, 0)), Is.False, "un rôdeur dehors ne doit pas atteindre le QG");
            Assert.That(ff.IsReachable(new GridPos(10, 7)), Is.True, "l'intérieur reste relié au QG");
        }

        [Test]
        public void OneLevelDitchDoesNotStopWalkers()
        {
            var g = FlatGrid();
            DigMoatRing(g, radius: 5, depth: 1);
            var ff = new FlowField(g, maxClimb: 1);
            ff.Build(new[] { Hq });
            Assert.That(ff.IsReachable(new GridPos(0, 0)), Is.True);
        }

        [Test]
        public void ClimbersCrossATwoLevelMoat()
        {
            var g = FlatGrid();
            DigMoatRing(g, radius: 5, depth: 2);
            var ff = new FlowField(g, maxClimb: 2);
            ff.Build(new[] { Hq });
            Assert.That(ff.IsReachable(new GridPos(0, 0)), Is.True, "le Grimpeur franchit 1 niveau de plus");
        }

        [Test]
        public void RampLetsWalkersInAgain()
        {
            var g = FlatGrid();
            DigMoatRing(g, radius: 5, depth: 2);
            // Rampe : on remonte une case de la douve d'un niveau, ce qui crée des marches de 1.
            g.SetLevel(new GridPos(10, 5), g.GetLevel(new GridPos(10, 5)) + 1);
            var ff = new FlowField(g, maxClimb: 1);
            ff.Build(new[] { Hq });
            Assert.That(ff.IsReachable(new GridPos(0, 0)), Is.True);
        }

        [Test]
        public void WallsAreCrossedOnlyAtHighCost()
        {
            var g = FlatGrid();
            for (int x = 0; x < Size; x++) g.SetWall(new GridPos(x, 5), true);
            var ff = new FlowField(g, maxClimb: 1);
            ff.Build(new[] { Hq });
            int outside = ff.CostAt(new GridPos(10, 4));
            Assert.That(outside, Is.GreaterThanOrEqualTo(FlowField.WallCost), "il faut casser le mur pour passer");
        }

        [Test]
        public void WaterIsImpassable()
        {
            var g = FlatGrid();
            for (int y = 0; y < Size; y++) g.SetBiome(new GridPos(3, y), Biome.Water);
            var ff = new FlowField(g, maxClimb: 1);
            ff.Build(new[] { Hq });
            Assert.That(ff.IsReachable(new GridPos(0, 10)), Is.False);
        }
    }
}
