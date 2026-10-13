namespace AfterCity.Core.World
{
    /// <summary>Revetement d'une case. L'asphalte viendra avec l'arbre technologique.</summary>
    public enum RoadType : byte
    {
        None,
        Dirt,     // chemin de terre : +50 % de vitesse
        Asphalt   // route goudronnee : +100 % (technologie a debloquer)
    }

    public static class Roads
    {
        /// <summary>Multiplicateur de vitesse de marche sur ce revetement.</summary>
        public static double SpeedFactor(RoadType road)
        {
            switch (road)
            {
                case RoadType.Dirt: return 1.5;
                case RoadType.Asphalt: return 2.0;
                default: return 1.0;
            }
        }
    }
}
