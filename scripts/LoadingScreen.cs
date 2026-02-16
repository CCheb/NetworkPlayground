using Godot;
using System;

public partial class LoadingScreen : Control
{	
	private const string levelPath = "res://scenes/level.tscn";
	private const short SERVER = 1;
	private int _playersLoaded = 0;
	private bool sentLoaded = false;
	
	public override void _Ready()
	{
		ResourceLoader.LoadThreadedRequest(levelPath);
	}

	public override void _Process(double delta)
	{
		if(sentLoaded)
			return;

		var status = ResourceLoader.LoadThreadedGetStatus(levelPath);

		if(status == ResourceLoader.ThreadLoadStatus.Loaded)
		{	
			sentLoaded = true;
			GenericCore.Instance.RpcId(SERVER, "PlayerLoaded");
		}
	}

	
}
