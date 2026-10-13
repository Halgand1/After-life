using AfterCity.Core.Sim;
using AfterCity.Core.World;
using NUnit.Framework;

namespace AfterCity.Core.Tests
{
    public class ForagingTests
    {
        static Colony Flat()
        {
            var g = new WorldGrid(16, 16);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                g.SetLevel(new GridPos(x, y), 3);
            var c = new Colony(g);
            c.AddVillager(new GridPos(1, 1));
            return c;
        }

        static void Run(Colony c, double s) { for (double t = 0; t < s; t += 0.05) c.Tick(0.05); }

        [Test]
        public void FourPebblesMakeOneStone()
        {
            var c = Flat();
            c.AddPebbles(3);
            Assert.That(c.Stock.Get(ResourceType.Stone), Is.EqualTo(0));
            c.AddPebbles(2);
            Assert.That(c.Stock.Get(ResourceType.Stone), Is.EqualTo(1));
            Assert.That(c.Stock.Get(ResourceType.Pebble), Is.EqualTo(1));
        }

        [Test]
        public void GatheringPebblesClearsTheCell()
        {
            var c = Flat();
            var p = new GridPos(5, 5);
            c.Grid.SetFeature(p, CellFeature.Pebbles);
            Assert.That(c.Order(JobType.Gather, p), Is.True);
            Run(c, 10);
            Assert.That(c.Grid.GetFeature(p), Is.EqualTo(CellFeature.None));
            Assert.That(c.Stock.Get(ResourceType.Pebble) + c.Stock.Get(ResourceType.Stone) * Colony.PebblesPerStone, Is.InRange(2, 3));
        }

        [Test]
        public void PickingBerriesGivesFood()
        {
            var c = Flat();
            var p = new GridPos(4, 6);
            c.Grid.SetFeature(p, CellFeature.Berries);
            c.Order(JobType.Pick, p);
            Run(c, 10);
            Assert.That(c.Stock.Get(ResourceType.Food), Is.EqualTo(Colony.BerryFood));
        }

        [Test]
        public void SearchingAWreckGivesALittleAndItCanStillBeDismantled()
        {
            var c = Flat();
            var p = new GridPos(7, 7);
            c.Grid.SetFeature(p, CellFeature.Wreck);
            System.Collections.Generic.Dictionary<ResourceType, int> found = null;
            c.Searched += (cell, loot) => found = loot;
            Assert.That(c.Order(JobType.Search, p), Is.True);
            Run(c, 12);
            Assert.That(found, Is.Not.Null.And.Not.Empty);
            Assert.That(c.Grid.GetFeature(p), Is.EqualTo(CellFeature.WreckSearched));
            Assert.That(c.CanOrder(JobType.Search, p), Is.False, "deja fouillee");
            Assert.That(c.Order(JobType.Salvage, p), Is.True, "on peut encore la demonter");
        }

        [Test]
        public void TheFarmHasPebblesAndBerriesNearby()
        {
            var map = MapGenerator.Generate(128, 128, 1337);
            int pebbles = 0, berries = 0;
            for (int dy = -8; dy <= 8; dy++)
            for (int dx = -8; dx <= 8; dx++)
            {
                var f = map.Grid.GetFeature(new GridPos(map.FarmCenter.X + dx, map.FarmCenter.Y + dy));
                if (f == CellFeature.Pebbles) pebbles++;
                if (f == CellFeature.Berries) berries++;
            }
            Assert.That(pebbles, Is.GreaterThan(0));
            Assert.That(berries, Is.GreaterThan(0));
        }
    }
}
