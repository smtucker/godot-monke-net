using Godot;
using MonkeNet.Client;
using MonkeNet.NetworkMessages;
using System;
using System.Collections.Generic;

namespace MonkeNet.Shared;

public partial class EntitySpawner : Node
{
    public const int AuthorityServer = 0;

    [Signal] public delegate void EntitySpawnedEventHandler(Node3D entity);
    [Signal] public delegate void EntityDiedEventHandler(Node3D entity);

    public static EntitySpawner Instance { get; private set; }
    public List<NetworkBehaviour> Entities { get; private set; } = []; //TODO: make dictionary for easier access

	private List<INetworkedEntity> _entitiesToDestroy = [];	

    protected abstract Node3D HandleEntityCreationClientSide(EntityEventMessage @event);
    protected abstract Node3D HandleEntityCreationServerSide(EntityEventMessage @event);

    public override void _Ready()
    {
        Instance = this;
    }

	public void PurgeEntities()
	{
		foreach (INetworkedEntity entity in _entitiesToDestroy)
        {
            entity.QueueFree();
			Entities.Remove(entity);
        }
        _entitiesToDestroy.Clear();
	}

    //TODO: do not cast, make Entities a list of INetworkedEntity directly
    public NetworkBehaviour GetEntityById(int entityId)
    {
        for (int i = 0; i < Entities.Count; i++)
        {
            if (Entities[i] is NetworkBehaviour networkedEntity && networkedEntity.EntityId == entityId)
            {
                return networkedEntity;
            }
        }

        throw new MonkeNetException($"Couldn't find entity by id {entityId}");
    }

    // Can be called from both the server or a client, so it needs to handle both scenarios
    public Node SpawnEntity(EntityEventMessage @event)
    {
        var config = MonkeNetConfig.Instance
            .GetSpawnConfigurationForEntityType(@event.EntityType);

        var scene = SolveWhatEntitySceneToSpawn(config, @event);

        var instance = scene?.Instantiate()
            ?? throw new MonkeNetException($"Couldn't instance entity {@event.EntityType}");

        NetworkBehaviour networkBehaviour = MonkeNetComponents.GetComponent<NetworkBehaviour>(instance)
            ?? throw new MonkeNetException($"Can't spawn entity that doesn't have a {nameof(NetworkBehaviour)} node!");

        InitializeEntity(instance, networkBehaviour, @event);
        AddChild(instance);
        Entities.Add(networkBehaviour);
        EmitSignal(SignalName.EntitySpawned, instance);
        networkBehaviour.OnEntitySpawned();

        GD.Print($"Spawned entity:{@event.EntityId} ({@event.EntityType}) Auth:{@event.Authority}");
        return instance;
    }

    public void DestroyEntity(EntityEventMessage @event)
    {
        //    var entity = GetNode<NetworkBehaviour>(@event.EntityId.ToString());
        //    entity.Free();
        //    Entities.Remove(entity);
        // throw new NotImplementedException();

        var entity = GetNode<INetworkedEntity>(@event.EntityId.ToString());
        // entity.QueueFree();
        // Entities.Remove(entity);
		_entitiesToDestroy.Add(entity);
    }
	
	public void KillEntity(EntityEventMessage @event)
	{
		GD.Print($"EntitySpawner: Death event for {@event.EntityId}");
		var entity = GetNode<INetworkedEntity>(@event.EntityId.ToString());
		if (entity is Node3D node3d)
		{
			EmitSignal(SignalName.EntityDied, node3d);
		}
		_entitiesToDestroy.Add(entity);
	}

    public List<int> GetAllEntitiesByAuthority(int authority)
    {
        List<int> entitiesGeneratedByAuthority = [];

        for (int i = 0; i < Entities.Count; i++)
        {
            if (Entities[i].Authority == authority)
            {
                entitiesGeneratedByAuthority.Add(Entities[i].EntityId);
            }
        }

        return entitiesGeneratedByAuthority;
    }

    private static void InitializeEntity(Node node, NetworkBehaviour entity, EntityEventMessage @event)
    {
        node.Name = @event.EntityId.ToString();
        entity.EntityId = @event.EntityId;
        entity.EntityType = @event.EntityType;
        entity.Authority = @event.Authority;
        entity.Metadata = @event.Metadata;

        // Apply position and rotation from the event. This is crucial for clients.
        if (node is Node3D node3d)
        {
            node3d.Position = @event.Position;
            var rot = node3d.Rotation;
            rot.Y = @event.Yaw;
            node3d.Rotation = rot;
        }
    }

    private PackedScene SolveWhatEntitySceneToSpawn(EntitySpawnConfiguration entitySpawnConfig, EntityEventMessage @event)
    {
        if (MonkeNetManager.Instance.IsServer)
            return entitySpawnConfig.ServerScene;

        bool isAuthority = @event.Authority == ClientManager.Instance.GetNetworkId();

        if (isAuthority)
            return entitySpawnConfig.ClientAuthorityScene;

        return entitySpawnConfig.ClientDummyScene;
    }
}