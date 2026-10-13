using AfterCity.Core.Sim;
using AfterCity.Core.Util;
using AfterCity.Core.World;
using NUnit.Framework;

namespace AfterCity.Core.Tests
{
    public class EquipmentTests
    {
        static Colony Flat(int size = 20)
        {
            var g = new WorldGrid(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                g.SetLevel(new GridPos(x, y), 3);
            return new Colony(g) { Home = new GridPos(2, 2), Random = new Rng(5) };
        }

        static void Run(Colony c, double s) { for (double t = 0; t < s; t += 0.05) c.Tick(0.05); }

        [Test]
        public void EveryCatalogueLoadoutExists()
        {
            foreach (SurvivorRole role in System.Enum.GetValues(typeof(SurvivorRole)))
            foreach (var id in EquipmentSpec.Loadout(role))
                Assert.That(EquipmentSpec.Get(id), Is.Not.Null, id);
            int n = 0;
            foreach (var _ in EquipmentSpec.All) n++;
            Assert.That(n, Is.EqualTo(38), "38 objets dans catalogue_equipement.json");
        }

        [Test]
        public void SurvivorsArrivePreEquippedForTheirRole()
        {
            var c = Flat();
            var worker = c.AddVillager(new GridPos(1, 1));
            var quarrier = c.AddVillager(new GridPos(2, 1));
            var militia = c.AddVillager(new GridPos(3, 1));
            Assert.That(worker.Role, Is.EqualTo(SurvivorRole.Worker));
            Assert.That(worker.Equipment[EquipSlot.RightHand].Id, Is.EqualTo("Hache"));
            Assert.That(quarrier.Equipment[EquipSlot.RightHand].Id, Is.EqualTo("Pioche"));
            Assert.That(militia.Equipment[EquipSlot.LeftHand].Id, Is.EqualTo("Bouclier_Panneau"));
            var soldier = c.AddVillager(new GridPos(4, 1), SurvivorRole.Soldier);
            Assert.That(soldier.Equipment[EquipSlot.Head].Id, Is.EqualTo("Casque_Militaire"));
        }

        [Test]
        public void TheRightToolMakesWorkFaster()
        {
            double TimeToChop(SurvivorRole role)
            {
                var c = Flat();
                var v = c.AddVillager(new GridPos(5, 5), role);
                var tree = new GridPos(5, 6);
                c.Grid.SetFeature(tree, CellFeature.Tree);
                c.Order(JobType.Chop, tree);
                double t = 0;
                while (c.Grid.GetFeature(tree) == CellFeature.Tree && t < 30) { c.Tick(0.05); t += 0.05; }
                return t;
            }
            double axe = TimeToChop(SurvivorRole.Worker), bare = TimeToChop(SurvivorRole.Medic);
            Assert.That(axe, Is.LessThan(bare * 0.7));
        }

        [Test]
        public void SwappingPutsTheOldItemInStorage()
        {
            var c = Flat();
            var v = c.AddVillager(new GridPos(1, 1), SurvivorRole.Medic);
            var axe = new EquipmentItem(EquipmentSpec.Get("Pied_De_Biche"));
            c.AddToStorage(axe);
            Assert.That(c.Equip(v, axe), Is.True);
            Assert.That(v.Equipment[EquipSlot.RightHand], Is.SameAs(axe));
            Assert.That(c.Storage, Is.Empty, "la main droite du medecin etait vide");

            var helmet = new EquipmentItem(EquipmentSpec.Get("Casque_Moto"));
            c.AddToStorage(helmet);
            var rifle = new EquipmentItem(EquipmentSpec.Get("Fusil"));
            c.AddToStorage(rifle);
            Assert.That(c.Equip(v, rifle), Is.True);
            Assert.That(c.Storage, Has.Member(axe));
            Assert.That(c.Equip(v, rifle), Is.False, "deja porte");
            Assert.That(c.Unequip(v, EquipSlot.RightHand), Is.True);
            Assert.That(c.Storage, Has.Member(rifle));
            Assert.That(v.Equipment[EquipSlot.RightHand], Is.Null);
        }

        [Test]
        public void ArmorSoftensAttacksButNotHunger()
        {
            var c = Flat();
            var soldier = c.AddVillager(new GridPos(1, 1), SurvivorRole.Soldier);
            var medic = c.AddVillager(new GridPos(2, 1), SurvivorRole.Medic);
            Assert.That(c.Hurt(soldier, 20, attack: true), Is.LessThan(20 * 0.7));
            Assert.That(c.Hurt(medic, 20, attack: true), Is.EqualTo(20));
            Assert.That(c.Hurt(soldier, 10), Is.EqualTo(10), "la faim ignore l'armure");
        }

        [Test]
        public void ToolsWearOutAndBreak()
        {
            var c = Flat(30);
            c.Home = new GridPos(-1, -1); // pas de renfort a l'aube : un seul carrier
            var v = c.AddVillager(new GridPos(1, 1), SurvivorRole.Quarrier);
            var pick = v.Equipment[EquipSlot.RightHand];
            int uses = pick.Durability;
            EquipmentItem broken = null;
            c.ItemBroken += (who, item) => broken = item;
            for (int i = 0; i < uses; i++)
            {
                var rock = new GridPos(2 + i % 25, 3 + i / 25 * 2);
                c.Grid.SetFeature(rock, CellFeature.Rock);
                c.Order(JobType.Mine, rock);
            }
            Run(c, uses * 6);
            Assert.That(c.Jobs, Is.Empty);
            Assert.That(broken, Is.SameAs(pick));
            Assert.That(v.Equipment[EquipSlot.RightHand], Is.Null);
        }

        [Test]
        public void SearchingWrecksSometimesTurnsUpEquipment()
        {
            var c = Flat(40);
            c.AddVillager(new GridPos(1, 1), SurvivorRole.Cleaner);
            int found = 0;
            c.ItemFound += (p, item) => found++;
            for (int i = 0; i < 60; i++)
            {
                var p = new GridPos(2 + i % 30, 3 + i / 30 * 2);
                c.Grid.SetFeature(p, CellFeature.Wreck);
                c.Order(JobType.Search, p);
            }
            Run(c, 400);
            Assert.That(found, Is.InRange(2, 16));
            Assert.That(c.Storage.Count, Is.EqualTo(found));
        }
    }
}
