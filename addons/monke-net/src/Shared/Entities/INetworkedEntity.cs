namespace MonkeNet.Shared;

/// <summary>
/// All entities, client/server side implement this interface, it contains information about entity id, type, ownership, etc.
/// </summary>
public interface INetworkedEntity
{
    public int EntityId { get; set; }
    public byte EntityType { get; set; }
    public int Authority { get; set; }
    public string Metadata { get; set; }

    public void Free();
    public void EntitySpawned();
	public void QueueFree();
}