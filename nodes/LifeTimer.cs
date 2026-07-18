namespace GDHelpers;

using System.Threading.Tasks;
using Godot;

[GlobalClass]
public partial class LifeTimer : Timer
{
    [Export]
    private bool flashBefore = false;

    [Export]
    private float StartFlashTime = .5f;
    private bool isFlashing;

    public override void _Ready()
    {
        if (Autostart && GetParent() is GpuParticles3D particles3D)
        {
            particles3D.Emitting = true;
        }
        if (!flashBefore)
        {
            Timeout += OnTimeout;
        }
    }

    public override void _Process(double delta)
    {
        if (!flashBefore || isFlashing)
        {
            return;
        }
        if (TimeLeft <= StartFlashTime)
        {
            var _ = FlashAndRemove3DObject((Node3D)GetParent(), 10, .1f);
        }
    }

    private void OnTimeout()
    {
        GetParent()?.QueueFree();
    }

    /// <summary>
    /// Makes a 3D object flash by toggling visibility before removing it.
    /// </summary>
    /// <param name="object3D">The 3D object to flash</param>
    /// <param name="flashCount">Number of times to flash</param>
    /// <param name="flashDurationSeconds">Duration of each visibility state in seconds</param>
    /// <returns>Task that completes when the flashing and removal is done</returns>
    public async Task FlashAndRemove3DObject(
        Node3D object3D,
        int flashCount = 5,
        float flashDurationSeconds = 0.2f
    )
    {
        isFlashing = true;
        if (object3D == null)
            return;

        // Store original visibility
        bool originalVisibility = object3D.Visible;

        // Flash the object
        for (int i = 0; i < flashCount; i++)
        {
            // Toggle visibility
            object3D.Visible = !object3D.Visible;

            // Wait for specified duration
            await ToSignal(GetTree().CreateTimer(flashDurationSeconds), "timeout");
        }

        // Ensure the object is visible for the last flash if needed
        if (!object3D.Visible)
        {
            object3D.Visible = true;
            await ToSignal(GetTree().CreateTimer(flashDurationSeconds), "timeout");
        }

        // Remove the object
        object3D.QueueFree();
    }
}
