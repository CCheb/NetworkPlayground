using Godot;
using Godot.Collections;

public partial class GenericCore : Node
{
    [Signal]
    public delegate void ClientConnectedNotifierEventHandler(long peerId, Dictionary<string, string> peerInfo);
    [Signal]
    public delegate void ClientDisconnectedNotifierEventHandler(long peerId);
    [Signal]
    public delegate void ClientServerNotFoundNotifierEventHandler(Error error);
    [Signal]
    public delegate void ServerCreatedEventHandler(Dictionary<string, string> serverInfo);
    [Signal]
    public delegate void ServerDisconnectedEventHandler();
    [Signal]
    public delegate void ServerFailedEventHandler(Error error);
    [Signal]
    public delegate void ClientConnectionOkEventHandler();
    [Signal]
    public delegate void ConnectedPeersDictionaryUpdatedEventHandler(long newPeerId);

    private int _connectionPort = 7000;
    private int _portMinimum = 7010;
    private int _portMaximum = 7000;

    private string _serverAddress = "127.0.0.1";
    private int _maxClientConnections = 4;

    public Dictionary<long, Dictionary<string, string>> _connectedPeers = new();
    private Dictionary<string, string> _localPeerInfo = new()
    {
        { "NetID", "1" },
        { "UserName", "John Doe"}
    };

    public Dictionary<int, NetID> _netObjects = new();  // Server will be only one to see netObjects
    private Array<Node> nodesForErase = new Array<Node>();

    public static GenericCore Instance { get; private set; }
    public bool IsServer;
    public bool PeerConnected;

    public override void _Ready()
    {
        base._Ready();
        SetInstance();
        SetNetworkSignals();
        CheckPorts();
        CheckForCommandLineArgs();
    }

    private void SetInstance()
    {
        Instance ??= this;
        GD.Print("Instance static variable set!"); 
    }

    private void SetNetworkSignals()
    {
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnClientConnectSuccess;
        Multiplayer.ConnectionFailed += OnConnectionToServerFail;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
    }

    private void CheckPorts()
    {
        if (_portMinimum > _portMaximum)
            (_portMinimum, _portMaximum) = (_portMaximum, _portMinimum);
    }

    private void CheckForCommandLineArgs()
    {
        string[] args = OS.GetCmdlineArgs();
        foreach (string arg in args)
        {
            if (arg == "MASTER")
            {
                CreateGame();
            }
        }
    }

    // Client starts here. This gets called when a client peer clicks the join game button
    public Error JoinGame()
    {
        GD.Print($"Attempting to connect to {_serverAddress}:{_connectionPort}");
        
        var peer = new ENetMultiplayerPeer();
        Error error = peer.CreateClient(_serverAddress, _connectionPort);
        if (error != Error.Ok)
            return error;

        GD.Print("Connected to server");

        // PeerConnected and ConnectedToServer signals will implicitly trigger if connection is made to server
        Multiplayer.MultiplayerPeer = peer;
    
        PeerConnected = true;
        return Error.Ok;
    }

    // Server starts here. This gets called when a server peer clicks the host game button
    public Error CreateGame()
    {
        GD.Print($"Attempting to create server at {_serverAddress}:{_connectionPort}");

        var peer = new ENetMultiplayerPeer();
        Error error = peer.CreateServer(_connectionPort, _maxClientConnections);
        if (error != Error.Ok)
        {
            EmitSignalServerFailed(error);
            return error;
        }

        GD.Print("Created Local Game");

        // If server, no signals get emitted since its techinically the first peer
        // Because of this we need to set things up manually by setting the _localPeerInfo
        Multiplayer.MultiplayerPeer = peer;

        _connectedPeers[1] = _localPeerInfo;
        EmitSignalServerCreated(_localPeerInfo);
        
        IsServer = true;
        PeerConnected = true;
        
        return Error.Ok;
    }

    // Emits on local client that just connected. Emits before OnPeerConnected
    private void OnClientConnectSuccess()
    {
        int peerId = Multiplayer.GetUniqueId();
        _connectedPeers[peerId] = _localPeerInfo; 
    }

    // Sends a message to the rest of the clients to register this peer
    private void OnPeerConnected(long id)
    {
        // Plays both ways. When client connects it sends its info to other connected peers (via id)
        // Other connected peers then send their info back to the player (via id)
        RpcId(id, MethodName.RegisterPeer, _localPeerInfo);
        EmitSignalClientConnectionOk();
        GD.Print("Client Connected!");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RegisterPeer(Dictionary<string, string> peerInfo)
    {
        // From the perspective of the receiver. Goes both ways
        int newPeerId = Multiplayer.GetRemoteSenderId(); //Who is sending to call this function

        GD.Print($"Peer {Multiplayer.GetUniqueId()} registering peer {newPeerId}");

        if (newPeerId == 1 && !Multiplayer.IsServer())  
            peerInfo["NetID"] = Multiplayer.GetUniqueId().ToString();
        else
            peerInfo["NetID"] = newPeerId.ToString(); 

        //Updating the dictionary for the new player
        _connectedPeers[newPeerId] = peerInfo;
        EmitSignalClientConnectedNotifier(newPeerId, peerInfo); // Notify NetCore for object creation
        EmitSignalConnectedPeersDictionaryUpdated(newPeerId);

        GD.Print(_connectedPeers);
    }

    public void RegisterObject(NetID netId)
    {
        netId.netObjectID = (uint)Instance._netObjects.Count;
        Instance._netObjects.Add(Instance._netObjects.Count, netId);
    }
    
    // Sends a message to all connectedPeers that a client disconnected
    // Also removes player from connected peers table
    private void OnPeerDisconnected(long id)
    {
        _connectedPeers.Remove(id);
        //Need to destroy objects.
        EmitSignalClientDisconnectedNotifier(id);
    }

    private void OnConnectionToServerFail()
    {
        Multiplayer.MultiplayerPeer = null;
    }

    private void OnServerDisconnected()
    {
        // null == complete network disconnecion/reset
        Multiplayer.MultiplayerPeer = null;
        _connectedPeers.Clear();
        EmitSignalServerDisconnected();
    }

    public void ParseInitialPromptInfo(string userName, string serverAddress, int portNumber)
    {
        _localPeerInfo["UserName"] = userName;
        _serverAddress = serverAddress;
        _connectionPort = portNumber;
    }

    //----//

    private int _playersLoaded = 0;

    [Rpc(MultiplayerApi.RpcMode.AnyPeer,CallLocal = true,TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void PlayerLoaded()
    {
        if (Multiplayer.IsServer())
        {
            GD.Print($"Peer {Multiplayer.GetRemoteSenderId()} just loaded in!");
            _playersLoaded += 1;
            if (_playersLoaded == Instance._connectedPeers.Count)
            {
                _playersLoaded = 0;
                Rpc("StartGame");
            }
        }
    }

    [Rpc(CallLocal = true,TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void StartGame()
	{
		GD.Print($"Peer {Multiplayer.GetUniqueId()} now starting...");
		PackedScene level = (PackedScene)ResourceLoader.LoadThreadedGet("res://scenes/level.tscn");
		GetTree().ChangeSceneToPacked(level);
	}

}
