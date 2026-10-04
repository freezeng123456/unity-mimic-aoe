using System;
using System.Collections.Generic;

namespace Frontier
{
    public partial class World
    {
        private static void ValidateTransportSnapshot(RuleCatalog rules,EntitySnapshot[] entities)
        {
            void Require(bool valid,string message){if(!valid)throw new ArgumentException("Invalid saved transport: "+message);}
            var byId=new Dictionary<int,EntitySnapshot>();foreach(var e in entities)byId.Add(e.state.id,e);
            foreach(var saved in entities)
            {
                var host=saved.state;var definition=rules.Entity(saved.definition);
                if(definition.transportShip)
                {
                    Require(definition.waterborne&&definition.kind==EntityKind.Unit&&definition.transportCapacity>0,"host definition");
                    int load=0;var unique=new HashSet<int>();
                    foreach(int id in saved.occupants)
                    {
                        Require(unique.Add(id)&&byId.TryGetValue(id,out _),"cargo identity");
                        var cargo=byId[id];var unit=rules.Entity(cargo.definition);
                        Require(host.Alive&&cargo.state.Alive&&cargo.state.garrisonedIn==host.id&&cargo.state.owner==host.owner&&unit.kind==EntityKind.Unit&&!unit.waterborne,"cargo relationship");
                        load+=Math.Max(1,unit.population);
                    }
                    Require(load<=definition.transportCapacity,"capacity");
                }
                else if(saved.occupants.Length>0)
                {
                    Require(definition.kind==EntityKind.Building&&definition.garrison>=saved.occupants.Length,"shelter definition or capacity");
                    var unique=new HashSet<int>();foreach(int id in saved.occupants)
                    {
                        Require(unique.Add(id)&&byId.TryGetValue(id,out _),"shelter occupant identity");
                        var cargo=byId[id];var unit=rules.Entity(cargo.definition);
                        Require(unit.kind==EntityKind.Unit&&!unit.waterborne&&cargo.state.owner==host.owner&&cargo.state.garrisonedIn==host.id,"shelter relationship");
                    }
                }
                if(!host.Alive||host.garrisonedIn==0)continue;
                Require(byId.TryGetValue(host.garrisonedIn,out var parent),"missing host");
                var parentDef=rules.Entity(parent.definition);
                if(!parentDef.transportShip)
                {Require(parentDef.kind==EntityKind.Building&&parentDef.garrison>0&&definition.kind==EntityKind.Unit&&!definition.waterborne&&host.owner==parent.state.owner&&Array.IndexOf(parent.occupants,host.id)>=0,"unlisted shelter occupant");continue;}
                Require(!definition.waterborne&&definition.kind==EntityKind.Unit&&host.owner==parent.state.owner&&parent.state.Alive&&Array.IndexOf(parent.occupants,host.id)>=0,"unlisted passenger");
            }
        }
    }
}
