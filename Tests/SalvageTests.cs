using System.Linq;
using AfterCity.Core.Sim;
using AfterCity.Core.Util;
using AfterCity.Core.World;
using NUnit.Framework;

namespace AfterCity.Core.Tests
{
    public class SalvageTests
    {
        [Test]
        public void CommonPartsComeMoreOftenThanRareOnes()
        {
            var parts = Salvage.Roll(new Rng(3), 10000);
            Assert.That(parts[ResourceType.Metal], Is.GreaterThan(parts[ResourceType.Steel]));
            Assert.That(parts[ResourceType.Steel], Is.GreaterThan(parts[ResourceType.Electronics]));
            Assert.That(parts[ResourceType.Electronics], Is.GreaterThan(0));
        }

        [Test]
        public void DismantlingAWreckGivesScrapAndParts()
        {
            var g = new WorldGrid(12, 12);
            var wreck = new GridPos(6, 6);
            g.SetFeature(wreck, CellFeature.Wreck);
            var c = new Colony(g);
            c.AddVillager(new GridPos(1, 1));
            System.Collections.Generic.Dictionary<ResourceType, int> got = null;
            c.Salvaged += (p, loot) => got = loot;
            c.Order(JobType.Salvage, wreck);
            for (int i = 0; i < 400; i++) c.Tick(0.05);
            Assert.That(got, Is.Not.Null);
            Assert.That(c.Stock.Get(ResourceType.Scrap), Is.InRange(2, 4));
            Assert.That(got.Where(kv => kv.Key != ResourceType.Scrap).Sum(kv => kv.Value), Is.InRange(3, 5));
        }

        [Test]
        public void WorkshopTurnsScrapIntoParts()
        {
            var g = new WorldGrid(12, 12);
            var c = new Colony(g);
            c.Stock.Add(ResourceType.Scrap, 7);
            Assert.That(c.RefineScrap(), Is.Null, "pas d'atelier");
            c.AddPrebuilt(BuildingType.Workshop, new GridPos(5, 5));
            var parts = c.RefineScrap();
            Assert.That(parts.Values.Sum(), Is.EqualTo(Salvage.PartsPerBatch));
            Assert.That(c.Stock.Get(ResourceType.Scrap), Is.EqualTo(2));
            Assert.That(c.RefineScrap(), Is.Null, "plus assez de ferraille");
        }
    }
}
