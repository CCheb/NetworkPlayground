using Godot;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public partial class Lobby : Control
{
	[Export] private GridContainer lobby;
	[Export] private Label countDownLabel;
	private short clientsConnected = 0;
	private short countDown = 5;
	private bool gameStarted = false;
	private bool countdownRunning = false;

    public override void _ExitTree()
    {
        base._ExitTree();
		foreach(UserNpm player in lobby.GetChildren())
		{
			if(player != null)
				player.ReadyUpToggled -= OnPlayerPressedReady;
		}
    }
	
	private void OnPlayerJoined(Node node)
	{
		if(GenericCore.Instance.IsServer)
		{
			clientsConnected++;
			if(node is UserNpm playerCard)
				playerCard.ReadyUpToggled += OnPlayerPressedReady;
		}
	}	

	private void OnPlayerLeft()
	{
		if(GenericCore.Instance.IsServer)
			clientsConnected--;
	}

	private void OnPlayerPressedReady()
	{
		CheckLobbyState();
	}

	private async void CheckLobbyState()
	{
		if (!GenericCore.Instance.IsServer)
        	return;

    	if (clientsConnected < 2 || gameStarted)
    	    return;
	
    	if (!AllPlayersReady())	// Need All players to press ready before proceeding
    	{
    	    ResetCountDown();
    	    return;
    	}
	
    	if (countdownRunning)	// so that it doesnt loop again!
    	    return;
	
    	countdownRunning = true;
    	while (countDown >= 0)
    	{
			countDownLabel.Visible = true;
    		countDownLabel.Text = countDown.ToString();
    		await ToSignal(GetTree().CreateTimer(1.0f), "timeout");

    		if (!AllPlayersReady())
    		{
    		    ResetCountDown();
    		    countdownRunning = false;
    		    return;
    		}

    		countDown--;
    	}
	
    	gameStarted = true;

    	GD.Print("Game Started!");
		
		LoadGame();
	}

	private bool AllPlayersReady()
	{	
		// Assume everyone is ready 
		bool lobbyReady = true;
		foreach(UserNpm player in lobby.GetChildren())
		{	
			if(player == null)	
				break;
	
			if(!player.IsReady)
			{
				lobbyReady = false;
				break;
			}
		}

		return lobbyReady;
	}

	private void ResetCountDown()
	{
		countDown = 5;
		countDownLabel.Text = countDown.ToString();
		countDownLabel.Visible = false;
	}

	private void LoadGame()
	{
		Rpc(MethodName.ChangeToLoadingScreen, "res://scenes/loadingScreen.tscn");
	}
	
	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private async void ChangeToLoadingScreen(string gameScenePath)
	{
	    GD.Print($"Peer {Multiplayer.GetRemoteSenderId()} signaled peer {Multiplayer.GetUniqueId()} to LoadGame");
		
	    if (GenericCore.Instance.IsServer)
	    {
			DiscardNetworkObjects();
	    } 
		
		// Since clients dont have netObjects we could also wait for the server before proceeding
		await WaitForXFrames(4);

	    GetTree().ChangeSceneToFile(gameScenePath);
	}

	private void DiscardNetworkObjects()
	{
		// Server is the only peer that can see the valid _netObjects
	    var objects = GenericCore.Instance._netObjects.Values.ToList();
	    foreach (var netId in objects)
	    {
	        if (netId != null && IsInstanceValid(netId) && netId.GetParent() != null)
	        {
	            netId.GetParent().QueueFree();
	        }
	    }
	    GenericCore.Instance._netObjects.Clear();
	}

	private async Task WaitForXFrames(int x)
	{
		for(int i = 0; i < x; i++)
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	

}
