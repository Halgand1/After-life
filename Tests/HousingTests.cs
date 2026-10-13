using AfterCity.Core.Sim;
using AfterCity.Core.World;
using NUnit.Framework;

namespace AfterCity.Core.Tests
{
    public class HousingTests
    {
        static Colony Flat(int size = 24)
        {
            var g = new WorldGrid(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                g.SetLevel(new GridPos(x, y), 3);
            return new Colony(g) { Home = new GridPos(-1, -1), IgnoreRequirements = true };
        }

        static void Days(Colony c, double days) { for (double t = 0; t < days * Colony.DayLength; t += 0.1) c.Tick(0.1); }

        [Test]
        public void EveryHomeTypeHasPlacesAndWants()
        {
            foreach (var t in new[] { BuildingType.Tent, BuildingType.Shelter, BuildingType.Hostel, BuildingType.WoodHouse, BuildingType.BrickHouse })
            {
                var h = HomeSpec.Of(t);
                Assert.That(h, Is.Not.Null, t.ToString());
                Assert.That(h.Places, Is.GreaterThan(0));
                Assert.That(h.Wants, Does.Contain(ResourceType.Food));
            }
            Assert.That(HomeSpec.Of(BuildingType.Workshop), Is.Null);
        }

        [Test]
        public void BiggerHomesWantMoreKindsOfResources()
        {
            int tent = HomeSpec.Of(BuildingType.Tent).Wants.Count;
            int shelter = HomeSpec.Of(BuildingType.Shelter).Wants.Count;
            int wood = HomeSpec.Of(BuildingType.WoodHouse).Wants.Count;
            Assert.That(tent, Is.LessThan(shelter));
            Assert.That(shelter, Is.LessThan(wood));
            Assert.That(HomeSpec.Of(BuildingType.BrickHouse).FoodPerResident, Is.GreaterThan(HomeSpec.Of(BuildingType.Shelter).FoodPerResident));
        }

        [Test]
        public void SurvivorsMoveInAndEatFromTheirShelf()
        {
            var c = Flat();
            var home = c.AddPrebuilt(BuildingType.Shelter, new GridPos(10, 10));
            var a = c.AddVillager(new GridPos(2, 2));
            var b = c.AddVillager(new GridPos(3, 2));
            c.Stock.Add(ResourceType.Food, 20);
            c.Stock.Add(ResourceType.Wood, 20);
            c.SkipToHour(5.9);
            Days(c, 0.05);
            Assert.That(a.House, Is.SameAs(home));
            Assert.That(home.Residents.Count, Is.EqualTo(2));
            Assert.That(home.Shelf(ResourceType.Food), Is.GreaterThan(0), "les porteurs ont rempli l'etagere");
            Assert.That(home.Shelf(ResourceType.Food) + c.Stock.Get(ResourceType.Food), Is.EqualTo(18), "deux repas pris");
        }

        [Test]
        public void AWellStockedHomeBecomesComfortableAndCanBeUpgraded()
        {
            var c = Flat();
            var home = c.AddPrebuilt(BuildingType.Shelter, new GridPos(10, 10));
            c.AddVillager(new GridPos(2, 2));
            c.AddVillager(new GridPos(3, 2));
            c.Stock.Add(ResourceType.Food, 100);
            c.Stock.Add(ResourceType.Wood, 100);
            c.Stock.Add(ResourceType.Stone, 20);
            bool changed = false;
            c.HomeComfortChanged += b => changed = true;
            Days(c, Building.DaysToComfort + 1);
            Assert.That(home.Comfortable, Is.True);
            Assert.That(changed, Is.True);
            Assert.That(c.CanUpgrade(home), Is.True);
            Assert.That(c.Upgrade(home), Is.True);
            Assert.That(home.Type, Is.EqualTo(BuildingType.WoodHouse));
            Assert.That(home.Residents.Count, Is.EqualTo(2), "les habitants restent");
            Assert.That(home.Comfortable, Is.False, "la nouvelle maison reclame plus (eau, medicaments)");
            Assert.That(home.Size, Is.EqualTo(3), "la maison en bois s'agrandit sur les cases libres");
            Assert.That(c.Grid.GetStructure(new GridPos(10, 10)), Is.EqualTo(Building.StructureCode(BuildingType.WoodHouse, true)));
            Assert.That(c.BuildingAt(new GridPos(12, 12)), Is.SameAs(home));
            Assert.That(c.WhyNoUpgrade(home), Does.StartWith("Pas encore confortable"));
        }

        [Test]
        public void AnUpgradeNeedsFreeRoomAroundTheHome()
        {
            var c = Flat();
            var tent = c.AddPrebuilt(BuildingType.Tent, new GridPos(10, 10));
            c.AddVillager(new GridPos(2, 2));
            c.Stock.Add(ResourceType.Food, 100);
            c.Stock.Add(ResourceType.Wood, 100);
            // Rochers sur les quatre diagonales : aucune emprise 2x2 contenant la tente n'est libre
            foreach (var p in new[] { new GridPos(9, 9), new GridPos(11, 9), new GridPos(9, 11), new GridPos(11, 11) })
                c.Grid.SetFeature(p, CellFeature.Rock);
            Days(c, Building.DaysToComfort + 1);
            Assert.That(tent.Comfortable, Is.True);
            Assert.That(c.WhyNoUpgrade(tent), Does.Contain("place"));
            Assert.That(c.Upgrade(tent), Is.False);

            c.Grid.SetFeature(new GridPos(9, 9), CellFeature.None);
            Assert.That(c.Upgrade(tent), Is.True, "la tente devient un abri en reculant vers la case degagee");
            Assert.That(tent.Type, Is.EqualTo(BuildingType.Shelter));
            Assert.That(tent.Origin, Is.EqualTo(new GridPos(9, 9)));
            Assert.That(tent.Covers(new GridPos(10, 10)), Is.True);
            Assert.That(c.Grid.GetStructure(new GridPos(9, 9)), Is.EqualTo(Building.StructureCode(BuildingType.Shelter, true)));
            Assert.That(c.Grid.GetStructure(new GridPos(10, 10)), Is.EqualTo(Building.StructureCode(BuildingType.Shelter, false)));
        }

        [Test]
        public void AHomeMissingWhatItWantsIsNotComfortable()
        {
            var c = Flat();
            var home = c.AddPrebuilt(BuildingType.Shelter, new GridPos(10, 10));
            c.AddVillager(new GridPos(2, 2));
            c.Stock.Add(ResourceType.Food, 100); // pas de bois
            Days(c, 5);
            Assert.That(home.Comfortable, Is.False);
            Assert.That(home.Missing, Does.Contain(ResourceType.Wood));
            Assert.That(c.CanUpgrade(home), Is.False);
        }

        [Test]
        public void HomesNeverHoardTheFoodOfHomelessSurvivors()
        {
            var c = Flat();
            var home = c.AddPrebuilt(BuildingType.Tent, new GridPos(10, 10));
            for (int i = 0; i < 6; i++) c.AddVillager(new GridPos(2 + i, 2));
            c.Stock.Add(ResourceType.Food, 12);
            c.SkipToHour(5.9);
            Days(c, 0.05);
            Assert.That(home.Residents.Count, Is.EqualTo(2));
            Assert.That(home.Shelf(ResourceType.Food), Is.LessThanOrEqualTo(4));
            foreach (var v in c.Villagers) Assert.That(v.Health, Is.EqualTo(Villager.MaxHealth), v.Name + " a mange");
        }

        [Test]
        public void AWellGivesWaterEveryEveningForTheBiggerHomes()
        {
            var c = Flat();
            c.AddPrebuilt(BuildingType.Well, new GridPos(5, 5));
            int drawn = 0;
            c.WaterDrawn += n => drawn += n;
            c.SkipToHour(17.9);
            Days(c, 0.05);
            Assert.That(drawn, Is.EqualTo(Colony.WellYield));
            Assert.That(c.Stock.Get(ResourceType.Water), Is.EqualTo(Colony.WellYield));
        }

        [Test]
        public void ShelfCapacityFollowsTheHomeAndItsResidents()
        {
            var c = Flat();
            var hostel = c.AddPrebuilt(BuildingType.Hostel, new GridPos(10, 10));
            Assert.That(hostel.ShelfCapacity(ResourceType.Medicine), Is.EqualTo(0), "le foyer ne reclame pas de medicaments");
            Assert.That(hostel.ShelfCapacity(ResourceType.Wood), Is.EqualTo(20));
            Assert.That(hostel.ShelfCapacity(ResourceType.Food), Is.EqualTo(0), "personne n'y vit encore");
            for (int i = 0; i < 3; i++) c.AddVillager(new GridPos(2 + i, 2));
            c.AssignHomes();
            Assert.That(hostel.ShelfCapacity(ResourceType.Food), Is.EqualTo(6), "deux jours de vivres pour trois habitants");
            c.Stock.Add(ResourceType.Food, 50);
            c.Stock.Add(ResourceType.Wood, 50);
            c.RefillShelves();
            Assert.That(hostel.Shelf(ResourceType.Food), Is.EqualTo(hostel.ShelfCapacity(ResourceType.Food)));
            Assert.That(hostel.Shelf(ResourceType.Wood), Is.EqualTo(20));
        }

        [Test]
        public void ComfortableHomesMakeTheirResidentsWorkFaster()
        {
            double TimeToChop(bool comfy)
            {
                var c = Flat();
                var home = c.AddPrebuilt(BuildingType.Tent, new GridPos(15, 15));
                var v = c.AddVillager(new GridPos(5, 5), SurvivorRole.Medic);
                c.Stock.Add(ResourceType.Food, 100);
                if (comfy) Days(c, Building.DaysToComfort + 1);
                Assert.That(home.Comfortable, Is.EqualTo(comfy));
                var tree = new GridPos(5, 6);
                c.Grid.SetFeature(tree, CellFeature.Tree);
                c.Order(JobType.Chop, tree);
                double t = 0;
                while (c.Grid.GetFeature(tree) == CellFeature.Tree && t < 30) { c.Tick(0.05); t += 0.05; }
                return t;
            }
            Assert.That(TimeToChop(true), Is.LessThan(TimeToChop(false)));
        }
    }
}
