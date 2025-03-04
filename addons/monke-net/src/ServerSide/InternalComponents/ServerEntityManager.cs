using Godot;
using MonkeNet.NetworkMessages;
using MonkeNet.Serializer;
using MonkeNet.Shared;
using System.Collections.Generic;

namespace MonkeNet.Server;

/// <summary>
/// Handles creation/deletion of entities
/// </summary>
[GlobalClass]
public partial class ServerEntityManager : InternalServerComponent
{
    private EntitySpawner _entitySpawner;
    private int _entityIdCount = 0;
    public override void _EnterTree()
    {
        _entitySpawner = MonkeNetConfig.Instance.EntitySpawner;
    }

    public void SendSnapshotData(int currentTick)
    {
        var snapshotCommand = PackSnapshot(currentTick);
        SendCommandToClient(0, snapshotCommand, INetworkManager.PacketModeEnum.Unreliable, (int)ChannelEnum.Snapshot);
    }

    protected override void OnCommandReceived(int clientId, IPackableMessage command)
    {
        if (command is EntityRequestMessage entityRequest)
        {
            SpawnEntity(++_entityIdCount, entityRequest.EntityType, (int)NetworkManagerEnet.AudienceMode.Broadcast, clientId);
        }
    }

    protected override void OnClientConnected(int clientId)
    {
        SyncWorldState(clientId);
    }

    protected override void OnClientDisconnected(int clientId)
    {
        //TODO: this will send 1 packet for each entity, do in bulk, same as sync should be done
        List<int> entitiesGeneratedByAuthority = _entitySpawner.GetAllEntitiesByAuthority(clientId);
        foreach (int entityId in entitiesGeneratedByAuthority)
        {
            DestroyEntity(entityId, (int)NetworkManagerEnet.AudienceMode.Broadcast);
        }
    }

    /// <summary>
    /// Packs the current game state for a tick (Snapshot)
    /// </summary>
    /// <param name="currentTick"></param>
    private GameSnapshotMessage PackSnapshot(int currentTick)
    {
        // Solve which entities we should include in this snapshot
        List<IServerEntity> includedEntities = [];
        foreach (INetworkedEntity entity in _entitySpawner.Entities)
        {
            if (entity is IServerEntity serverEntity)
            {
                includedEntities.Add(serverEntity);
            }
        }

        // Pack entity data into snapshot
        var entityCount = includedEntities.Count;

        var snapshot = new GameSnapshotMessage
        {
            Tick = currentTick,
            States = new IEntityStateData[entityCount]
        };

        for (int i = 0; i < entityCount; i++)
        {
            snapshot.States[i] = includedEntities[i].GenerateCurrentStateMessage();
        }

        return snapshot;
    }

    /// <summary>
    /// Notifies all clients that an Entity has spawned
    /// </summary>
    /// <param name="entityId"></param>
    /// <param name="entityType"></param>
    /// <param name="targetId"></param>
    /// <param name="authority"></param>
    private void SpawnEntity(int entityId, byte entityType, int targetId, int authority)
    {
        var entityEvent = new EntityEventMessage
        {
            Event = EntityEventEnum.Created,
            EntityId = entityId,
            EntityType = entityType,
            Authority = authority
        };

        // Execute event locally and retrieve position and rotation data
        Node3D instancedEntity = _entitySpawner.SpawnEntity(entityEvent);
        entityEvent.Position = instancedEntity.Position;
        //entityEvent.Rotation = instancedEntity.Rotation;

        SendCommandToClient(targetId, entityEvent, INetworkManager.PacketModeEnum.Reliable, (int)ChannelEnum.EntityEventMessage);
    }

    /// <summary>
    /// Notifies all clients that an Entity has been destroyed
    /// </summary>
    /// <param name="entityId"></param>
    /// <param name="targetId"></param>
    private void DestroyEntity(int entityId, int targetId)
    {
        var entityEvent = new EntityEventMessage
        {
            Event = EntityEventEnum.Destroyed,
            EntityId = entityId,
            EntityType = 0,
            Authority = 0
        };

        _entitySpawner.DestroyEntity(entityEvent);  // Execute event locally

        SendCommandToClient(targetId, entityEvent, INetworkManager.PacketModeEnum.Reliable, (int)ChannelEnum.EntityEventMessage);
    }

    /// <summary>
    /// Sends the whole game state to a specific clientId, used when the client connects to replicate world state
    /// </summary>
    /// <param name="clientId"></param>
    private void SyncWorldState(int clientId)
    {
        foreach (INetworkedEntity entity in _entitySpawner.Entities)
        {
            var entityEvent = new EntityEventMessage
            {
                Event = EntityEventEnum.Created,
                EntityId = entity.EntityId,
                EntityType = entity.EntityType,
                Authority = entity.Authority,
            };

            SendCommandToClient(clientId, entityEvent, INetworkManager.PacketModeEnum.Reliable, (int)ChannelEnum.EntityEventMessage);
        }

    }
}