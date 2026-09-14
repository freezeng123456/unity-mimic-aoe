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
        public bool Step()
        {
            if(World.Paused||World.Winner>=0)return false;
            foreach(var controller in controllers)controller.Tick(StepSeconds);
            World.Tick(StepSeconds);StepCount++;return true;
        }
        // Input is elapsed simulation time (the caller may scale autoplay).
        // Paused time is never banked. Bounded catch-up slows the game under overload.
        public int AdvanceFrame(float simulationSeconds)
        {
            if(float.IsNaN(simulationSeconds)||float.IsInfinity(simulationSeconds)||simulationSeconds<0)throw new ArgumentOutOfRangeException(nameof(simulationSeconds));
            LastFrameMs=0;CatchingUp=false;if(World.Paused||World.Winner>=0)return 0;
            long started=Stopwatch.GetTimestamp();accumulator+=simulationSeconds;int steps=0;
            while(accumulator>=StepSeconds&&steps<MaxStepsPerFrame&&Step()){accumulator-=StepSeconds;steps++;}
            if(accumulator>2){accumulator=2;CatchingUp=true;}
            if(World.Winner>=0)accumulator=0;
            LastFrameMs=(float)((Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency);return steps;
        }
    }
}
