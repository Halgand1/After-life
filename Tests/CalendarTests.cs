using AfterCity.Core.Sim;
using AfterCity.Core.World;
using NUnit.Framework;

namespace AfterCity.Core.Tests
{
    public class CalendarTests
    {
        [Test]
        public void ADayLastsEightySecondsAndSeasonsSixDays()
        {
            Assert.That(Colony.DayLength, Is.EqualTo(80));
            var c = new Colony(new WorldGrid(8, 8)) { Home = new GridPos(-1, -1) };
            Assert.That(c.Day, Is.EqualTo(1));
            Assert.That(c.Season, Is.EqualTo(Season.Spring));
            Assert.That(c.DayOfSeason, Is.EqualTo(1));
            Assert.That(c.Year, Is.EqualTo(1));

            for (double t = 0; t < Colony.DayLength * 6; t += 0.5) c.Tick(0.5);
            Assert.That(c.Day, Is.EqualTo(7));
            Assert.That(c.Season, Is.EqualTo(Season.Summer));
            Assert.That(c.DayOfSeason, Is.EqualTo(1));

            for (double t = 0; t < Colony.DayLength * 18; t += 0.5) c.Tick(0.5);
            Assert.That(c.Day, Is.EqualTo(25));
            Assert.That(c.Season, Is.EqualTo(Season.Spring));
            Assert.That(c.Year, Is.EqualTo(2));
            Assert.That(Seasons.Name(Season.Winter), Is.EqualTo("Hiver"));
        }
    }
}
