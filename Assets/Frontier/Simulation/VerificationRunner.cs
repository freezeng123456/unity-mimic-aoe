using System;
using System.Collections.Generic;

namespace Frontier
{
    public static class VerificationRunner
    {
        public sealed class DuelReport { public bool finished; public int winner; public float time; public readonly List<string> events=new(); }
        /// Runs two normal rule clients. World creation is injected so this utility never invents
        /// a test economy or bypasses the project's catalog/bootstrap path.
        public static DuelReport RunDuel(Func<int, World> createWorld, float limit=3600f, float step=.1f)
        {
            if (createWorld==null) throw new ArgumentNullException(nameof(createWorld));
            var w=createWorld(314159); var a=new SkirmishAI(w,0); var b=new SkirmishAI(w,1); var r=new DuelReport();
            for(float t=0;t<limit && w.Winner<0;t+=step) { a.Tick(step); b.Tick(step); w.Tick(step); }
            r.time=w.Time; r.winner=w.Winner; r.finished=w.Winner>=0; foreach(var e in w.Events) r.events.Add(e.kind+":"+e.message); return r;
        }
    }
}
