namespace GDHelpers;

using Godot;

[GlobalClass]
public partial class CooldownTimer : Node
{
    [Export]
    public float CooldownTime;

    public float Value;
    public float MaxValue;

    private Tween tween;

    public void StopCooldown()
    {
        if (tween != null && tween.IsRunning())
        {
            tween.Stop();
        }
    }

    public void StartCooldown(float val, float time = -1, float delay = 0.5f)
    {
        Value = val;
        if (time == -1)
        {
            time = CooldownTime;
        }

        tween = CreateTween();

        tween
            .TweenMethod(Callable.From((float v) => Value = v), Value, MaxValue, time)
            .SetDelay(delay);
    }
}
