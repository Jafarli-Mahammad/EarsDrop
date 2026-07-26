namespace Domain.Common;

public class Entity
{
    public Guid Id { get; protected init; } = Guid.NewGuid();
}