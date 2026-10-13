using AfterCity.Core.Sim;
using AfterCity.Core.World;
using NUnit.Framework;

namespace AfterCity.Core.Tests
{
    public class TerraformerTests
    {
        static (WorldGrid grid, Stockpile stock, Terraformer tf) Flat(int level)
        {
            var grid = new WorldGrid(10, 10);
            for (int y = 0; y < 10; y++)
            for (int x = 0; x < 10; x++)
                grid.SetLevel(new GridPos(x, y), level);
            var stock = new Stockpile();
            return (grid, stock, new Terraformer(grid, stock));
        }

        [Test]
        public void DiggingLowersAndProducesEarth()
        {
            var (grid, stock, tf) = Flat(3);
            var p = new GridPos(5, 5);
            Assert.That(tf.Dig(p), Is.True);
            Assert.That(grid.GetLevel(p), Is.EqualTo(2));
            Assert.That(stock.Get(ResourceType.Earth), Is.EqualTo(1));
        }

        [Test]
        public void CannotDigBelowZero()
        {
            var (grid, _, tf) = Flat(0);
            Assert.That(tf.Dig(new GridPos(1, 1)), Is.False);
            Assert.That(grid.GetLevel(new GridPos(1, 1)), Is.EqualTo(0));
        }

        [Test]
        public void RaisingNeedsEarth()
        {
            var (grid, stock, tf) = Flat(3);
            var p = new GridPos(2, 2);
            Assert.That(tf.Raise(p), Is.False);
            stock.Add(ResourceType.Earth, 2);
            Assert.That(tf.Raise(p), Is.True);
            Assert.That(grid.GetLevel(p), Is.EqualTo(4));
            Assert.That(stock.Get(ResourceType.Earth), Is.EqualTo(1));
        }

        [Test]
        public void CannotRaiseAboveMax()
        {
            var (grid, stock, tf) = Flat(WorldGrid.MaxLevel);
            stock.Add(ResourceType.Earth, 5);
            Assert.That(tf.Raise(new GridPos(3, 3)), Is.False);
        }

        [Test]
        public void FlattenMovesEarthFromHighToLow()
        {
            var (grid, stock, tf) = Flat(3);
            grid.SetLevel(new GridPos(0, 0), 5);
            grid.SetLevel(new GridPos(1, 0), 1);
            tf.Flatten(new GridPos(0, 0), new GridPos(1, 0), 3);
            Assert.That(grid.GetLevel(new GridPos(0, 0)), Is.EqualTo(3));
            Assert.That(grid.GetLevel(new GridPos(1, 0)), Is.EqualTo(3));
            Assert.That(stock.Get(ResourceType.Earth), Is.EqualTo(0));
        }
    }
}
