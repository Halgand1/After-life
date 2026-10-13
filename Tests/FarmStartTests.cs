using AfterCity.Core.Util;
using AfterCity.Core.World;
using NUnit.Framework;

namespace AfterCity.Core.Tests
{
    public class FarmStartTests
    {
        [TestCase(1337UL)]
        [TestCase(1UL)]
        [TestCase(42UL)]
        public void FarmHasStartingResources(ulong seed)
        {
            var map = MapGenerator.Generate(128, 128, seed);
            FarmStart.Ensure(map, new Rng(seed + 1));
            Assert.That(FarmStart.Count(map, CellFeature.Berries, 6, 20), Is.GreaterThanOrEqualTo(FarmStart.Berries));
            Assert.That(FarmStart.Count(map, CellFeature.Pebbles, 4, 15), Is.GreaterThanOrEqualTo(FarmStart.Pebbles));
            Assert.That(FarmStart.Count(map, CellFeature.Tree, 8, 18), Is.GreaterThanOrEqualTo(FarmStart.Trees));
            Assert.That(FarmStart.Count(map, CellFeature.Rock, 10, 22), Is.GreaterThanOrEqualTo(FarmStart.Rocks));
        }

        [Test]
        public void FarmYardStaysClear()
        {
            var map = MapGenerator.Generate(128, 128, 7);
            FarmStart.Ensure(map, new Rng(8));
            for (int y = -MapGenerator.FarmRadius; y <= MapGenerator.FarmRadius; y++)
            for (int x = -MapGenerator.FarmRadius; x <= MapGenerator.FarmRadius; x++)
            {
                var p = new GridPos(map.FarmCenter.X + x, map.FarmCenter.Y + y);
                Assert.That(map.Grid.GetFeature(p), Is.EqualTo(CellFeature.None));
            }
        }

        [Test]
        public void SameSeedSameResult()
        {
            var a = MapGenerator.Generate(96, 96, 5);
            var b = MapGenerator.Generate(96, 96, 5);
            Assert.That(FarmStart.Ensure(a, new Rng(9)), Is.EqualTo(FarmStart.Ensure(b, new Rng(9))));
            for (int y = 0; y < 96; y++)
            for (int x = 0; x < 96; x++)
                Assert.That(b.Grid.GetFeature(new GridPos(x, y)), Is.EqualTo(a.Grid.GetFeature(new GridPos(x, y))));
        }
    }
}
