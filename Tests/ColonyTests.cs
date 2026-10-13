using AfterCity.Core.Agents;
using AfterCity.Core.Sim;
using AfterCity.Core.World;
using NUnit.Framework;

namespace AfterCity.Core.Tests
{
    public class ColonyTests
    {
        static WorldGrid Flat(int size = 16, int level = 3)
        {
            var g = new WorldGrid(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                g.SetLevel(new GridPos(x, y), level);
            return g;
        }

        static void Run(Colony c, double seconds)
        {
            for (double t = 0; t < seconds; t += 0.05) c.Tick(0.05);
        }

        [Test]
        public void ChoppingATreeGivesWoodAndRemovesIt()
        {
            var g = Flat();
            var tree = new GridPos(8, 3);
            g.SetFeature(tree, CellFeature.Tree);
            var c = new Colony(g);
            c.AddVillager(new GridPos(2, 3));
            GridPos? changed = null;
            c.CellChanged += p => changed = p;

            Assert.That(c.Order(JobType.Chop, tree), Is.True);
            Run(c, 15);

            Assert.That(g.GetFeature(tree), Is.EqualTo(CellFeature.None));
            Assert.That(c.Stock.Get(ResourceType.Wood), Is.EqualTo(3));
            Assert.That(changed, Is.EqualTo(tree));
            Assert.That(c.Jobs, Is.Empty);
        }

        [Test]
        public void CannotChopWhereThereIsNoTree()
        {
            var c = new Colony(Flat());
            Assert.That(c.Order(JobType.Chop, new GridPos(4, 4)), Is.False);
        }

        [Test]
        public void DiggingTwiceMakesATrapAndTheDiggerStaysOut()
        {
            var g = Flat();
            var c = new Colony(g);
            var v = c.AddVillager(new GridPos(2, 2));
            var pit = new GridPos(6, 6);

            Assert.That(c.Order(JobType.Dig, pit), Is.True);
            Run(c, 10);
            Assert.That(c.Order(JobType.Dig, pit), Is.True);
            Run(c, 10);

            Assert.That(g.GetLevel(pit), Is.EqualTo(1));
            Assert.That(c.Stock.Get(ResourceType.Earth), Is.EqualTo(2));
            Assert.That(v.Cell, Is.Not.EqualTo(pit));
            // Un rodeur (montee 1) qui tombe dedans ne peut plus en sortir
            Assert.That(g.CanStep(pit, new GridPos(6, 7), 1), Is.False);
        }

        [Test]
        public void RaisingNeedsEarth()
        {
            var g = Flat();
            var c = new Colony(g);
            c.AddVillager(new GridPos(2, 2));
            var p = new GridPos(5, 5);
            Assert.That(c.Order(JobType.Raise, p), Is.False);
            c.Stock.Add(ResourceType.Earth, 1);
            Assert.That(c.Order(JobType.Raise, p), Is.True);
            Assert.That(c.Order(JobType.Raise, new GridPos(6, 5)), Is.False, "la terre est deja reservee");
            Run(c, 10);
            Assert.That(g.GetLevel(p), Is.EqualTo(4));
            Assert.That(c.Stock.Get(ResourceType.Earth), Is.EqualTo(0));
        }

        [Test]
        public void BarricadeCostsWoodAndRefundsOnCancel()
        {
            var g = Flat();
            var c = new Colony(g);
            c.Stock.Add(ResourceType.Wood, 3);
            var p = new GridPos(5, 5);
            Assert.That(c.Order(JobType.Barricade, p), Is.True);
            Assert.That(c.Stock.Get(ResourceType.Wood), Is.EqualTo(1));
            Assert.That(c.Order(JobType.Barricade, new GridPos(6, 5)), Is.False);
            Assert.That(c.Cancel(p), Is.True);
            Assert.That(c.Stock.Get(ResourceType.Wood), Is.EqualTo(3));
        }

        [Test]
        public void BarricadeIsBuiltAndBlocksSurvivors()
        {
            var g = Flat();
            var c = new Colony(g);
            c.Stock.Add(ResourceType.Wood, 2);
            c.AddVillager(new GridPos(1, 1));
            var p = new GridPos(5, 5);
            c.Order(JobType.Barricade, p);
            Run(c, 15);
            Assert.That(g.HasWall(p), Is.True);
            Assert.That(new Pathfinder(g).CanWalk(new GridPos(4, 5), p), Is.False);
        }

        [Test]
        public void PathGoesAroundWallsAndFailsWhenEnclosed()
        {
            var g = Flat(10);
            for (int y = 0; y < 9; y++) g.SetWall(new GridPos(5, y), true);
            var pf = new Pathfinder(g);
            var path = pf.FindPath(new GridPos(2, 2), new[] { new GridPos(8, 2) });
            Assert.That(path, Is.Not.Null);
            Assert.That(path, Has.Member(new GridPos(5, 9)));
            Assert.That(path[path.Count - 1], Is.EqualTo(new GridPos(8, 2)));

            g.SetWall(new GridPos(5, 9), true);
            Assert.That(pf.FindPath(new GridPos(2, 2), new[] { new GridPos(8, 2) }), Is.Null);
        }

        [Test]
        public void SurvivorsCannotClimbTwoLevels()
        {
            var g = Flat(6, 2);
            for (int y = 0; y < 6; y++) g.SetLevel(new GridPos(3, y), 4);
            var pf = new Pathfinder(g);
            Assert.That(pf.FindPath(new GridPos(1, 1), new[] { new GridPos(5, 1) }), Is.Null);
        }

        [Test]
        public void StartingColonyHasFiveSurvivorsOnTheFarmAndTreesAround()
        {
            var map = MapGenerator.Generate(128, 128, 1337);
            var c = Colony.StartAtFarm(map);
            Assert.That(c.Villagers.Count, Is.EqualTo(5));
            foreach (var v in c.Villagers)
                Assert.That(map.Grid.GetBiome(v.Cell), Is.EqualTo(Biome.Plains));
            int trees = 0, rocks = 0;
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                var f = map.Grid.GetFeature(new GridPos(x, y));
                if (f == CellFeature.Tree) trees++;
                if (f == CellFeature.Rock) rocks++;
            }
            Assert.That(trees, Is.GreaterThan(1000));
            Assert.That(rocks, Is.GreaterThan(0));
            Assert.That(map.Grid.GetFeature(map.FarmCenter), Is.EqualTo(CellFeature.None));
        }

        [Test]
        public void SeveralSurvivorsShareTheWork()
        {
            var g = Flat(20);
            for (int x = 10; x < 16; x++) g.SetFeature(new GridPos(x, 10), CellFeature.Tree);
            var c = new Colony(g);
            for (int i = 0; i < 3; i++) c.AddVillager(new GridPos(2 + i, 2));
            for (int x = 10; x < 16; x++) c.Order(JobType.Chop, new GridPos(x, 10));
            Run(c, 30);
            Assert.That(c.Stock.Get(ResourceType.Wood), Is.EqualTo(18));
        }
    }
}
