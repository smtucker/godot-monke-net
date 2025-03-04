using GameDemo;
using Godot;
using MonkeNet.Client;
using MonkeNet.Serializer;

public partial class PlayerInputProducer : InputProducerComponent
{
    [Export] private FirstPersonCameraController _cameraController;

    public override void _Ready()
    {
        base._Ready(); // Important! This will notify the Client Manager of this input producer!
    }

    public override IPackableElement GenerateCurrentInput()
    {
        return new CharacterInputMessage
        {
            Keys = GetCurrentPressedKeys(),
            CameraYaw = _cameraController.GetLateralRotationAngle()
        };
    }

    private static byte GetCurrentPressedKeys()
    {
        byte keys = 0;
        if (Input.IsActionPressed("right")) keys |= (byte)InputFlags.Right;
        if (Input.IsActionPressed("left")) keys |= (byte)InputFlags.Left;
        if (Input.IsActionPressed("forward")) keys |= (byte)InputFlags.Forward;
        if (Input.IsActionPressed("backward")) keys |= (byte)InputFlags.Backward;
        if (Input.IsActionPressed("space")) keys |= (byte)InputFlags.Space;
        if (Input.IsActionPressed("shift")) keys |= (byte)InputFlags.Shift;
        return keys;
    }
}
