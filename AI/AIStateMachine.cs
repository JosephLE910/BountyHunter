using System;
using System.Collections.Generic;
using BountyHunter.AI.States;

namespace BountyHunter.AI
{
    /// <summary>
    /// 简单泛型 FSM，通过类型转换状态
    /// </summary>
    public class AIStateMachine
    {
        private readonly Dictionary<Type, AIState> _states = new();
        public AIState CurrentState { get; private set; }

        public void RegisterState(AIState state)
        {
            _states[state.GetType()] = state;
        }

        public void Start<T>() where T : AIState
        {
            CurrentState = _states[typeof(T)];
            CurrentState.OnEnter();
        }

        public void TransitionTo<T>() where T : AIState
        {
            if (!_states.TryGetValue(typeof(T), out var next)) return;
            CurrentState?.OnExit();
            CurrentState = next;
            CurrentState.OnEnter();
        }

        public void Update(float deltaTime)
        {
            CurrentState?.OnUpdate(deltaTime);
        }
    }
}
