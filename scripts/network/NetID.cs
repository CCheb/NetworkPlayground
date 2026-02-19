using Godot;

[GlobalClass]
//[Tool]
public partial class NetID : MultiplayerSynchronizer
{
    [Signal] public delegate void NetIdIsReadyEventHandler();
    [Export] public NetworkCore _myNetworkCore;
    public bool IsLocal;
    public bool IsServer;
    public long OwnerId;
    public uint netObjectID;

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Initialize(long peerIdOwner)
    {
        // For clients it will be that big int id
        OwnerId = peerIdOwner;
        if (peerIdOwner == 1)
            IsServer = true;
        if (Multiplayer.GetUniqueId() == OwnerId)
            IsLocal = true;
        EmitSignalNetIdIsReady();
    }
}