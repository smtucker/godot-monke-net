using Godot;
using MonkeNet.NetworkMessages;
using System.Collections.Generic;

namespace MonkeNet.Shared;

public abstract partial class EntitySpawner : Node
{
    public const int AuthorityServer = 0;

    [Signal] public delegate void EntitySpawnedEventHandler(Node3D entity);

    public static EntitySpawner Instance { get; private set; }
    public List<INetworkedEntity> Entities { get; private set; } = []; //TODO: make dictionary for easier access

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
    public INetworkedEntity GetEntityById(int entityId)
    {
        for (int i = 0; i < Entities.Count; i++)
        {
            if (Entities[i] is INetworkedEntity networkedEntity && networkedEntity.EntityId == entityId)
            {
                return networkedEntity;
            }
        }

        throw new MonkeNetException($"Couldn't find entity by id {entityId}");
    }

    // Can be called from both the server or a client, so it needs to handle both scenarios
    public Node3D SpawnEntity(EntityEventMessage @event)
    {
        Node3D instancedNode;
        if (MonkeNetManager.Instance.IsServer)
        {
            instancedNode = HandleEntityCreationServerSide(@event);
        }
        else
        {
            instancedNode = HandleEntityCreationClientSide(@event);
        }

        if (instancedNode is not INetworkedEntity networkedEntity)
        {
            throw new MonkeNetException($"Can't spawn entity that is not a {typeof(INetworkedEntity).Name}");
        }

        InitializeEntity(instancedNode, networkedEntity, @event);
        AddChild(instancedNode);
        Entities.Add(networkedEntity);
        EmitSignal(SignalName.EntitySpawned, instancedNode);
        networkedEntity.EntitySpawned();
        GD.Print($"Spawned entity:{@event.EntityId} ({@event.EntityType}) Auth:{@event.Authority}");
        return instancedNode;
    }

    public void DestroyEntity(EntityEventMessage @event)
    {
        var entity = GetNode<INetworkedEntity>(@event.EntityId.ToString());
        // entity.QueueFree();
        // Entities.Remove(entity);
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

    private static void InitializeEntity(Node node, INetworkedEntity entity, EntityEventMessage @event)
    {
        node.Name = @event.EntityId.ToString();
        entity.EntityId = @event.EntityId;
        entity.EntityType = @event.EntityType;
        entity.Authority = @event.Authority;
        entity.Metadata = @event.Metadata;
    }
}