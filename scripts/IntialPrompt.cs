using Godot;
using System;

public partial class IntialPrompt : Control
{
	[Export] private LineEdit UsernameEntryBox;
	[Export] private LineEdit ServerAddressEntryBox;
	[Export] private LineEdit PortNumberAddressEntryBox;

    public override void _Ready()
    {
        base._Ready();
		GenericCore.Instance.ClientConnectionOk += OnConnectionSuccessful;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
		GenericCore.Instance.ClientConnectionOk -= OnConnectionSuccessful;
    }

	private void OnConnectionSuccessful()
	{
		// Load lobby scene
		GetTree().ChangeSceneToFile("res://scenes/lobby.tscn");
	}

	private void OnJoinButtonPressed()
	{
		if(UsernameEntryBox.Text != string.Empty)
		{	
			GenericCore.Instance.ParseInitialPromptInfo(UsernameEntryBox.Text, ServerAddressEntryBox.Text, PortNumberAddressEntryBox.Text.ToInt());
			Error err = GenericCore.Instance.JoinGame();
			if(err != Error.Ok)
				GD.PushWarning("Join failed. Server might not exist or is busy");
			// At this point ClientConnectionOk should be emitted
		}
		else
		{
			GD.PushWarning("Must enter a user name!");
		}
	}

	private void OnHostButtonPressed()
	{
		GenericCore.Instance.ParseInitialPromptInfo(UsernameEntryBox.Text, ServerAddressEntryBox.Text, PortNumberAddressEntryBox.Text.ToInt());
		Error err = GenericCore.Instance.CreateGame();
		if(err != Error.Ok)
			GD.PushWarning("Tried to create server but failed");

		OnConnectionSuccessful();
	}

}
