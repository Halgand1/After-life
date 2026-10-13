using AfterCity.Core.World;
using NUnit.Framework;

namespace AfterCity.Core.Tests
{
    public class MapGeneratorTests
    {
        [Test]
        public void SameSeedGivesSameMap()
        {
            var a = MapGenerator.Generate(96, 96, 1234).Grid;
            var b = MapGenerator.Generate(96, 96, 1234).Grid;
            for (int y = 0; y < 96; y++)
            for (int x = 0; x < 96; x++)
            {
                var p = new GridPos(x, y);
                Assert.That(b.GetLevel(p), Is.EqualTo(a.GetLevel(p)));
                Assert.That(b.GetBiome(p), Is.EqualTo(a.GetBiome(p)));
            }
        }

        [Test]
        public void DifferentSeedsGiveDifferentMaps()
        {
            var a = MapGenerator.Generate(96, 96, 1).Grid;
            var b = MapGenerator.Generate(96, 96, 2).Grid;
            int differences = 0;
            for (int y = 0; y < 96; y++)
            for (int x = 0; x < 96; x++)
                if (a.GetLevel(new GridPos(x, y)) != b.GetLevel(new GridPos(x, y))) differences++;
            Assert.That(differences, Is.GreaterThan(500));
        }

        [TestCase(7UL)]
        [TestCase(42UL)]
        [TestCase(2026UL)]
        public void FarmSitsOnFlatDryPlains(ulong seed)
        {
            var map = MapGenerator.Generate(128, 128, seed);
            int level = map.Grid.GetLevel(map.FarmCenter);
            for (int y = -MapGenerator.FarmRadius; y <= MapGenerator.FarmRadius; y++)
            for (int x = -MapGenerator.FarmRadius; x <= MapGenerator.FarmRadius; x++)
            {
                var p = new GridPos(map.FarmCenter.X + x, map.FarmCenter.Y + y);
                Assert.That(map.Grid.GetBiome(p), Is.EqualTo(Biome.Plains));
                Assert.That(map.Grid.GetLevel(p), Is.EqualTo(level));
            }
        }

        [Test]
        public void CornersAreDenseForestOrWater()
        {
            var g = MapGenerator.Generate(128, 128, 99).Grid;
            foreach (var c in new[] { new GridPos(0, 0), new GridPos(127, 0), new GridPos(0, 127), new GridPos(127, 127) })
                Assert.That(g.GetBiome(c), Is.AnyOf(Biome.DenseForest, Biome.Water));
        }
    }
}
