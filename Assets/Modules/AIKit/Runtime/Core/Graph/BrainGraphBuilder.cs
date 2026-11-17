using System.Collections.Generic;

namespace AIKit
{
    /// <summary>
    /// Builds a behavior graph in code — the same picture the canvas draws, and nothing more: sensors, one Brain, the
    /// decisions wired to it, each decision's chain of blocks.
    ///
    /// var g = new BrainGraphBuilder();                                   // the Brain (a CombatBrainTask)
    /// g.Sensor(new DetectionTask(new RadiusSensorTask { Radius = 8f }) { Tag = "Player", StoreIn = "Target" });
    /// g.BrainTask.Target = "Target";
    /// g.Variable("Target", VariableType.Transform);
    /// AgentState fight = g.Decision(new FightReasoner(), 1);
    /// g.Chain(fight, new AttackTask());                                  // Do → Strike
    /// AgentState patrol = g.Decision(new PatrolReasoner(), 2);
    /// g.Chain(patrol, new PatrolAreaTask { Area = "Zone" });
    /// agent.SetGraph(g.Build());
    /// </summary>
    public sealed class BrainGraphBuilder
    {
        private readonly List<VariableDeclaration> _variables = new();
        private int _nextPriority = 1;

        /// <summary>A graph with this Brain (a plain Brain when none is given — a Squad Brain for a squad's command graph).</summary>
        public BrainGraphBuilder(CombatBrainTask brain = null)
        {
            Root = new AgentState("Root");
            BrainTask = brain ?? new CombatBrainTask();
            Brain = Block(BrainTask);
        }

        /// <summary>The graph's root: the sensors are its tasks, the nodes its children.</summary>
        public AgentState Root { get; }

        /// <summary>The Brain's node.</summary>
        public AgentState Brain { get; }

        /// <summary>The Brain itself (its Target pin: the variable a sensor writes).</summary>
        public CombatBrainTask BrainTask { get; }

        /// <summary>A sensor of the graph: it works beside whatever runs and writes its variable.</summary>
        public BrainGraphBuilder Sensor(SensorTask sensor)
        {
            if (sensor != null)
            {
                sensor.IsBackground = true;
                Root.AddTask(sensor);
            }

            return this;
        }

        /// <summary>A variable the graph declares (an agent's blackboard gets it on start).</summary>
        public BrainGraphBuilder Variable(string name, VariableType type)
        {
            _variables.Add(new VariableDeclaration(name, type));
            return this;
        }

        /// <summary>A variable the graph declares, with what it holds at the start (a number, a flag).</summary>
        public BrainGraphBuilder Variable(VariableDeclaration declaration)
        {
            if (declaration != null)
                _variables.Add(declaration);
            return this;
        }

        /// <summary>A node with one block; named by the block's type unless a name is given (tests tell nodes apart by it).</summary>
        public AgentState Block(AgentTask task, string name = null)
        {
            var node = new AgentState(name ?? NodeTitles.Of(task));
            node.AddTask(task);
            Root.AddChild(node);
            return node;
        }

        /// <summary>A decision wired to the Brain, with its priority (1 — the most important).</summary>
        public AgentState Decision(ReasonerTask decision, int priority, string name = null)
        {
            decision.Priority = priority;
            _nextPriority = System.Math.Max(_nextPriority, priority + 1);
            AgentState node = Block(decision, name);
            Brain.AddTransition(new Transition { Target = node, Trigger = TransitionTrigger.Always });
            return node;
        }

        /// <summary>A decision wired to the Brain, less important than every one wired before it.</summary>
        public AgentState Decision(ReasonerTask decision) => Decision(decision, _nextPriority);

        /// <summary>A decision that always holds (Daily Routine), least important of those wired so far: "the rest of the time".</summary>
        public AgentState Always(string name = null) => Decision(new DailyRoutineReasoner(), _nextPriority, name);

        /// <summary>The decision's Do: the first block of its chain.</summary>
        public BrainGraphBuilder Do(AgentState decision, AgentState first) =>
            Arrow(decision, first, TransitionTrigger.Always);

        /// <summary>From the block's "it worked" output (Done) to the next block.</summary>
        public BrainGraphBuilder Then(AgentState from, AgentState to) => Arrow(from, to, TransitionTrigger.TasksSucceeded);

        /// <summary>From the block's "it failed" output to the next block.</summary>
        public BrainGraphBuilder OnFail(AgentState from, AgentState to) => Arrow(from, to, TransitionTrigger.TasksFailed);

        /// <summary>From the block's end, whatever the outcome, to the next block.</summary>
        public BrainGraphBuilder OnEnd(AgentState from, AgentState to) => Arrow(from, to, TransitionTrigger.TasksFinished);

        /// <summary>From a numbered output of a block that picks one (Random) to the next block.</summary>
        public BrainGraphBuilder Out(AgentState from, int output, AgentState to)
        {
            from.AddTransition(new Transition { Target = to, Trigger = TransitionTrigger.TasksSucceeded, Output = output });
            return this;
        }

        /// <summary>The block's On Tick pin: a chain of instant blocks run once per think while the block runs.</summary>
        public BrainGraphBuilder OnTick(AgentState from, AgentState to) => Arrow(from, to, TransitionTrigger.Tick);

        /// <summary>
        /// The decision's chain: Do → the first block, each block's Done → the next. Returns the blocks' nodes in order.
        /// </summary>
        public AgentState[] Chain(AgentState decision, params AgentTask[] blocks)
        {
            var nodes = new AgentState[blocks.Length];
            for (int i = 0; i < blocks.Length; i++)
            {
                nodes[i] = Block(blocks[i]);
                if (i == 0)
                    Do(decision, nodes[0]);
                else
                    Then(nodes[i - 1], nodes[i]);
            }

            return nodes;
        }

        public BrainGraphBuilder Arrow(AgentState from, AgentState to, TransitionTrigger trigger)
        {
            from.AddTransition(new Transition { Target = to, Trigger = trigger });
            return this;
        }

        public BehaviorGraph Build()
        {
            var graph = new BehaviorGraph(Root);
            graph.Variables.AddRange(_variables);
            graph.AssignIds();
            return graph;
        }
    }
}
