namespace AfterCity.Core.Sim
{
    /// <summary>Technologies a rechercher (la liste grandira : l'arbre complet est dans le GDD).</summary>
    public enum TechType
    {
        Asphalt   // goudron : routes asphaltees (vitesse x2)
    }

    public readonly struct TechSpec
    {
        public readonly string Name;
        public readonly string Description;
        public readonly BuildingType RequiresBuilding;
        public readonly int Scrap;
        public readonly int Stone;

        public TechSpec(string name, string description, BuildingType requires, int scrap, int stone)
        {
            Name = name;
            Description = description;
            RequiresBuilding = requires;
            Scrap = scrap;
            Stone = stone;
        }

        public static TechSpec Of(TechType t)
        {
            switch (t)
            {
                case TechType.Asphalt:
                    return new TechSpec("Asphalte", "Routes goudronnees : vitesse x2 (1 pierre par case)", BuildingType.Workshop, 8, 6);
                default:
                    return new TechSpec(t.ToString(), "", BuildingType.Workshop, 0, 0);
            }
        }
    }
}
