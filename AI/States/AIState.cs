namespace BountyHunter.AI.States
{
    /// <summary>
    /// FSM 状态基类
    /// </summary>
    public abstract class AIState
    {
        protected KartAIController AI;

        protected AIState(KartAIController ai) => AI = ai;

        public abstract void OnEnter();
        public abstract void OnUpdate(float deltaTime);
        public abstract void OnExit();
    }
}
