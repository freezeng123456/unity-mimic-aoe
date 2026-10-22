using System;
using System.Collections.Generic;

namespace Frontier
{
    // Pure local candidate chooser. These are predicted route-intent velocities,
    // not measured velocities or reciprocal motion commitments. The caller owns
    // every actual static/lane/body sweep and may reject any candidate.
    internal struct SteeringVector084
    {
        internal double x,y;
        internal SteeringVector084(double x,double y){this.x=x;this.y=y;}
        internal double LengthSquared=>x*x+y*y;
        internal double Length=>Math.Sqrt(LengthSquared);
        internal SteeringVector084 Normalized=>LengthSquared>1e-20?this/Length:new SteeringVector084();
        internal static double Dot(SteeringVector084 a,SteeringVector084 b)=>a.x*b.x+a.y*b.y;
        public static SteeringVector084 operator +(SteeringVector084 a,SteeringVector084 b)=>new SteeringVector084(a.x+b.x,a.y+b.y);
        public static SteeringVector084 operator -(SteeringVector084 a,SteeringVector084 b)=>new SteeringVector084(a.x-b.x,a.y-b.y);
        public static SteeringVector084 operator -(SteeringVector084 a)=>new SteeringVector084(-a.x,-a.y);
        public static SteeringVector084 operator *(SteeringVector084 a,double b)=>new SteeringVector084(a.x*b,a.y*b);
        public static SteeringVector084 operator /(SteeringVector084 a,double b)=>new SteeringVector084(a.x/b,a.y/b);
    }
    internal struct SteeringNeighbor084
    {
        internal int id;
        internal SteeringVector084 position,intent;
        internal double radius,distanceSquared;
        internal int priority;
        internal bool moving;
    }
    internal sealed class LocalSteering084
    {
        internal const double Horizon=.8,Comfort=.1;
        internal const int MaxNeighbors=16,MaxCandidates=96;
        private const int DirectionNeighbors=6;
        private struct ScoredVelocity {internal SteeringVector084 velocity;internal double score;internal int index;}
        private readonly List<SteeringVector084> candidates=new List<SteeringVector084>(192);
        private readonly List<ScoredVelocity> scored=new List<ScoredVelocity>(MaxCandidates);
        internal int CandidateCount,AdmissibleCount,GateChecks;
        internal bool AnticipatoryConflict,OppositeFlow;
        private static double Clamp(double value,double min,double max)=>Math.Max(min,Math.Min(max,value));
        private void Add(SteeringVector084 velocity,double speed)
        {
            if(velocity.LengthSquared>speed*speed)velocity=velocity.Normalized*speed;
            if(double.IsNaN(velocity.x)||double.IsNaN(velocity.y)||double.IsInfinity(velocity.x)||double.IsInfinity(velocity.y))return;
            // Stable generation order is the final tie-break. A small duplicate
            // threshold bounds repeated collinear tangents without merging speeds.
            foreach(var previous in candidates)if((velocity-previous).LengthSquared<1e-10)return;
            if(candidates.Count>=MaxCandidates)return;
            candidates.Add(velocity);
        }
        private void Direction(SteeringVector084 direction,double speed)
        {
            if(direction.LengthSquared<1e-16)return;
            direction=direction.Normalized;
            Add(direction*speed,speed);Add(direction*(speed*.6),speed);Add(direction*(speed*.3),speed);
        }
        private void Tangents(SteeringVector084 center,double radius,double speed)
        {
            double distance=center.Length;if(distance<1e-10)return;
            var bearing=center/distance;var right=new SteeringVector084(bearing.y,-bearing.x);
            if(distance<=radius)
            {
                Direction(right,speed);Direction(-right,speed);Direction(-bearing,speed);return;
            }
            double sine=Clamp(radius/distance,0,1),cosine=Math.Sqrt(Math.Max(0,1-sine*sine));
            Direction(bearing*cosine+right*sine,speed);
            Direction(bearing*cosine-right*sine,speed);
        }
        private static double ClosestTime(SteeringVector084 relative,SteeringVector084 relativeVelocity,double horizon)
        {
            double length=relativeVelocity.LengthSquared;
            return length>1e-16?Clamp(-SteeringVector084.Dot(relative,relativeVelocity)/length,0,horizon):0;
        }
        internal static bool Conflicts(SteeringVector084 origin,SteeringVector084 preferred,double radius,SteeringNeighbor084 neighbor,double horizon=Horizon)
        {
            var relative=neighbor.position-origin;var motion=neighbor.intent-preferred;
            double time=ClosestTime(relative,motion,horizon),clearance=radius+neighbor.radius+Comfort;
            return (relative+motion*time).LengthSquared<clearance*clearance;
        }
        internal static SteeringNeighbor084 FollowingPrediction(SteeringNeighbor084 neighbor,SteeringVector084 preferred,bool canonicalRearFollower)
        {
            if(!canonicalRearFollower||!neighbor.moving||preferred.LengthSquared<1e-16)return neighbor;
            // The existing bridge policy gives the higher directed-across actor
            // priority and physically caps its rear follower at the real gap.
            // Keep the observed rear heading while not making its leader
            // pre-empt that projected overrun. This is advisory, never a body,
            // command, actual speed cap or the authoritative sequential sweep.
            neighbor.intent=neighbor.intent.Normalized*Math.Min(neighbor.intent.Length,preferred.Length);return neighbor;
        }
        internal SteeringVector084 Choose(int actorId,SteeringVector084 origin,SteeringVector084 preferred,double radius,
            double dt,IList<SteeringNeighbor084> neighbors,Func<SteeringVector084,bool> admissible,double horizon=Horizon)
        {
            candidates.Clear();scored.Clear();CandidateCount=AdmissibleCount=GateChecks=0;AnticipatoryConflict=OppositeFlow=false;
            horizon=Math.Max(dt,Math.Min(Horizon,horizon));
            double speed=preferred.Length;if(speed<1e-10||dt<=0)return new SteeringVector084();
            var forward=preferred/speed;var right=new SteeringVector084(forward.y,-forward.x);
            int neighborCount=Math.Min(neighbors.Count,MaxNeighbors);
            for(int n=0;n<neighborCount;n++)
            {
                var neighbor=neighbors[n];
                if(!Conflicts(origin,preferred,radius,neighbor,horizon))continue;
                AnticipatoryConflict=true;
                if(neighbor.moving&&neighbor.intent.LengthSquared>1e-12&&
                    SteeringVector084.Dot(forward,neighbor.intent.Normalized)<-.35)OppositeFlow=true;
            }
            Add(preferred,speed);
            // In free space this exactly retains the caller's waypoint speed cap.
            if(!AnticipatoryConflict)
            {GateChecks++;if(admissible(preferred)){CandidateCount=AdmissibleCount=1;return preferred;}}
            Add(new SteeringVector084(),speed);
            Add(preferred*.8,speed);Add(preferred*.6,speed);Add(preferred*.4,speed);Add(preferred*.2,speed);
            // These lateral escape directions exist even at tangent contact.
            // Dynamic circle tangents below provide non-quantized headings.
            Direction(right,speed);Direction(-right,speed);
            Direction(forward+right,speed);Direction(forward-right,speed);
            for(int n=0;n<Math.Min(neighborCount,DirectionNeighbors);n++)
            {
                var neighbor=neighbors[n];
                var relative=neighbor.position-origin;double clearance=radius+neighbor.radius+Comfort;
                var relativeVelocity=neighbor.intent-preferred;
                double time=ClosestTime(relative,relativeVelocity,horizon);
                // Current-body tangents let exact sequential sweeps find space;
                // intent-predicted tangents begin turning before that space closes.
                Tangents(relative,clearance,speed);
                Tangents(relative+neighbor.intent*time,clearance,speed);
                Tangents(relative+neighbor.intent*horizon,clearance,speed);
                if(relative.LengthSquared<clearance*clearance*1.8)Direction(-relative,speed);
            }
            foreach(var velocity in candidates)
            {
                CandidateCount++;
                // Progress and minimal deflection favor the actual visible route.
                double score=(velocity-preferred).LengthSquared/(speed*speed)*.55-
                    SteeringVector084.Dot(velocity,forward)/speed;
                if(OppositeFlow)score+=Math.Max(0,-SteeringVector084.Dot(velocity,right))/speed*.35;
                for(int n=0;n<neighborCount;n++)
                {
                    var neighbor=neighbors[n];
                    var relative=neighbor.position-origin;var motion=neighbor.intent-velocity;
                    double time=ClosestTime(relative,motion,horizon);
                    double distance=(relative+motion*time).Length;
                    double physical=radius+neighbor.radius,preferredClearance=physical+Comfort;
                    double deficit=Math.Max(0,preferredClearance-distance);
                    // Prediction is advisory. Stationary bodies get the full
                    // penalty; a mover never relies on a parked body's response.
                    double weight=neighbor.moving?18:30;
                    score+=weight*deficit*deficit/(Comfort*Comfort+.04);
                    if(distance<physical)score+=4*(physical-distance)/(physical+.01);
                    // Distant neighbors cannot dominate progress with an
                    // arbitrary side preference. Only imminent crossing pairs
                    // use a stable ID tie-break to make one party more patient.
                    if(neighbor.moving&&neighbor.intent.LengthSquared>1e-12&&time>dt&&time<horizon&&deficit>0&&
                        Math.Abs(SteeringVector084.Dot(forward,neighbor.intent.Normalized))<.6&&actorId>neighbor.id)
                        score+=Math.Max(0,SteeringVector084.Dot(velocity,forward))/speed*.08;
                }
                scored.Add(new ScoredVelocity{velocity=velocity,score=score,index=scored.Count});
            }
            // Scoring is bounded96x16. Rank first, then call the unchanged
            // expensive Nav/lane/body gates only until the first legal choice.
            // This is the same minimum over legal candidates, with stable ties.
            scored.Sort(CompareScored);
            foreach(var candidate in scored)
            {GateChecks++;if(admissible(candidate.velocity)){AdmissibleCount++;return candidate.velocity;}}
            return new SteeringVector084();
        }
        private static int CompareScored(ScoredVelocity a,ScoredVelocity b)
        {int order=a.score.CompareTo(b.score);return order!=0?order:a.index.CompareTo(b.index);}
    }
}
