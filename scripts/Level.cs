using Godot;
using System;

public partial class Level : Node3D
{
	[Export] private Godot.Collections.Array<Marker3D> playerSpawns;
	[Export] private Marker3D spectatorSpawn;
	[Export] private NetworkCore netCore;
	[Export] private PackedScene cameraScene;
	
	public override void _Ready()
	{
		if(!GenericCore.Instance.IsServer)
			return;

		int count = 0;
		foreach(var peer in GenericCore.Instance._connectedPeers)
		{
			if(peer.Key != 1)
			{
				netCore.NetCreateObject(0, playerSpawns[count].GlobalPosition, playerSpawns[count].Quaternion, peer.Key);
			}
			else
			{
				Camera3D playerCamera = cameraScene.Instantiate<Camera3D>();
				spectatorSpawn.AddChild(playerCamera);
			}
			count++;
		}
	}
}
