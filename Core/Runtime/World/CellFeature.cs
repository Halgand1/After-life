namespace AfterCity.Core.World
{
    /// <summary>Ce qui se trouve sur une case en plus du sol : ressources a recolter.</summary>
    public enum CellFeature : byte
    {
        None,
        Tree,   // bois
        Rock,   // pierre
        Wreck   // epave de voiture : ferraille
    }
}
