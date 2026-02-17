using Godot;
using System;

public partial class NetworkCore : MultiplayerSpawner
{
    [Signal]
    public delegate void PlayerJoinedEventHandler(Node node);

    [Export] 
    public bool SpawnInexZeroOnConnect;

    public override void _ExitTree()
    {
        base._ExitTree();
        GenericCore.Instance.ClientConnectedNotifier -= OnClientConnected;
        GenericCore.Instance.ClientDisconnectedNotifier -= OnClientDisconnected;
    }
    public override void _Ready()
    {
        base._Ready();
        GenericCore.Instance.ClientConnectedNotifier += OnClientConnected;
        GenericCore.Instance.ClientDisconnectedNotifier += OnClientDisconnected;
    }

    public void OnClientConnected(long newPeerId, Godot.Collections.Dictionary<string, string> newPeerInfo) 
    {   
        // Server tries to spawn something for the new client
        if(SpawnInexZeroOnConnect && Multiplayer.IsServer())
        {
            NetCreateObject(0, new Vector3(0, 0, 0), Quaternion.Identity, newPeerId);
        }
    }

    public void NetCreateObject(int index, Vector3 initialPosition, Quaternion rotation, long owner = -1L)
    {
        if (!Multiplayer.IsServer())
            return;

        Node rootNode = GetNodeFromAutoList(index);

        FindRootNodeType(ref rootNode, initialPosition, rotation);

        GetNode(SpawnPath).AddChild(rootNode, true);
        
        FindRootNodeNetID(rootNode, owner);
        
        EmitSignalPlayerJoined(rootNode);
    }

    private Node GetNodeFromAutoList(int index)
    {   
        var packedScene = GD.Load<PackedScene>(_SpawnableScenes[index]);
        return packedScene.Instantiate();
    }

    private void FindRootNodeType(ref Node rootNode, Vector3 initialPosition, Quaternion rotation)
    {
        if (rootNode is Node2D node2D)
        {
            var transform = new Transform2D(0.0f, new Vector2(initialPosition.X, initialPosition.Y));
            node2D.Transform = transform;
        }
        if (rootNode is Node3D node3D)
        {   
            // We build transform since using GlobalPosition requires that the node be in the scene tree already
            // But at this stage it has not been added yet so we set its transform matrix
            var basis = new Basis(rotation);
            var tranform = new Transform3D(basis, initialPosition);

            node3D.Transform = tranform;
        }
        if (rootNode is Control control)
        {
            control.GlobalPosition = new Vector2(initialPosition.X, initialPosition.Y);
        }
    }

    private void FindRootNodeNetID(Node rootNode, long owner)
    {
        foreach (var child in rootNode.GetChildren())
        {
            if (child is NetID netId)
            {
                netId.IsGood = true;
                netId.Rpc("Initialize", owner);
                netId._myNetworkCore = this;
                
                GenericCore.Instance.RegisterObject(netId);
            } 
        }
    }
    
    public void OnClientDisconnected(long peerId)
    {
        NetDestroyObject(peerId);
    }
    
    // Destroys all netObjects that were owned by the peer
    private void NetDestroyObject(long peerId)
    {
        
        Godot.Collections.Array<int> badObjs = FindBadObjects(peerId);

        foreach (var badObj in badObjs)
        {
            try {
                GenericCore.Instance._netObjects[badObj].GetParent().QueueFree();
                GenericCore.Instance._netObjects.Remove(badObj);
            }
            catch{
                GD.PushWarning("Notice: Wrong Spawner trying to destroy object.  Not an error.");
            }
        }
    }

    private Godot.Collections.Array<int> FindBadObjects(long peerId)
    {
        Godot.Collections.Array<int> badObjs = new();
        foreach (var i in GenericCore.Instance._netObjects.Keys)
        {
            if (GenericCore.Instance._netObjects[i].OwnerId != peerId) continue;
            badObjs.Add(i);
        }
        
        return badObjs;
    }

    // Destroys a single NetID from the list
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
}
