// credit: original code from
// https://github.com/firebelley/GodotUtilities/
namespace GDHelpers;

using System.Collections.Generic;
using Godot;

public partial class DelegateStateMachine : RefCounted
{
    public delegate void StateChanged();

    public delegate void State(double delta = 0);

    private State currentState;
    private StateChanged onChange;

    private readonly Dictionary<State, StateFlows> states = new();

    public void SetOnChange(StateChanged value)
    {
        onChange = value;
    }

    public void AddStates(State normal, State enterState = null, State leaveState = null)
    {
        var stateFlows = new StateFlows(normal, enterState, leaveState);
        states[normal] = stateFlows;
    }

    public void ChangeState(State toStateDelegate)
    {
        states.TryGetValue(toStateDelegate, out var stateDelegates);
        Callable.From(() => SetState(stateDelegates)).CallDeferred();
    }

    public void SetInitialState(State stateDelegate)
    {
        states.TryGetValue(stateDelegate, out var stateFlows);
        SetState(stateFlows);
    }

    public State GetCurrentState() => currentState;

    public string GetCurrentStateName() => currentState?.Method.Name ?? "none";

    public void Update(double delta) => currentState?.Invoke(delta);

    private void SetState(StateFlows stateFlows)
    {
        onChange?.Invoke();
        if (currentState != null)
        {
            states.TryGetValue(currentState, out var currentStateDelegates);
            currentStateDelegates?.LeaveState?.Invoke();
        }
        currentState = stateFlows.Normal;
        stateFlows?.EnterState?.Invoke();
    }

    private class StateFlows
    {
        public State Normal { get; private set; }
        public State EnterState { get; private set; }
        public State LeaveState { get; private set; }

        public StateFlows(State normal, State enterState = null, State leaveState = null)
        {
            Normal = normal;
            EnterState = enterState;
            LeaveState = leaveState;
        }
    }
}
