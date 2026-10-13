using AfterCity.Core.Sim;
using AfterCity.Core.World;
using NUnit.Framework;

namespace AfterCity.Core.Tests
{
    public class BuildingTests
    {
        static Colony Flat(int size = 20)
        {
            var g = new WorldGrid(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                g.SetLevel(new GridPos(x, y), 3);
            return new Colony(g) { Home = new GridPos(2, 2), IgnoreRequirements = true };
        }

        static void Run(Colony c, double seconds, Horde h = null)
        {
            for (double t = 0; t < seconds; t += 0.05)
            {
                c.Tick(0.05);
                h?.Tick(0.05);
            }
        }

        [Test]
        public void ShelterIsPaidBuiltAndBlocksTheWay()
        {
            var c = Flat();
            c.Stock.Add(ResourceType.Wood, 12);
            c.AddVillager(new GridPos(2, 2));
            var b = c.PlaceBuilding(BuildingType.Shelter, new GridPos(8, 8));
            Assert.That(b, Is.Not.Null);
            Assert.That(c.Stock.Get(ResourceType.Wood), Is.EqualTo(2));
            Assert.That(c.CanOrder(JobType.Barricade, new GridPos(9, 9)), Is.False, "emprise reservee");
            Run(c, 25);
            Assert.That(b.Built, Is.True);
            Assert.That(c.Grid.HasStructure(new GridPos(9, 9)), Is.True);
            Assert.That(c.Grid.CanStep(new GridPos(7, 8), new GridPos(8, 8), 1), Is.False);
            Assert.That(c.Capacity, Is.EqualTo(Colony.BaseCapacity + 2));
        }

        [Test]
        public void CannotBuildWithoutResourcesOrOnUnevenGround()
        {
            var c = Flat();
            Assert.That(c.CanPlace(BuildingType.Shelter, new GridPos(5, 5)), Is.False);
            c.Stock.Add(ResourceType.Wood, 10);
            Assert.That(c.CanPlace(BuildingType.Shelter, new GridPos(5, 5)), Is.True);
            c.Grid.SetLevel(new GridPos(6, 6), 4);
            Assert.That(c.CanPlace(BuildingType.Shelter, new GridPos(5, 5)), Is.False);
            c.Grid.SetFeature(new GridPos(10, 10), CellFeature.Tree);
            Assert.That(c.CanPlace(BuildingType.Shelter, new GridPos(9, 9)), Is.False);
        }

        [Test]
        public void CancellingAPlannedBuildingRefunds()
        {
            var c = Flat();
            c.Stock.Add(ResourceType.Wood, 6);
            c.Stock.Add(ResourceType.Stone, 4);
            c.PlaceBuilding(BuildingType.Watchtower, new GridPos(5, 5));
            Assert.That(c.Stock.Get(ResourceType.Stone), Is.EqualTo(0));
            Assert.That(c.Cancel(new GridPos(5, 5)), Is.True);
            Assert.That(c.Stock.Get(ResourceType.Wood), Is.EqualTo(6));
            Assert.That(c.Stock.Get(ResourceType.Stone), Is.EqualTo(4));
            Assert.That(c.Buildings, Is.Empty);
        }

        [Test]
        public void ASurvivorArrivesAtDawnWhenThereIsRoom()
        {
            var c = Flat();
            c.AddVillager(new GridPos(2, 2));
            Villager arrived = null;
            c.SurvivorArrived += v => arrived = v;
            while (!c.IsNight) c.Tick(0.5);
            while (c.IsNight) c.Tick(0.5);
            c.Tick(0.1);
            Assert.That(arrived, Is.Not.Null);
            Assert.That(c.Villagers.Count, Is.EqualTo(2));
        }

        [Test]
        public void NoNewSurvivorWhenTheColonyIsFull()
        {
            var c = Flat();
            for (int i = 0; i < Colony.BaseCapacity; i++) c.AddVillager(new GridPos(2 + i, 2));
            while (!c.IsNight) c.Tick(0.5);
            while (c.IsNight) c.Tick(0.5);
            c.Tick(0.1);
            Assert.That(c.Villagers.Count, Is.EqualTo(Colony.BaseCapacity));
        }

        [Test]
        public void WatchtowerShootsZombies()
        {
            var c = Flat(30);
            c.Stock.Add(ResourceType.Wood, 6);
            c.Stock.Add(ResourceType.Stone, 4);
            c.AddVillager(new GridPos(3, 3));
            var tower = c.PlaceBuilding(BuildingType.Watchtower, new GridPos(15, 15));
            Run(c, 30);
            Assert.That(tower.Built, Is.True);
            while (!c.IsNight) c.Tick(0.5);
            var h = new Horde(c, new GridPos(15, 15), 1);
            var z = h.Spawn(new GridPos(20, 15));
            Run(c, 6, h);
            Assert.That(z.IsDead, Is.True);
            Assert.That(h.Zombies, Is.Empty);
        }
    }
}
