using Activer.Core.Models;

namespace Activer.Core.Services;

public interface IActivityPerformer
{
    ActivityExecutionResult Perform(ActivityExecutionRequest request);
}
