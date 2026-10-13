using AfterCity.Core.Sim;
using AfterCity.Core.World;
using NUnit.Framework;

namespace AfterCity.Core.Tests
{
    public class TutorialTests
    {
        static Colony Flat(int size = 24)
        {
            var g = new WorldGrid(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                g.SetLevel(new GridPos(x, y), 3);
            var c = new Colony(g) { Home = new GridPos(-1, -1), IgnoreRequirements = true };
            c.AddVillager(new GridPos(2, 2));
            c.AddVillager(new GridPos(3, 2));
            return c;
        }

        static void Run(Colony c, Tutorial t, double s)
        {
            for (double x = 0; x < s; x += 0.05) { c.Tick(0.05); t.Update(); }
        }

        [Test]
        public void StepsFollowWhatThePlayerReallyDoes()
        {
            var c = Flat();
            var t = new Tutorial(c);
            Assert.That(t.Current.Title, Is.EqualTo("Coupe du bois"));
            Run(c, t, 5);
            Assert.That(t.Index, Is.EqualTo(0), "rien ne se valide tout seul");

            for (int i = 0; i < 2; i++) { var p = new GridPos(6 + i, 6); c.Grid.SetFeature(p, CellFeature.Tree); c.Order(JobType.Chop, p); }
            Run(c, t, 20);
            Assert.That(t.Current.Title, Is.EqualTo("Ramasse des cailloux"));

            for (int i = 0; i < 3; i++) { var p = new GridPos(6 + i, 9); c.Grid.SetFeature(p, CellFeature.Pebbles); c.Order(JobType.Gather, p); }
            Run(c, t, 20);
            Assert.That(t.Current.Title, Is.EqualTo("Pose une barricade"));

            c.Order(JobType.Barricade, new GridPos(4, 4));
            Run(c, t, 15);
            Assert.That(t.Current.Title, Is.EqualTo("Construis un abri"));

            c.Stock.Add(ResourceType.Wood, 20);
            c.PlaceBuilding(BuildingType.Shelter, new GridPos(12, 12));
            Run(c, t, 30);
            Assert.That(t.Current.Title, Is.EqualTo("Tiens jusqu'a la nuit"));

            bool completed = false;
            t.Completed += () => completed = true;
            c.SkipToHour(20.6);
            Run(c, t, 0.1);
            Assert.That(t.Finished && completed, Is.True);
            Assert.That(t.Skipped, Is.False);
        }

        [Test]
        public void TheTutorialCanBeSkipped()
        {
            var c = Flat();
            var t = new Tutorial(c);
            bool completed = false;
            t.Completed += () => completed = true;
            t.Skip();
            Assert.That(t.Finished && t.Skipped && completed, Is.True);
            Assert.That(t.Current, Is.Null);
        }
    }
}
