using Godot;
using System;
using System.ComponentModel;

public partial class UserNpm : Control
{	
	[Signal] public delegate void ReadyUpToggledEventHandler();
	[Export] private NetID myNetID;
	[Export] private Label userNameLabel;
	[Export] private ColorRect readyStatus;
	[Export] private Button readyUpButton;
	private bool hasOwner = false;
	private bool hasPeerData = false;
	public bool IsReady = false;
	private const int SERVER = 1;
    // Called when the node enters the scene tree for the first time.
    public override void _ExitTree()
    {
        base._ExitTree();
		myNetID.NetIdIsReady -= OnNetIdReady;
		GenericCore.Instance.PeerRegistered -= OnPeerRegistered;
    }
	public override void _Ready()
	{
		myNetID.NetIdIsReady += OnNetIdReady;
		GenericCore.Instance.PeerRegistered += OnPeerRegistered;
	}

	// Multiplayer has multiple clocks and we cant wait for the whole system
	private void OnNetIdReady()
	{
		
		GD.Print("NetID Is ready!");
		hasOwner = true;

		if (PeerDictionaryReady())	// In the case OnNetIdReady executes after OnPeerRegistered
    	{	
        	hasPeerData = true;
    	}

		if(myNetID.IsLocal)
			readyUpButton.Visible = true;
		// To update username we only need the minimum that it needs (ownerID and _connectedPeers)
		TryToInitialize();
	}

	private bool PeerDictionaryReady()
	{
		return GenericCore.Instance._connectedPeers.ContainsKey(myNetID.OwnerId);
	}

	private void OnPeerRegistered(long peerId)
	{
		if(peerId == myNetID.OwnerId)
		{
			hasPeerData = true;
			// To update username we only need the minimum that it needs (ownerID and _connectedPeers)
			TryToInitialize();
		}
	}

	private void TryToInitialize()
	{
		if(hasOwner && hasPeerData)
			SetUserName();
	}

	private void SetUserName()
	{
		userNameLabel.Text = GenericCore.Instance._connectedPeers[myNetID.OwnerId]["UserName"];
	}

	private void OnReadyUpButtonToggled(bool toggledOn)
	{
		// Goes to the matching NodePath of the card on the Server side
		RpcId(SERVER, MethodName.RequestReadyUp, toggledOn);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RequestReadyUp(bool toggledOn)
	{
		if(GenericCore.Instance.IsServer)
		{
			// Server updates the local card's UI and sends that result out
			readyStatus.Color = toggledOn ? new Color("Green") : new Color("Red");
			IsReady = toggledOn;

			EmitSignalReadyUpToggled();
		}
	}

	
}
