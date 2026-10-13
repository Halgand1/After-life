using System.Linq;
using AfterCity.Core.Sim;
using AfterCity.Core.World;
using NUnit.Framework;

namespace AfterCity.Core.Tests
{
    public class ExpeditionTests
    {
        static Colony WithPost(int survivors)
        {
            var g = new WorldGrid(20, 20);
            for (int y = 0; y < 20; y++)
            for (int x = 0; x < 20; x++)
                g.SetLevel(new GridPos(x, y), 3);
            var c = new Colony(g) { Home = new GridPos(3, 3) };
            for (int i = 0; i < survivors; i++) c.AddVillager(new GridPos(2 + i, 2));
            c.Stock.Add(ResourceType.Wood, 15);
            c.PlaceBuilding(BuildingType.ExpeditionPost, new GridPos(10, 10));
            for (int i = 0; i < 400; i++) c.Tick(0.05);
            return c;
        }

        [Test]
        public void ChanceIsNeverBelowFiftyPercent()
        {
            var c = WithPost(1);
            foreach (var t in ExpeditionTarget.All)
                Assert.That(Expedition.ComputeChance(t, c.Villagers.ToList()), Is.InRange(Expedition.MinSuccess, Expedition.MaxSuccess));
            var hardest = ExpeditionTarget.All[ExpeditionTarget.All.Length - 1];
            Assert.That(Expedition.ComputeChance(hardest, c.Villagers.ToList()), Is.EqualTo(50));
        }

        [Test]
        public void MoreMembersMeansBetterOdds()
        {
            var c = WithPost(5);
            var t = ExpeditionTarget.All[1];
            Assert.That(c.PreviewChance(t, 3), Is.GreaterThan(c.PreviewChance(t, 1)));
        }

        [Test]
        public void NeedsAPostAndKeepsSomeoneHome()
        {
            var g = new WorldGrid(10, 10);
            var c = new Colony(g);
            c.AddVillager(new GridPos(1, 1));
            c.AddVillager(new GridPos(2, 1));
            Assert.That(c.CanSendExpedition(1), Is.False, "pas de poste");
            var c2 = WithPost(2);
            Assert.That(c2.CanSendExpedition(2), Is.False, "il faut garder quelqu'un");
            Assert.That(c2.CanSendExpedition(1), Is.True);
        }

        [Test]
        public void MembersLeaveThenComeBackWithLootAndAlive()
        {
            var c = WithPost(4);
            var t = ExpeditionTarget.All[0];
            var e = c.SendExpedition(t, 2);
            Assert.That(e, Is.Not.Null);
            Assert.That(e.Members.All(m => m.Away), Is.True);
            Assert.That(c.PresentCount, Is.EqualTo(2));
            Expedition back = null;
            c.ExpeditionReturned += x => back = x;
            for (double s = 0; s < t.Duration + 1; s += 0.5) c.Tick(0.5);
            Assert.That(back, Is.SameAs(e));
            Assert.That(e.Members.All(m => !m.Away && m.Health > 0), Is.True);
            Assert.That(e.Gains.Values.Sum(), Is.GreaterThan(0));
            Assert.That(c.Expeditions, Is.Empty);
        }

        [Test]
        public void SuccessfulMissionsPayMoreAndHurtLessOnAverage()
        {
            double goodLoot = 0, badLoot = 0, goodHurt = 0, badHurt = 0;
            int good = 0, bad = 0;
            var c = WithPost(4);
            for (int i = 0; i < 400; i++)
            {
                var e = c.SendExpedition(ExpeditionTarget.All[2], 2);
                for (double s = 0; s < e.Target.Duration + 1; s += 1) c.Tick(1);
                if (e.Succeeded) { good++; goodLoot += e.Gains.Values.Sum(); goodHurt += e.DamageTaken; }
                else { bad++; badLoot += e.Gains.Values.Sum(); badHurt += e.DamageTaken; }
                foreach (var v in c.Villagers) c.Heal(v);
            }
            Assert.That(good, Is.GreaterThan(0));
            Assert.That(bad, Is.GreaterThan(0));
            Assert.That(goodLoot / good, Is.GreaterThan(badLoot / bad));
            Assert.That(goodHurt / good, Is.LessThan(badHurt / bad));
        }
    }
}
