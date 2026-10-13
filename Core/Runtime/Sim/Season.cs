namespace AfterCity.Core.Sim
{
    /// <summary>Saisons de l'annee de jeu (GDD 14.4), dans l'ordre.</summary>
    public enum Season { Spring, Summer, Autumn, Winter }

    public static class Seasons
    {
        public static string Name(Season s)
        {
            switch (s)
            {
                case Season.Spring: return "Printemps";
                case Season.Summer: return "Ete";
                case Season.Autumn: return "Automne";
                default: return "Hiver";
            }
        }
    }
}
