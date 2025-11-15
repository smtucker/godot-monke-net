using Godot;
using MonkeNet.Serializer;
using MonkeNet.Shared;

namespace MonkeNet.Client;

public interface IPredictableEntity : IClientEntity
{
    public Vector3 Position { get; set; }
    public bool HasMisspredicted(IEntityStateData receivedState, Vector3 savedState);
    public void HandleReconciliation(IEntityStateData receivedState);
    public void OnReceivedState(IEntityStateData receivedState);
    public void ResimulateTick(IPackableElement input);
}
