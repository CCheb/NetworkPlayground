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

	private Vector3 totalMouseRotation = Vector3.Zero;
	private Vector3 verticalRotation = Vector3.Zero;
	private Vector3 horizontalRotation = Vector3.Zero;

	// Simple struct to hold our player input data. In this case movement, rotation, and jump
	private struct PlayerInput
	{
		public Vector2 move;
		public float yawDelta;
		public float pitchDelta;
		public bool jump;
	}
	PlayerInput input = new PlayerInput();

	public override void _Input(InputEvent @event)
    {
        base._Input(@event);
		if(@event.IsActionPressed("pause") && myNetId.IsLocal)
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
    }

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
			SetAsLocalPlayer();
		else
			SetAsNonLocalPlayer();
	
		SetPlayerNameTag();
	}

	private void SetAsLocalPlayer()
	{
		Camera3D playerCamera = cameraScene.Instantiate<Camera3D>();
		cameraController.AddChild(playerCamera);
		Input.MouseMode = Input.MouseModeEnum.Captured;
		SetPhysicsProcess(true);
		SetProcessUnhandledInput(true);
	}

	private void SetAsNonLocalPlayer()
	{
		visor.Visible = true;
	}

	private void SetPlayerNameTag()
	{
		playerNameTag.Text = GenericCore.Instance._connectedPeers[myNetId.OwnerId]["UserName"];
	}
	
    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);

		if (CurrentInputIsMouse(@event) && myNetId.IsLocal)
		{
			CalculateRotationDeltas((InputEventMouseMotion)@event);
		}
    }

	private bool CurrentInputIsMouse(InputEvent @event)
	{
		return (@event is InputEventMouseMotion) && (Input.MouseMode == Input.MouseModeEnum.Captured);
	}

	private void CalculateRotationDeltas(InputEventMouseMotion mouseMotion)
	{
		// Screen space to world space (2D -> 3D)!!!

		// Its important that we negate these values because turning right in screen space is + but in world space will
		// be negative. Thats why we take the screen space rotation and negate it over to world space rotation 
		input.yawDelta = -mouseMotion.Relative.X * 0.1f;
		input.pitchDelta = -mouseMotion.Relative.Y * 0.1f;
	}

	public override void _PhysicsProcess(double delta)
	{
		if(myNetId.IsLocal)
		{
			GenerateAndSendMovementInput();
			ResetRotationDeltas();
		}	

		if(GenericCore.Instance.IsServer)
			ApplyInput(delta);
	}

	private void GenerateAndSendMovementInput()
	{
		// Simply grab the input and pass it forward for calculation
		Vector2 move = Input.GetVector("move_left", "move_right", "move_up", "move_down");
    	bool jump = Input.IsActionPressed("jump");
		RpcId(SERVER, MethodName.SendInput, move, jump, input.yawDelta, input.pitchDelta);
	}

	private void ResetRotationDeltas()
	{
		input.yawDelta = 0.0f;
		input.pitchDelta = 0.0f;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	private void SendInput(Vector2 move, bool jump, float yawDelta, float pitchDelta)
	{
		if(!GenericCore.Instance.IsServer)
			return;

		// From client to client representation on the server side
		input.move = move;
		input.jump = jump;
		input.yawDelta = yawDelta;
		input.pitchDelta = pitchDelta;
	}

	private void ApplyInput(double delta)
	{
		
		CalculateMovement(delta);
		CalculateRotations(delta);
	}

	private void CalculateMovement(double delta)
	{
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

		// Calculates/simulates the movement 
		MoveAndSlide();
	}

	private void CalculateRotations(double delta)
	{
		RotatePlayer(delta);
		RotateCamera(delta);
	}

	private void RotatePlayer(double delta)
	{
		// Player rotation, want horizontal rotation
		totalMouseRotation.Y += input.yawDelta * (float)delta; // Parse total mouse rotation.
		horizontalRotation = new Vector3(0.0f, totalMouseRotation.Y, 0.0f);
		Basis = Basis.FromEuler(horizontalRotation);
	}

	private void RotateCamera(double delta)
	{
		// Camera rotation, want vertical rotation
		totalMouseRotation.X += input.pitchDelta * (float)delta; // Parse total mouse rotation
		totalMouseRotation.X = Mathf.Clamp(totalMouseRotation.X, Mathf.DegToRad(-90.0f), Mathf.DegToRad(90.0f));
		verticalRotation = new Vector3(totalMouseRotation.X, 0.0f, 0.0f);

		cameraController.Rotation = verticalRotation;
		visor.Rotation = verticalRotation;	// In the case the current NetworkPlayer does not have a camera
	}
	
}
