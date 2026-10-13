using AfterCity.Core.Sim;
using AfterCity.Core.World;
using NUnit.Framework;

namespace AfterCity.Core.Tests
{
    public class HordeTests
    {
        const int Size = 30;
        static readonly GridPos Farm = new GridPos(15, 15);

        static Colony FlatColony()
        {
            var g = new WorldGrid(Size, Size);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                g.SetLevel(new GridPos(x, y), 3);
            return new Colony(g);
        }

        static void Run(Colony c, Horde h, double seconds)
        {
            for (double t = 0; t < seconds; t += 0.05)
            {
                c.Tick(0.05);
                h.Tick(0.05);
            }
        }

        /// <summary>Avance la colonie jusqu'a la tombee de la nuit (sans horde).</summary>
        static void ToNight(Colony c)
        {
            while (!c.IsNight) c.Tick(0.5);
        }

        [Test]
        public void FirstNightBringsAWaveThatDiesAtDawn()
        {
            var c = FlatColony();
            var h = new Horde(c, Farm, 1);
            int waves = 0;
            h.WaveStarted += n => waves = n;
            Run(c, h, Colony.DayLength * (20.6 - Colony.StartHour) / 24.0);
            Assert.That(c.IsNight, Is.True);
            Assert.That(waves, Is.EqualTo(Horde.WaveSize(1)));
            Assert.That(h.Zombies.Count, Is.EqualTo(Horde.WaveSize(1)));

            while (c.IsNight) { c.Tick(0.5); h.Tick(0.5); }
            Assert.That(h.Zombies, Is.Empty);
        }

        [Test]
        public void ZombieWalksToTheFarmAndBitesASurvivor()
        {
            var c = FlatColony();
            var v = c.AddVillager(Farm);
            ToNight(c);
            var h = new Horde(c, Farm, 1);
            h.Spawn(new GridPos(2, 15));
            Run(c, h, 40);
            Assert.That(v.Health, Is.LessThan(Villager.MaxHealth));
        }

        [Test]
        public void LoneSurvivorDiesAgainstFourZombies()
        {
            var c = FlatColony();
            var v = c.AddVillager(Farm);
            Villager dead = null;
            c.VillagerDied += x => dead = x;
            ToNight(c);
            var h = new Horde(c, Farm, 1);
            for (int i = 0; i < 4; i++) h.Spawn(new GridPos(Farm.X - 2 + i, Farm.Y + 2));
            Run(c, h, 30);
            Assert.That(dead, Is.SameAs(v));
            Assert.That(c.Villagers, Is.Empty);
        }

        [Test]
        public void ADeepMoatTrapsZombies()
        {
            var c = FlatColony();
            c.AddVillager(Farm);
            // Douve seche de 2 niveaux tout autour de la ferme
            for (int y = 8; y <= 22; y++)
            for (int x = 8; x <= 22; x++)
                if (x == 8 || x == 22 || y == 8 || y == 22)
                    c.Grid.SetLevel(new GridPos(x, y), 1);
            ToNight(c);
            var h = new Horde(c, Farm, 1);
            var z = h.Spawn(new GridPos(2, 15));
            Run(c, h, 40);
            Assert.That(z.State, Is.EqualTo(ZombieState.Trapped));
            Assert.That(c.Grid.GetLevel(z.Cell), Is.EqualTo(1));
            Assert.That(c.Villagers[0].Health, Is.EqualTo(Villager.MaxHealth));
        }

        [Test]
        public void ZombiesSmashThroughABarricadeRing()
        {
            var c = FlatColony();
            var v = c.AddVillager(Farm);
            for (int y = 10; y <= 20; y++)
            for (int x = 10; x <= 20; x++)
                if (x == 10 || x == 20 || y == 10 || y == 20)
                    c.Grid.SetWall(new GridPos(x, y), true);
            var changed = 0;
            c.CellChanged += _ => changed++;
            ToNight(c);
            var h = new Horde(c, Farm, 1);
            h.Spawn(new GridPos(2, 15));

            Run(c, h, 15);
            Assert.That(v.Health, Is.EqualTo(Villager.MaxHealth), "la barricade tient un moment");
            Run(c, h, 40);
            Assert.That(changed, Is.GreaterThanOrEqualTo(1), "une barricade a cede");
            Assert.That(v.Health, Is.LessThan(Villager.MaxHealth));
        }

        [Test]
        public void SurvivorsFightBack()
        {
            var c = FlatColony();
            for (int i = 0; i < 4; i++) c.AddVillager(new GridPos(Farm.X + (i % 2), Farm.Y + i / 2));
            ToNight(c);
            var h = new Horde(c, Farm, 1);
            h.Spawn(new GridPos(Farm.X - 3, Farm.Y));
            Run(c, h, 30);
            Assert.That(h.Zombies, Is.Empty);
            Assert.That(c.Villagers.Count, Is.EqualTo(4));
        }
    }
}
