using Godot;

[GlobalClass]
//[Tool]
public partial class NetID : MultiplayerSynchronizer
{
    [Signal] public delegate void NetIdIsReadyEventHandler();
    [Export] public bool IsLocal;
    [Export] public bool IsServer;
    [Export] public long OwnerId;
    [Export] public uint netObjectID;
    [Export] public NetworkCore _myNetworkCore;
    [Export] public bool IsGood = false;
    public override void _EnterTree()
    {
        base._EnterTree();

        if (ReplicationConfig == null)
        {
            GD.Print("No replication config found, creating one.");
            ReplicationConfig = new SceneReplicationConfig();
        }

        var config = ReplicationConfig as SceneReplicationConfig;
        if (config == null)
        {
            GD.PushError("ReplicationConfig is not a SceneReplicationConfig!");
            return;
        }
        if (!config.HasProperty("MultiplayerSynchronizer:IsGood"))
        {
            config.AddProperty("MultiplayerSynchronizer:IsGood");
        }
        if (!config.HasProperty("MultiplayerSynchronizer:OwnerId"))
        {
            config.AddProperty("MultiplayerSynchronizer:OwnerId");
        }
    }

    public override void _Ready()
    {
        base._Ready();
     
        slowStart();
    }

    public async void slowStart()
    {
        await ToSignal(GetTree().CreateTimer(0.15f), SceneTreeTimer.SignalName.Timeout);
        while (GenericCore.Instance == null || !GenericCore.Instance.PeerConnected)
        {
            await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
        }
        if(GenericCore.Instance.IsServer && OwnerId ==0)
        {
            OwnerId = 1;
            if (Multiplayer.GetUniqueId() == OwnerId)
            IsLocal = true;
            //GenericCore.Instance.RegisterObject(this);
            SetMultiplayerAuthority(1); // 1 = server
            IsGood = true;
        }
        await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
        if (!GenericCore.Instance.IsServer)
        {
            await ToSignal(GetTree().CreateTimer(1.5f), SceneTreeTimer.SignalName.Timeout);
            if (!IsGood)
            {
                GD.Print("Deleting the inscene object: " + GetParent().Name);
        
                GetParent().QueueFree();
            }
        }
    }

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

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public async void ManualDelete()
    {
        GD.Print("Trying to remote destroy an object: " + GetParent().Name);
        if(ReplicationConfig != null)
        {
            try
            {
                ReplicationConfig = null;
            }
            catch {//Stop stupid chatty error
                   }
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetParent().QueueFree();
    }
}