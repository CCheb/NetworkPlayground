using Godot;
using Godot.Collections;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public partial class GenericCore : Node
{
    [Signal]
    public delegate void ClientConnectedEventHandler(long peerId, Dictionary<string, string> peerInfo);
    [Signal]
    public delegate void ClientDisconnectedEventHandler(long peerId);
    [Signal]
    public delegate void ClientServerNotFoundEventHandler(Error error);
    [Signal]
    public delegate void ServerCreatedEventHandler(Dictionary<string, string> serverInfo);
    [Signal]
    public delegate void ServerDisconnectedEventHandler();
    [Signal]
    public delegate void ServerFailedEventHandler(Error error);
    [Signal]
    public delegate void ClientConnectionOkEventHandler();
    [Signal]
    public delegate void PeerRegisteredEventHandler(long peerId);

    private int _connectionPort = 7000;
    private int _portMinimum = 7010;
    private int _portMaximum = 7000;

    [Export]
    public string PublicIP;
    [Export]
    public string PrivateIP;
    private string _serverAddress = "127.0.0.1";
    private int _maxClientConnections = 4;

    public Dictionary<long, Dictionary<string, string>> _connectedPeers = new();
    private Dictionary<string, string> _localPeerInfo = new()
    {
        // Right now we are not setting _localPeerInfo for each peer
        { "NetID", "1" },
        { "UserName", "John Doe"}
    };

    [Export]
    public Dictionary<int, NetID> _netObjects = new();
    public  uint _netObjectsCount;
    private Godot.Collections.Array<Node> nodesForErase = new Godot.Collections.Array<Node>();

    public static GenericCore Instance { get; private set; }
    public bool IsServer;
    public bool PeerConnected;

    private int _playersLoaded = 0;
    struct NetworkPing
    {
        public System.Net.NetworkInformation.Ping ping;
        public System.Net.NetworkInformation.PingOptions pingOption;

        public NetworkPing()
        {
            ping = new System.Net.NetworkInformation.Ping();
            pingOption = new System.Net.NetworkInformation.PingOptions(0,true);
        }
    }

    public long GetServerNetId()
    {
        // Each peer is first in the _connectedPeers dictionary
        return _connectedPeers.First().Key;
    }

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

    public async Task JoinWan()
    {
        GD.Print("Attempting to connect to public IP.");
        GD.Print("Trying Public IP Address: " + PublicIP.ToString());

        NetworkPing networkPing = new NetworkPing();
        System.Net.NetworkInformation.PingReply pr = SendPingTo(PublicIP, networkPing);
        await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
        GD.Print("Ping Return: " + pr.Status.ToString());

        if (pr.Status == System.Net.NetworkInformation.IPStatus.Success)
        {
            GD.Print("The public IP responded with a roundtrip time of: " + pr.RoundtripTime);
            _serverAddress = PublicIP;
            JoinGame();
        }
        else
        {
            GD.Print("The public IP failed to respond. Trying LAN");
            JoinLAN(networkPing);
        }
    }

    private async void JoinLAN(NetworkPing networkPing)
    {
        GD.Print("Trying Florida Poly Address: " + PrivateIP.ToString());

        System.Net.NetworkInformation.PingReply pr = SendPingTo(PrivateIP, networkPing);
        await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
        GD.Print("Ping Return: " + pr.Status.ToString());

        if (pr.Status == System.Net.NetworkInformation.IPStatus.Success)
        {
            GD.Print("The Florida Poly IP responded with a roundtrip time of: " + pr.RoundtripTime);
            _serverAddress = PrivateIP;
            JoinGame();
        }
        else
        {
            GD.Print("The Florida Poly IP failed to respond. Defaulting to LocalHost");
            JoinLocal();
        }
    }

    private void JoinLocal()
    {
        GD.Print("Using Home Address!");
        if (JoinGame() != Error.Ok)
        {
            _serverAddress = "127.0.0.1";
            JoinGame();
        }
    }

    private System.Net.NetworkInformation.PingReply SendPingTo(string IP, NetworkPing networkPing)
    {
        string data = "HELLLLOOOOO!";
        byte[] buffer = ASCIIEncoding.ASCII.GetBytes(data);
        int timeout = 500;
        return  networkPing.ping.Send(IP, timeout, buffer, networkPing.pingOption);
    }

    public void ParseInitialPromptInfo(string userName, string serverAddress, int portNumber)
    {
        _localPeerInfo["UserName"] = userName;
        _serverAddress = serverAddress;
        _connectionPort = portNumber;
    }

    /// <summary>
    /// Client starts here. This gets called when a client peer clicks the join game button
    /// </summary>
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

        //CheckForObjectsOnScene(GetTree().Root);
        EmitSignalServerCreated(_localPeerInfo);
        
        IsServer = true;
        PeerConnected = true;
        
        return Error.Ok;
    }

    /// <summary>
    /// Sends a message to the rest of the clients to register this player.
    /// </summary>
    /// <param name="id"></param>
    private void OnPeerConnected(long id)
    {
        // Plays both ways. When client connects it sends its info to other connected peers (via id)
        // Other connected peers then send their info back to the player (via id)
        RpcId(id, MethodName.RegisterPeer, _localPeerInfo);
        EmitSignalClientConnectionOk();
        GD.Print("Client Connected!");
    }

    /// <summary>
    /// Sends a message to the local instance that a client disconnected<br/>
    /// Also removes player from connected peers table
    /// </summary>
    /// <param name="id"></param>
    private void OnPeerDisconnected(long id)
    {
        _connectedPeers.Remove(id);
        //Need to destroy objects.
        EmitSignalClientDisconnected(id);
        if (!Multiplayer.IsServer()) return;

    }

    private void OnClientConnectSuccess()
    {
        int peerId = Multiplayer.GetUniqueId();
        // We are currently not setting the player info on itself!!!
        _connectedPeers[peerId] = _localPeerInfo;
        // For any UI nodes that might need to update
        GD.Print(_connectedPeers);
        EmitSignalClientConnected(peerId, _localPeerInfo);
    }

    private void OnConnectionToServerFail()
    {
        // Reasuring that the MultiplayerPeer is null
        Multiplayer.MultiplayerPeer = null;
    }

    private void OnServerDisconnected()
    {
        Multiplayer.MultiplayerPeer = null;
        _connectedPeers.Clear();
        EmitSignalServerDisconnected();
    }

    /// <summary>
    /// This function is called from local but not run on local<br/>
    /// This function tells the other peers (including server) that they joined the network.
    /// 
    /// _connectedPeers looks like:
    /// {567 : {NETID : 567}}
    /// </summary>
    /// <param name="peerInfo"></param>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RegisterPeer(Dictionary<string, string> peerInfo)
    {
        // From the perspective of the receiver
        int newPeerId = Multiplayer.GetRemoteSenderId(); //Who is sending to call this function
        GD.Print($"Peer {Multiplayer.GetUniqueId()} registering peer {newPeerId}");
        if (newPeerId == 1 && !Multiplayer.IsServer())  
            peerInfo["NetID"] = GetServerNetId().ToString();
        else
            peerInfo["NetID"] = newPeerId.ToString(); //Updating the dictionary for the new player
        _connectedPeers[newPeerId] = peerInfo;
        EmitSignalClientConnected(newPeerId, peerInfo); //Update local instance to new player
        EmitSignalPeerRegistered(newPeerId);
        GD.Print(_connectedPeers);

    }

    public void RegisterObject(NetID netId)
    {
        //netId.Rpc("Initialize", 1);
        GD.Print("NET ID INTEGER IS: " + Instance._netObjectsCount);
        netId.netObjectID = Instance._netObjectsCount;
        Instance._netObjects.Add((int)Instance._netObjectsCount++, netId);
    }

    public override Array<Dictionary> _GetPropertyList()
    {
        var propList = new Array<Dictionary>();

        propList.AddRange([
            new()
            {
                { "name", "_portMaximum" },
                { "type", (int)Variant.Type.Int },
                { "usage", (int)PropertyUsageFlags.Default },
                { "hint", (int)PropertyHint.Range },
                { "hint_string", "0,65535,1,hide_slider" }
            },
            new()
            {
                { "name", "_portMinimum" },
                { "type", (int)Variant.Type.Int },
                { "usage", (int)PropertyUsageFlags.Default },
                { "hint", (int)PropertyHint.Range },
                { "hint_string", "0,65535,1,hide_slider" }
            },
            new()
            {
                { "name", "_connectionPort" },
                { "type", (int)Variant.Type.Int },
                { "usage", (int)PropertyUsageFlags.Default },
                { "hint", (int)PropertyHint.Range },
                { "hint_string", "0,65535,1,hide_slider" }
            },
            new Dictionary()
            {
                { "name", "_serverAddress" },
                { "type", (int)Variant.Type.String },
                { "usage", (int)PropertyUsageFlags.Default },
            },
            new Dictionary()
            {
                { "name", "_maxConnections" },
                { "type", (int)Variant.Type.Int },
                { "usage", (int)PropertyUsageFlags.Default },
                { "hint", (int)PropertyHint.Range },
                { "hint_string", "0,100,1,or_greater,hide_slider" }
            },
            new Dictionary()
            {
                { "name", "_connectedPeers" },
                { "type", (int)Variant.Type.Dictionary },
                { "usage", (int)(PropertyUsageFlags.ReadOnly | PropertyUsageFlags.Editor) },
                { "hint", (int)PropertyHint.TypeString },
                {
                    "hint_string",
                    $"{Variant.Type.Int:D}:; {Variant.Type.Dictionary:D}/{PropertyHint.DictionaryType:D}:"
                }
            },
        ]);

        return propList;
    }

    public void SetConnectionPort(string s)
    {
        try
        {
            _connectionPort = int.Parse(s);
        }
        catch (Exception ex)
        {
            GD.Print($"{ex.Message}\nDefaulting to port 7000");
            _connectionPort = 7000;
        }
    }
    public void SetIP(string s)
    {
        _serverAddress = s;
    }

    public void JOIN_WAN_CALLBACK()
    {
        _connectionPort = _portMinimum;
        //JoinWan();
    }

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
