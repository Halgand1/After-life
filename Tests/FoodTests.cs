using AfterCity.Core.Sim;
using AfterCity.Core.World;
using NUnit.Framework;

namespace AfterCity.Core.Tests
{
    public class FoodTests
    {
        static Colony Flat()
        {
            var g = new WorldGrid(16, 16);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                g.SetLevel(new GridPos(x, y), 3);
            return new Colony(g) { Home = new GridPos(2, 2) };
        }

        static void RunHours(Colony c, double hours)
        {
            double target = c.Time + hours / 24.0 * Colony.DayLength;
            while (c.Time < target) c.Tick(0.25);
        }

        [Test]
        public void FarmHarvestsInTheEvening()
        {
            var c = Flat();
            c.HasFarm = true;
            int harvested = 0;
            c.Harvested += n => harvested += n;
            RunHours(c, 11); // 8 h -> 19 h
            Assert.That(harvested, Is.EqualTo(Colony.FarmYield));
            Assert.That(c.Stock.Get(ResourceType.Food), Is.EqualTo(Colony.FarmYield));
        }

        [Test]
        public void FieldsAddToTheHarvest()
        {
            var c = Flat();
            c.HasFarm = true;
            c.AddPrebuilt(BuildingType.Field, new GridPos(6, 6));
            Assert.That(c.DailyHarvest, Is.EqualTo(Colony.FarmYield + Colony.FieldYield));
        }

        [Test]
        public void EveryoneEatsAtDawn()
        {
            var c = Flat();
            for (int i = 0; i < 3; i++) c.AddVillager(new GridPos(2 + i, 2));
            c.Stock.Add(ResourceType.Food, 10);
            RunHours(c, 23); // 8 h -> 7 h le lendemain
            // 3 survivants + celui qui arrive a l'aube (il y a de la place)
            Assert.That(c.Villagers.Count, Is.EqualTo(4));
            Assert.That(c.Stock.Get(ResourceType.Food), Is.EqualTo(10 - 4));
        }

        [Test]
        public void HungerHurts()
        {
            var c = Flat();
            var v = c.AddVillager(new GridPos(2, 2));
            int hungry = 0;
            c.Ate += (e, h) => hungry = h;
            RunHours(c, 23);
            Assert.That(hungry, Is.EqualTo(c.Villagers.Count));
            Assert.That(v.Health, Is.EqualTo(Villager.MaxHealth - Colony.StarvationDamage));
        }
    }
}
