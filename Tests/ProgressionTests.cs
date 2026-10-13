using AfterCity.Core.Sim;
using AfterCity.Core.World;
using NUnit.Framework;

namespace AfterCity.Core.Tests
{
    public class ProgressionTests
    {
        static Colony Flat()
        {
            var g = new WorldGrid(24, 24);
            for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
                g.SetLevel(new GridPos(x, y), 3);
            var c = new Colony(g) { Home = new GridPos(2, 2) };
            c.Stock.Add(ResourceType.Wood, 100);
            c.Stock.Add(ResourceType.Stone, 100);
            return c;
        }

        [Test]
        public void BuildingsUnlockInOrder()
        {
            var c = Flat();
            Assert.That(c.IsUnlocked(BuildingType.Shelter), Is.True);
            Assert.That(c.IsUnlocked(BuildingType.Field), Is.True);
            Assert.That(c.IsUnlocked(BuildingType.ExpeditionPost), Is.False);
            Assert.That(c.CanPlace(BuildingType.ExpeditionPost, new GridPos(10, 10)), Is.False);
            c.AddPrebuilt(BuildingType.Shelter, new GridPos(4, 4));
            Assert.That(c.IsUnlocked(BuildingType.ExpeditionPost), Is.True);
            Assert.That(c.IsUnlocked(BuildingType.Workshop), Is.False);
            c.AddPrebuilt(BuildingType.ExpeditionPost, new GridPos(8, 4));
            Assert.That(c.IsUnlocked(BuildingType.Workshop), Is.True);
            Assert.That(c.IsUnlocked(BuildingType.Watchtower), Is.False);
            c.AddPrebuilt(BuildingType.Workshop, new GridPos(12, 4));
            Assert.That(c.IsUnlocked(BuildingType.Watchtower), Is.True);
        }

        [Test]
        public void AsphaltNeedsTheWorkshopAndTheResearch()
        {
            var c = Flat();
            c.AddVillager(new GridPos(2, 2));
            var p = new GridPos(6, 2);
            Assert.That(c.CanOrder(JobType.Asphalt, p), Is.False);
            Assert.That(c.CanResearch(TechType.Asphalt), Is.False, "pas d'atelier");
            c.AddPrebuilt(BuildingType.Workshop, new GridPos(12, 12));
            Assert.That(c.CanResearch(TechType.Asphalt), Is.False, "pas de ferraille");
            c.Stock.Add(ResourceType.Scrap, TechSpec.Of(TechType.Asphalt).Scrap);
            Assert.That(c.Research(TechType.Asphalt), Is.True);
            Assert.That(c.Stock.Get(ResourceType.Scrap), Is.EqualTo(0));

            int stone = c.Stock.Get(ResourceType.Stone);
            Assert.That(c.Order(JobType.Asphalt, p), Is.True);
            Assert.That(c.Stock.Get(ResourceType.Stone), Is.EqualTo(stone - 1));
            for (int i = 0; i < 300; i++) c.Tick(0.05);
            Assert.That(c.Grid.GetRoad(p), Is.EqualTo(RoadType.Asphalt));
            Assert.That(Roads.SpeedFactor(RoadType.Asphalt), Is.GreaterThan(Roads.SpeedFactor(RoadType.Dirt)));
        }
    }
}
