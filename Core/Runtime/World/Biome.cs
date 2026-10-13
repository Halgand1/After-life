namespace AfterCity.Core.World
{
    /// <summary>Biomes de la carte (GDD § 4.1).</summary>
    public enum Biome : byte
    {
        Plains,       // plaines fertiles
        Meadow,       // prairie
        DenseForest,  // forêt dense : pisteur requis
        Rock,
        Water,        // infranchissable
        Marsh
    }
}
