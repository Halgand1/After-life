namespace AfterCity.Core.Util
{
    /// <summary>
    /// Générateur pseudo-aléatoire déterministe (SplitMix64).
    /// Une même graine donne toujours la même suite : cartes reproductibles et tests stables.
    /// </summary>
    public sealed class Rng
    {
        ulong _state;

        public Rng(ulong seed)
        {
            _state = seed;
        }

        public ulong NextULong()
        {
            ulong z = (_state += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Réel dans [0, 1).</summary>
        public double NextDouble()
        {
            return (NextULong() >> 11) * (1.0 / (1UL << 53));
        }

        /// <summary>Entier dans [min, max).</summary>
        public int Range(int min, int max)
        {
            if (max <= min) return min;
            return min + (int)(NextULong() % (ulong)(max - min));
        }

        /// <summary>Vrai avec la probabilité donnée (0 à 1).</summary>
        public bool Chance(double probability)
        {
            return NextDouble() < probability;
        }
    }
}
