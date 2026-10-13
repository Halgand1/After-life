namespace AfterCity.Core.World
{
    /// <summary>Ce qui se trouve sur une case en plus du sol : ressources a recolter.</summary>
    public enum CellFeature : byte
    {
        None,
        Tree,   // bois
        Rock,   // pierre
        Wreck,          // epave de voiture : ferraille
        Pebbles,        // cailloux au sol : a ramasser (4 cailloux = 1 pierre)
        Berries,        // buisson de baies : vivres
        WreckSearched   // epave deja fouillee (on peut encore la demonter)
    }
}
