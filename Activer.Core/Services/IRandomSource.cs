namespace Activer.Core.Services;

public interface IRandomSource
{
    int Next(int minValue, int maxValueExclusive);
}
