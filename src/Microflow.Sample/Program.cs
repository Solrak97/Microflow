using Microflow.Core;

var state = new AgentState("walk in a square");
var graph = new FlowGraph<AgentState>("plan")
    .AddNode("plan", PlanAsync)
    .AddNode("execute", ExecuteAsync)
    .AddNode("done", DoneAsync)
    .AddTransition("plan", "execute", (s, r) => r.Outcome == NodeOutcome.Success)
    .AddTransition("execute", "execute", (s, r) => r.Outcome == NodeOutcome.Continue)
    .AddTransition("execute", "done", (s, r) => r.Outcome == NodeOutcome.Success)
    .AddTransition("done", "done", (s, r) => r.Outcome == NodeOutcome.Halt);

var runner = new FlowRunner<AgentState>(graph);
var result = await runner.RunAsync(state, maxSteps: 32);

Console.WriteLine($"Halted: {result.Halted}, Steps: {result.Steps}, FinalNode: {result.FinalNodeId}");
foreach (var e in result.Trace)
    Console.WriteLine($"[{e.Step}] {e.NodeId} -> {e.Outcome} => {e.NextNodeId}");

if (!string.IsNullOrEmpty(result.Error))
    Console.WriteLine("Error: " + result.Error);

static Task<NodeResult> PlanAsync(AgentState state, NodeContext context, CancellationToken ct)
{
    // In a real agent this would call an LLM planner.
    if (state.Actions.Count == 0)
    {
        state.Actions.Enqueue("/walk forward 2");
        state.Actions.Enqueue("/turn 90");
        state.Actions.Enqueue("/walk forward 2");
        state.Actions.Enqueue("/turn 90");
        state.Actions.Enqueue("/walk forward 2");
        state.Actions.Enqueue("/turn 90");
        state.Actions.Enqueue("/walk forward 2");
        state.Actions.Enqueue("/turn 90");
    }

    return Task.FromResult(NodeResult.Success($"Planned {state.Actions.Count} actions."));
}

static Task<NodeResult> ExecuteAsync(AgentState state, NodeContext context, CancellationToken ct)
{
    if (state.Actions.Count == 0)
        return Task.FromResult(NodeResult.Success("Plan complete."));

    var next = state.Actions.Dequeue();
    state.ExecutionLog.Add(next);
    return Task.FromResult(state.Actions.Count == 0
        ? NodeResult.Success($"Executed final action: {next}")
        : NodeResult.Continue($"Executed: {next}"));
}

static Task<NodeResult> DoneAsync(AgentState state, NodeContext context, CancellationToken ct)
{
    Console.WriteLine("Execution log:");
    foreach (var line in state.ExecutionLog) Console.WriteLine(" - " + line);
    return Task.FromResult(NodeResult.Halt("done"));
}

sealed class AgentState
{
    public AgentState(string goal) => Goal = goal;

    public string Goal { get; }
    public Queue<string> Actions { get; } = new Queue<string>();
    public List<string> ExecutionLog { get; } = new List<string>();
}
