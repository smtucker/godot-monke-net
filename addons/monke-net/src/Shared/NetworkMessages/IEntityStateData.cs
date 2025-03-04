using MonkeNet.Serializer;

namespace MonkeNet.Shared;

public interface IEntityStateData : IPackableElement
{
    public int EntityId { get; } // Entity ID this message is for
}