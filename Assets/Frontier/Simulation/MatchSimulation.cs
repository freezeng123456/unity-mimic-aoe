using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Frontier
{
    // The single scheduler for native play and diagnostics. AI commands are applied
    // in descending player-slot order, then the world advances one fixed step.
    // This preserves the native 0.2.3 dispatch order; it is not a networking guarantee.
    public sealed class MatchSimulation
    {
        public const float StepSeconds=.1f;
        public const int MaxStepsPerFrame=10;
        public World World { get; }
        public AiDifficultyProfile Difficulty { get; }
        public int StepCount { get; private set; }
        public float LastFrameMs { get; private set; }
        public bool CatchingUp { get; private set; }
        public event Action StepCompleted;
        readonly List<SkirmishAI> controllers=new List<SkirmishAI>();
        float accumulator;
        public MatchSimulation(World world,params int[] automatedPlayers):this(world,automatedPlayers,"standard"){}
        public MatchSimulation(World world,int[] automatedPlayers,string difficultyId)
        {
            World=world??throw new ArgumentNullException(nameof(world));
            Difficulty=AiDifficultyProfile.From(world.Rules,difficultyId);
            var controlled=new HashSet<int>(automatedPlayers??Array.Empty<int>());
            foreach(int id in controlled)if(id<0||id>=world.Players.Length)throw new ArgumentOutOfRangeException(nameof(automatedPlayers));
            for(int id=world.Players.Length-1;id>=0;id--)if(controlled.Contains(id))controllers.Add(new SkirmishAI(world,id,Difficulty));
        }
        public MatchSimulationSnapshot CaptureState()
        {
            var saved=new AiSnapshot[controllers.Count];for(int i=0;i<saved.Length;i++)saved[i]=controllers[i].CaptureState();
            return new MatchSimulationSnapshot{difficultyId=Difficulty.Id,stepCount=StepCount,accumulator=accumulator,controllers=saved};
        }
        public static MatchSimulation Restore(World world,MatchSimulationSnapshot state)
        {
            AiSaveValidation.Require(world!=null&&state!=null,"missing simulation");
            AiSaveValidation.Require(state.stepCount>=0&&AiSaveValidation.Clock(state.accumulator)&&state.accumulator<=2,"simulation step or accumulator");
            AiSaveValidation.Require(world.Rules.Difficulty(state.difficultyId)!=null,"unknown difficulty");
            AiSaveValidation.Require(state.controllers!=null&&state.controllers.Length<=world.Players.Length,"controller collection");
            var players=new HashSet<int>();
            foreach(var saved in state.controllers)AiSaveValidation.Require(saved!=null&&saved.player>=0&&saved.player<world.Players.Length&&players.Add(saved.player),"duplicate or invalid controller player");
            var result=new MatchSimulation(world,Array.Empty<int>(),state.difficultyId){StepCount=state.stepCount,accumulator=state.accumulator};
            // Array order in an external save cannot change the native descending dispatch contract.
            for(int id=world.Players.Length-1;id>=0;id--)
                foreach(var saved in state.controllers)if(saved.player==id)result.controllers.Add(SkirmishAI.Restore(world,saved,result.Difficulty));
            return result;
        }
        public bool Step()
        {
            if(World.Paused||World.IsMatchEnded)return false;
            foreach(var controller in controllers)controller.Tick(StepSeconds);
            World.Tick(StepSeconds);StepCount++;StepCompleted?.Invoke();return true;
        }
        // Input is elapsed simulation time (the caller may scale autoplay).
        // Paused time is never banked. Bounded catch-up slows the game under overload.
        public int AdvanceFrame(float simulationSeconds)
        {
            if(float.IsNaN(simulationSeconds)||float.IsInfinity(simulationSeconds)||simulationSeconds<0)throw new ArgumentOutOfRangeException(nameof(simulationSeconds));
            LastFrameMs=0;CatchingUp=false;if(World.Paused||World.IsMatchEnded)return 0;
            long started=Stopwatch.GetTimestamp();accumulator+=simulationSeconds;int steps=0;
            while(accumulator>=StepSeconds&&steps<MaxStepsPerFrame&&Step()){accumulator-=StepSeconds;steps++;}
            if(accumulator>2){accumulator=2;CatchingUp=true;}
            if(World.IsMatchEnded)accumulator=0;
            LastFrameMs=(float)((Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency);return steps;
        }
    }
}
