using Activer.Core.Services;

namespace Activer.Services;

public sealed class RandomSource : IRandomSource
{
    private readonly Random random = new();

    public int Next(int minValue, int maxValueExclusive)
    {
        return random.Next(minValue, maxValueExclusive);
    }
}
