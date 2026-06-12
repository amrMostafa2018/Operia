namespace Operia.Domain.Common;

public abstract class BaseEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
