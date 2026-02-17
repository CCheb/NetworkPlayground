using Godot;
using System;

public partial class NetworkCore : MultiplayerSpawner
{
    [Export]
    public bool SpawnInexZeroOnConnect;

    [Signal]
    public delegate void ExposedClientConnectedEventHandler(long peerId, Godot.Collections.Dictionary<string, string> peerInfo);

    [Signal]
    public delegate void ExposedClientDisconnectedEventHandler(long peerId);

    [Signal]
    public delegate void PlayerJoinedEventHandler(Node node);

    public override void _ExitTree()
    {
        base._ExitTree();
        GenericCore.Instance.ClientConnected -= OnClientConnected;
    }
    public override void _Ready()
    {
        base._Ready();
        GenericCore.Instance.ClientConnected += OnClientConnected;
    }

    public async void slowStart()
    {
        
        while(GenericCore.Instance == null)
        {
            await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
        }

        GenericCore.Instance.ClientConnected += OnClientConnected;
        GenericCore.Instance.ClientDisconnected += OnClientDisconnected;
        
    }

    public Node NetCreateObject(int index, Vector3 initialPosition, Quaternion rotation, long owner = -1L)
    {
        if (!Multiplayer.IsServer())
            return null;
        var packedScene = GD.Load<PackedScene>(_SpawnableScenes[index]);
        var node = packedScene.Instantiate();
        if (node is Node2D)
        {
            ((Node2D)node).GlobalPosition = new Vector2(initialPosition.X, initialPosition.Y);
        }
        if (node is Node3D)
        {
            GD.Print("Spawning 3d Node");
            ((Node3D)node).GlobalPosition = initialPosition;
            ((Node3D)node).Rotation = rotation.GetEuler();
        }
        if (node is Control)
        {
            ((Control)node).GlobalPosition = new Vector2(initialPosition.X, initialPosition.Y);
        }
        GetNode(SpawnPath).AddChild(node, true);
        foreach (var child in node.GetChildren())
            if (child is NetID netId)
            {
                netId.IsGood = true;
                netId.Rpc("Initialize", owner);
                netId._myNetworkCore = this;
                
                GenericCore.Instance.RegisterObject(netId);
            }
        

        EmitSignalPlayerJoined(node);
        return node;
    }

    
    /// <summary>
    /// Destroys all netObjects that were owned by the peer
    /// </summary>
    /// <param name="peerId">The peerId that needs deletion</param>
    private void NetDestroyObject(int peerId)
    {
        Godot.Collections.Array<int> badObjs = new();
        foreach (var i in GenericCore.Instance._netObjects.Keys)
        {
            if (GenericCore.Instance._netObjects[i].OwnerId != peerId) continue;
            badObjs.Add(i);
        }

        foreach (var badObj in badObjs)
        {
            try
            {
                GenericCore.Instance._netObjects[badObj].GetParent().QueueFree();
                GenericCore.Instance._netObjects.Remove(badObj);
            }
            catch
            {
                //Wrong Spawner...
                GD.PushWarning("Notice: Wrong Spawner trying to destroy object.  Not an error.");
            }
        }
    }

    /// <summary>
    /// Destroys a single NetID from the list
    /// </summary>
    /// <param name="netId">The netId that would be deleted</param>
    public void NetDestroyObject(NetID netId)
    {
        if(netId._myNetworkCore == null)
        {
            try
            {
                GenericCore.Instance._netObjects.Remove((int)netId.netObjectID);
                GD.Print("Spawner: " + Name + ", is tring to RPC delete - " + netId.GetParent().Name);
                netId.ReplicationConfig = null;
                netId.Rpc("ManualDelete");
            }  
            catch
            {
                GD.PushWarning("Game Object already Destroyed.");
            }
        }
        if(netId._myNetworkCore != this)
        {
            //Avoid a problem whenever possible.
            return;
        }
        foreach (var i in GenericCore.Instance._netObjects.Keys)
        {
            try
            {
                if (GenericCore.Instance._netObjects[i] != netId) continue;
                GenericCore.Instance._netObjects[i].GetParent().QueueFree();
                GenericCore.Instance._netObjects.Remove(i);
            }
            catch
            {
                //Wrong Spawner...
                GD.PushWarning("Notice: Wrong Spawner trying to destroy object.  Not an error.");
            }
        }
    }
    

    public void OnClientDisconnected(long id)
    {
        //NetDestroyObject((int)id);
        EmitSignalExposedClientDisconnected(id);
    }

    public void OnClientConnected(long peerId, Godot.Collections.Dictionary<string, string> peerInfo) 
    {
        if(SpawnInexZeroOnConnect)
        {
            // Only server instance is able to spawn an object. Since this script is attached under
            // MultiplayerSpawners it will automatically distribute this to all other peers
            if (Multiplayer.IsServer())
            {   // Gets called as soon as the server registers the client
                NetCreateObject(0, new Vector3(0, 0, 0), Quaternion.Identity, peerId);
            }
        }
        EmitSignalExposedClientConnected(peerId, peerInfo);
    }
}
