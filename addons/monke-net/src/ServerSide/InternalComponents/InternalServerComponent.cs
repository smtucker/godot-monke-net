using Godot;
using MonkeNet.Serializer;
using MonkeNet.Shared;

namespace MonkeNet.Server;

[GlobalClass, Icon("res://addons/monke-net/resources/link-solid.png")]
public abstract partial class InternalServerComponent : Node
{
    protected virtual void OnCommandReceived(int clientId, IPackableMessage command) { }
    protected virtual void OnProcessTick(int currentTick) { }
    protected virtual void OnNetworkProcessTick(int currentTick) { }
    protected virtual void OnClientConnected(int peerId) { }
    protected virtual void OnClientDisconnected(int peerId) { }

    public override void _Ready()
    {
        ServerManager.Instance.ServerTick += OnProcessTick;
        ServerManager.Instance.ServerNetworkTick += OnNetworkProcessTick;
        ServerManager.Instance.CommandReceived += OnCommandReceived;
        ServerManager.Instance.ClientConnected += OnClientConnected;
        ServerManager.Instance.ClientDisconnected += OnClientDisconnected;
    }

    protected static void SendCommandToClient(int peerId, IPackableMessage command, INetworkManager.PacketModeEnum mode, int channel)
    {
        ServerManager.Instance.SendCommandToClient(peerId, command, mode, channel);
    }

    protected int NetworkId
    {
        get { return ServerManager.Instance.GetNetworkId(); }
    }
}