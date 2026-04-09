using System;

namespace Microflow.Core
{
    /// <summary>
    /// Prewired flow topology for embodied agents:
    /// plan -> execute -> observe -> (plan|execute|done|recover)
    /// recover -> plan|done
    /// done -> halt
    /// </summary>
    public static class EmbodiedAgentPattern
    {
        public const string PlanNode = "plan";
        public const string ExecuteNode = "execute";
        public const string ObserveNode = "observe";
        public const string RecoverNode = "recover";
        public const string DoneNode = "done";

        public static FlowGraph<TState> Build<TState>(
            NodeHandler<TState> plan,
            NodeHandler<TState> execute,
            NodeHandler<TState> observe,
            NodeHandler<TState> recover,
            NodeHandler<TState> done)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            if (observe == null) throw new ArgumentNullException(nameof(observe));
            if (recover == null) throw new ArgumentNullException(nameof(recover));
            if (done == null) throw new ArgumentNullException(nameof(done));

            return new FlowGraph<TState>(PlanNode)
                .AddNode(PlanNode, plan)
                .AddNode(ExecuteNode, execute)
                .AddNode(ObserveNode, observe)
                .AddNode(RecoverNode, recover)
                .AddNode(DoneNode, done)

                .AddTransition(PlanNode, ExecuteNode, (_, r) => r.Outcome == NodeOutcome.Success || r.Outcome == NodeOutcome.Continue)
                .AddTransition(PlanNode, RecoverNode, (_, r) => r.Outcome == NodeOutcome.Failed || r.Outcome == NodeOutcome.Retry)

                .AddTransition(ExecuteNode, ObserveNode, (_, r) => r.Outcome == NodeOutcome.Success || r.Outcome == NodeOutcome.Continue)
                .AddTransition(ExecuteNode, RecoverNode, (_, r) => r.Outcome == NodeOutcome.Failed || r.Outcome == NodeOutcome.Retry)
                .AddTransition(ExecuteNode, DoneNode, (_, r) => r.Outcome == NodeOutcome.Halt)

                .AddTransition(ObserveNode, PlanNode, (_, r) => r.Outcome == NodeOutcome.Continue)
                .AddTransition(ObserveNode, ExecuteNode, (_, r) => r.Outcome == NodeOutcome.Success)
                .AddTransition(ObserveNode, DoneNode, (_, r) => r.Outcome == NodeOutcome.Halt)
                .AddTransition(ObserveNode, RecoverNode, (_, r) => r.Outcome == NodeOutcome.Failed || r.Outcome == NodeOutcome.Retry)

                .AddTransition(RecoverNode, PlanNode, (_, r) => r.Outcome == NodeOutcome.Success || r.Outcome == NodeOutcome.Continue)
                .AddTransition(RecoverNode, DoneNode, (_, r) => r.Outcome == NodeOutcome.Halt)

                .AddTransition(DoneNode, DoneNode, (_, r) => r.Outcome == NodeOutcome.Halt);
        }
    }
}
