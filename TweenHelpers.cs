using System;
using System.Threading.Tasks;
using Godot;

namespace GDHelpers
{
    public static class TweenHelpers
    {
        public static async Task AwaitFinished(this Tween tween)
        {
            await tween.ToSignal(tween, Tween.SignalName.Finished);
        }

        public static async void OnFinished(this Tween tween, Action action)
        {
            await tween.ToSignal(tween, Tween.SignalName.Finished);
            action();
        }

        public static async Task AwaitTimeout(this SceneTreeTimer timer)
        {
            await timer.ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
        }
    }
}
