using Godot;
using System;

public partial class LoadingScreen : Control
{	
	private const string levelPath = "res://scenes/level.tscn";
	private int _playersLoaded = 0;
	private bool alreadyLoaded = false;

	private const short SERVER = 1;
	private const ResourceLoader.ThreadLoadStatus LOADED = ResourceLoader.ThreadLoadStatus.Loaded;
	
	public override void _Ready()
	{
		StartLoadingLevel();
	}

	private void StartLoadingLevel()
	{
		ResourceLoader.LoadThreadedRequest(levelPath);
	}

	public override void _Process(double delta)
	{
		if(alreadyLoaded)	// Prevent client from resending RPC to server
			return;

		var levelStatus = GetLevelLoadingStatus();

		if(levelStatus == LOADED)
		{	
			alreadyLoaded = true;
			GenericCore.Instance.RpcId(SERVER, "PlayerLoaded");
		}
	}
	private ResourceLoader.ThreadLoadStatus GetLevelLoadingStatus()
	{
		return ResourceLoader.LoadThreadedGetStatus(levelPath);
	}
}
