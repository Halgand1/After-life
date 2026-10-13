using AfterCity.Core.Sim;
using AfterCity.Core.Util;
using AfterCity.Core.World;
using NUnit.Framework;

namespace AfterCity.Core.Tests
{
    public class WildlifeTests
    {
        static Colony Flat(int size = 40)
        {
            var g = new WorldGrid(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                g.SetLevel(new GridPos(x, y), 3);
            var c = new Colony(g) { Random = new Rng(7) };
            c.Wildlife = new Wildlife(g, new Rng(11));
            return c;
        }

        static void Run(Colony c, double s) { for (double t = 0; t < s; t += 0.05) c.Tick(0.05); }

        static double Dist(Villager v, Animal a) =>
            System.Math.Sqrt((a.X - v.X) * (a.X - v.X) + (a.Y - v.Y) * (a.Y - v.Y));

        [Test]
        public void AnimalsLeaveSurvivorsAloneUntilAttacked()
        {
            var c = Flat();
            var v = c.AddVillager(new GridPos(10, 10));
            var boar = c.Wildlife.Spawn(AnimalKind.Boar, new GridPos(12, 10));
            var deer = c.Wildlife.Spawn(AnimalKind.Deer, new GridPos(10, 12));
            Run(c, 60);
            Assert.That(v.Health, Is.EqualTo(Villager.MaxHealth));
            Assert.That(boar.State, Is.Not.EqualTo(AnimalState.Charging));
            Assert.That(deer.State, Is.Not.EqualTo(AnimalState.Fleeing));
        }

        [Test]
        public void WanderingStaysNearHome()
        {
            var c = Flat();
            var deer = c.Wildlife.Spawn(AnimalKind.Deer, new GridPos(20, 20));
            Run(c, 120);
            Assert.That(System.Math.Abs(deer.Cell.X - 20), Is.LessThanOrEqualTo(Wildlife.WanderRadius + 1));
            Assert.That(System.Math.Abs(deer.Cell.Y - 20), Is.LessThanOrEqualTo(Wildlife.WanderRadius + 1));
        }

        [Test]
        public void AttackedDeerRunsAwayThenCalmsDown()
        {
            var c = Flat();
            var v = c.AddVillager(new GridPos(10, 20));
            var deer = c.Wildlife.Spawn(AnimalKind.Deer, new GridPos(13, 20));
            double before = Dist(v, deer);
            c.Wildlife.Attack(deer, v, 10);
            Assert.That(deer.State, Is.EqualTo(AnimalState.Fleeing));
            Run(c, 3);
            Assert.That(Dist(v, deer), Is.GreaterThan(before + 6));
            Run(c, Wildlife.DeerAlarm);
            Assert.That(deer.State, Is.Not.EqualTo(AnimalState.Fleeing));
        }

        [Test]
        public void AttackedBoarChargesTheAttacker()
        {
            var c = Flat();
            var v = c.AddVillager(new GridPos(10, 20));
            var boar = c.Wildlife.Spawn(AnimalKind.Boar, new GridPos(14, 20));
            int charges = 0;
            c.Wildlife.Charged += (a, who, dmg) => { if (who == v) charges++; };
            c.Wildlife.Attack(boar, v, 10);
            Assert.That(boar.State, Is.EqualTo(AnimalState.Charging));
            Run(c, 4);
            Assert.That(charges, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void BoarChargesHurtAboutAThirdOfTheTime()
        {
            int hurt = 0, total = 0;
            for (int seed = 1; seed <= 400; seed++)
            {
                var c = Flat(12);
                c.Wildlife.Random = new Rng((ulong)seed);
                var v = c.AddVillager(new GridPos(5, 5));
                var boar = c.Wildlife.Spawn(AnimalKind.Boar, new GridPos(6, 5));
                c.Wildlife.Charged += (a, who, dmg) => { total++; if (dmg > 0) hurt++; };
                c.Wildlife.Attack(boar, v, 1);
                for (int i = 0; i < 40 && total < seed; i++) c.Tick(0.05);
            }
            Assert.That(total, Is.EqualTo(400));
            Assert.That(hurt / (double)total, Is.InRange(0.27, 0.43));
        }

        [Test]
        public void HuntingAGroupMakesBoarsLessDangerous()
        {
            int alone = 0, group = 0;
            for (int seed = 1; seed <= 400; seed++)
            {
                foreach (bool together in new[] { false, true })
                {
                    var c = Flat(12);
                    c.Wildlife.Random = new Rng((ulong)seed);
                    var v = c.AddVillager(new GridPos(5, 5));
                    if (together) { c.AddVillager(new GridPos(4, 5)); c.AddVillager(new GridPos(5, 6)); }
                    var boar = c.Wildlife.Spawn(AnimalKind.Boar, new GridPos(6, 5));
                    bool done = false;
                    c.Wildlife.Charged += (a, who, dmg) => { done = true; if (dmg > 0) { if (together) group++; else alone++; } };
                    c.Wildlife.Attack(boar, v, 1);
                    for (int i = 0; i < 40 && !done; i++) c.Tick(0.05);
                }
            }
            Assert.That(group, Is.LessThan(alone * 0.75));
        }

        [Test]
        public void HuntingADeerGivesFood()
        {
            var c = Flat();
            c.AddVillager(new GridPos(5, 20));
            var deer = c.Wildlife.Spawn(AnimalKind.Deer, new GridPos(15, 20));
            int gained = 0;
            c.Hunted += (a, v, food) => gained += food;
            Assert.That(c.OrderHunt(deer), Is.True);
            Assert.That(c.OrderHunt(deer), Is.False, "un seul ordre par animal");
            Run(c, 180);
            Assert.That(deer.IsDead, Is.True);
            Assert.That(gained, Is.EqualTo(Wildlife.DeerMeat));
            Assert.That(c.Stock.Get(ResourceType.Food), Is.EqualTo(Wildlife.DeerMeat));
            Assert.That(c.Hunts, Is.Empty);
        }

        [Test]
        public void HuntingABoarEndsWithTheBoarDeadOrTheHunterHurt()
        {
            var c = Flat();
            var hunter = c.AddVillager(new GridPos(5, 20));
            var boar = c.Wildlife.Spawn(AnimalKind.Boar, new GridPos(14, 20));
            int charges = 0;
            c.Wildlife.Charged += (a, v, dmg) => charges++;
            c.OrderHunt(boar);
            Run(c, 120);
            Assert.That(boar.IsDead || hunter.IsDead, Is.True);
            if (boar.IsDead) Assert.That(c.Stock.Get(ResourceType.Food), Is.EqualTo(Wildlife.BoarMeat));
        }

        [Test]
        public void CancellingAHuntFreesTheHunter()
        {
            var c = Flat();
            var v = c.AddVillager(new GridPos(5, 20));
            var deer = c.Wildlife.Spawn(AnimalKind.Deer, new GridPos(30, 20));
            c.OrderHunt(deer);
            Run(c, 1);
            Assert.That(v.Job, Is.Not.Null);
            Assert.That(c.CancelHunt(deer), Is.True);
            Assert.That(v.Job, Is.Null);
            Assert.That(c.Hunts, Is.Empty);
        }

        [Test]
        public void AnimalsNeverEnterWaterOrWalls()
        {
            var c = Flat();
            for (int y = 0; y < 40; y++) c.Grid.SetBiome(new GridPos(18, y), Biome.Water);
            for (int x = 0; x < 40; x++) c.Grid.SetWall(new GridPos(x, 24), true);
            var v = c.AddVillager(new GridPos(10, 20));
            var deer = c.Wildlife.Spawn(AnimalKind.Deer, new GridPos(16, 20));
            for (int i = 0; i < 6; i++)
            {
                c.Wildlife.Provoke(deer, v);
                for (double t = 0; t < 5; t += 0.05)
                {
                    c.Tick(0.05);
                    Assert.That(c.Grid.GetBiome(deer.Cell), Is.Not.EqualTo(Biome.Water));
                    Assert.That(c.Grid.HasWall(deer.Cell), Is.False);
                }
            }
        }

        [Test]
        public void PopulateKeepsHerdsAwayFromTheFarm()
        {
            var map = MapGenerator.Generate(96, 96, 12345);
            var w = Wildlife.Populate(map, new Rng(3));
            Assert.That(w.Animals.Count, Is.GreaterThan(0));
            bool deer = false, boar = false;
            foreach (var a in w.Animals)
            {
                if (a.Kind == AnimalKind.Deer) deer = true; else boar = true;
                int d = System.Math.Abs(a.Home.X - map.FarmCenter.X) + System.Math.Abs(a.Home.Y - map.FarmCenter.Y);
                Assert.That(d, Is.GreaterThanOrEqualTo(10));
                Assert.That(w.CanStand(a.Cell), Is.True);
            }
            Assert.That(deer && boar, Is.True);
        }
    }
}
