using System.Collections.Generic;

namespace AfterCity.Core.Sim
{
    /// <summary>
    /// Logements facon Kingdoms and Castles (GDD 8.1) : places, etagere de ressources, consommation
    /// et ressources reclamees qui grandissent avec la maison. Chiffres provisoires.
    /// </summary>
    public sealed class HomeSpec
    {
        public int Places { get; }
        /// <summary>Vivres manges par habitant et par jour (au-dela de 1, la part en plus vient de l'etagere).</summary>
        public double FoodPerResident { get; }
        /// <summary>Ressources que l'etagere doit contenir pour que la maison soit bien tenue.</summary>
        public IReadOnlyList<ResourceType> Wants { get; }
        /// <summary>Place sur l'etagere pour chaque ressource reclamee.</summary>
        public int ShelfPerResource { get; }
        public double WoodPerDay { get; }
        public double WaterPerResident { get; }
        public double MedicinePerDay { get; }
        /// <summary>Logement suivant quand celui-ci est confortable (meme emprise).</summary>
        public BuildingType? UpgradeTo { get; }

        HomeSpec(int places, double food, int shelf, ResourceType[] wants, double wood = 0, double water = 0, double medicine = 0, BuildingType? upgrade = null)
        {
            Places = places;
            FoodPerResident = food;
            ShelfPerResource = shelf;
            Wants = wants;
            WoodPerDay = wood;
            WaterPerResident = water;
            MedicinePerDay = medicine;
            UpgradeTo = upgrade;
        }

        static readonly HomeSpec TentSpec = new HomeSpec(2, 1, 4, new[] { ResourceType.Food });
        static readonly HomeSpec ShelterSpec = new HomeSpec(4, 1, 8, new[] { ResourceType.Food, ResourceType.Wood },
            wood: 1, upgrade: BuildingType.WoodHouse);
        static readonly HomeSpec HostelSpec = new HomeSpec(10, 1, 20, new[] { ResourceType.Food, ResourceType.Wood, ResourceType.Water },
            wood: 2, water: 1);
        static readonly HomeSpec WoodHouseSpec = new HomeSpec(5, 1.25, 10, new[] { ResourceType.Food, ResourceType.Wood, ResourceType.Water, ResourceType.Medicine },
            wood: 1, water: 1, medicine: 1.0 / 3, upgrade: BuildingType.BrickHouse);
        static readonly HomeSpec BrickHouseSpec = new HomeSpec(6, 1.5, 12, new[] { ResourceType.Food, ResourceType.Wood, ResourceType.Water, ResourceType.Medicine },
            wood: 1.5, water: 1, medicine: 0.5);

        /// <summary>Caracteristiques de logement, ou null si ce batiment n'est pas un logement.</summary>
        public static HomeSpec Of(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Tent: return TentSpec;
                case BuildingType.Shelter: return ShelterSpec;
                case BuildingType.Hostel: return HostelSpec;
                case BuildingType.WoodHouse: return WoodHouseSpec;
                case BuildingType.BrickHouse: return BrickHouseSpec;
                default: return null;
            }
        }

        public static bool IsHome(BuildingType type) => Of(type) != null;
    }
}
