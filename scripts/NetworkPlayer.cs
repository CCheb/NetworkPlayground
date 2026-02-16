using Godot;
using System;

public partial class NetworkPlayer : CharacterBody3D
{
	[Export] private NetID myNetId;
	[Export] private Node3D cameraController;
	[Export] private MeshInstance3D visor;
	[Export] private PackedScene cameraScene;
	[Export] private Label3D playerNameTag;

	private const int SERVER = 1;
	private float jumpVelocity = 4.5f;
	private float speed = 5.0f;

	private Vector3 mouseRotation = Vector3.Zero;
	private Vector3 cameraRotation = Vector3.Zero;
	private Vector3 playerRotation = Vector3.Zero;

	private bool mouseInput;
	// Simple struct to hold our player input data. In this case movement, rotation, and jump
	private struct PlayerInput
	{
		public Vector2 move;
		public float rotationInput;
		public float tiltInput;
		public bool jump;
	}

	PlayerInput input = new PlayerInput();

    public override void _EnterTree()
    {
        base._EnterTree();
		SetPhysicsProcess(false);
		SetProcessUnhandledInput(false);
    }

    public override void _ExitTree()
    {
        base._ExitTree();
		myNetId.NetIdIsReady -= OnNetIdReady;
    }
    public override void _Ready()
    {	
        base._Ready();
		myNetId.NetIdIsReady += OnNetIdReady;
		
    }

	private void OnNetIdReady()
	{
		if(myNetId.IsLocal)
		{
			Camera3D playerCamera = cameraScene.Instantiate<Camera3D>();
			cameraController.AddChild(playerCamera);
			Input.MouseMode = Input.MouseModeEnum.Captured;
			SetPhysicsProcess(true);
			SetProcessUnhandledInput(true);
		}
		else
		{
			visor.Visible = true;
		}

		playerNameTag.Text = GenericCore.Instance._connectedPeers[myNetId.OwnerId]["UserName"];
		GD.Print(myNetId.OwnerId);
	}

	 public override void _Input(InputEvent @event)
    {
        base._Input(@event);
		if(@event.IsActionPressed("pause") && myNetId.IsLocal)
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);
		// Determine if the mouse is captured and is moving. Only the local player should be able to set this
		mouseInput = (@event is InputEventMouseMotion) && (Input.MouseMode == Input.MouseModeEnum.Captured);

		// Screen space to world space
		if (mouseInput && myNetId.IsLocal)
		{
			// Grab the MouseMotionEvent 
			InputEventMouseMotion motionEvent = (InputEventMouseMotion)@event;
			// Converting mouse movement into radians that we will pass over to the player and camera
			// In this case we are grabbing the total ammount the mouse moved since the last frame
			// This is cornverting to radians per pixel (MouseSensitivity). From here we decide to use radians or degrees

			// How much has the mouse moved in the last frame. Convert that into rad / pixel

			// Its important that we negate these values because turning right in screen space is + but in world space will
			// be negative. Thats why we take the screen space rotation and negate it over to world space rotation 
			input.rotationInput = -motionEvent.Relative.X * 0.1f;
			input.tiltInput = -motionEvent.Relative.Y * 0.1f;

			GD.Print($"Peer {myNetId.OwnerId}:{Multiplayer.GetUniqueId()} ({input.rotationInput}, {input.tiltInput})");
		}
    }
	public override void _PhysicsProcess(double delta)
	{
		if(myNetId.IsLocal)
		{
			// Simply grab the input and pass it forward for calculation
			Vector2 move = Input.GetVector("move_left", "move_right", "move_up", "move_down");
    		bool jump = Input.IsActionPressed("jump");

			RpcId(SERVER, MethodName.SendInput, move, jump, input.rotationInput, input.tiltInput);

			input.rotationInput = 0.0f;
			input.tiltInput = 0.0f;
		}	

		
		if(!GenericCore.Instance.IsServer)
			return;

		ApplyInput(delta);
		
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	private void SendInput(Vector2 move, bool jump, float rotationInput, float tiltInput)
	{
		if(!GenericCore.Instance.IsServer)
			return;

		input.move = move;
		input.jump = jump;
		input.rotationInput = rotationInput;
		input.tiltInput = tiltInput;

	}

	private void ApplyInput(double delta)
	{
		// The rest is default movement code
		Vector3 velocity = Velocity;

		// Add the gravity.
		if (!IsOnFloor())
		{
			velocity += GetGravity() * (float)delta;
		}
		// Handle Jump.
		if (input.jump && IsOnFloor())
		{
			velocity.Y = jumpVelocity;
		}

		Vector3 direction = (Transform.Basis * new Vector3(input.move.X, 0, input.move.Y)).Normalized();

		velocity.X = direction.X * speed;
		velocity.Z = direction.Z * speed;

		Velocity = velocity;
		// MoveAndSlide is the function that actually calculates/simulates the movement 
		MoveAndSlide();

		// Horizontal rotation. The Vertical rotation is strictly for the camera and we send those values over to it
		// need a variable to persist throughout rotations
		mouseRotation.Y += input.rotationInput * (float)delta;

		// Form vectors to be applied to the player and camera rotations respectively
		// If we look horizontally we want the player to rotate which will rotate the camera with it since its a child
		playerRotation = new Vector3(0.0f, mouseRotation.Y, 0.0f);

		// Player rotation, want horizontal rotation
		Basis = Basis.FromEuler(playerRotation);


		// Its important that mouseRotation collects the rotation inputs instead of being set by them.
		mouseRotation.X += input.tiltInput * (float)delta;
		mouseRotation.X = Mathf.Clamp(mouseRotation.X, Mathf.DegToRad(-90.0f), Mathf.DegToRad(90.0f));
		cameraRotation = new Vector3(mouseRotation.X, 0.0f, 0.0f);

		cameraController.Rotation = cameraRotation;
		visor.Rotation = cameraRotation;
	}
}
