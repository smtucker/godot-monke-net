using Godot;
using MonkeNet.Serializer;
using MonkeNet.Server;
using MonkeNet.Shared;

namespace GameDemo;

public partial class ServerPlayer : CharacterBody3D, IServerSyncedEntity
{
    [Export] private SharedPlayerMovement _playerMovement;

    public int EntityId { get; set; }
    public byte EntityType { get; set; }
    public int Authority { get; set; }
    public string Metadata { get; set; }
    public float Yaw { get; set; }

    public void EntitySpawned() { }

    public void OnProcessTick(int tick, IPackableElement genericInput)
    {
        CharacterInputMessage input = (CharacterInputMessage)genericInput;
        Yaw = input.CameraYaw;
        _playerMovement.AdvancePhysics(input);
    }

    // Capture current entity state, sent by the Server Entity Manager to all clients
    public IEntityStateData GenerateCurrentStateMessage()
    {
        return new EntityStateMessage
        {
            EntityId = this.EntityId,
            Yaw = this.Yaw,
            Position = this.Position,
            Velocity = this.Velocity
        };
    }

}
