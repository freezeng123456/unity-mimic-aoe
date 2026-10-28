using System;

namespace Frontier
{
    public sealed partial class GameController
    {
        public bool SelectRiverContent(string id)
        {
            if(IsLan||!InMenu){Notify("Return to the solo menu before choosing an expedition map");return false;}
            if(id!=RiverContent070.Skirmish){Notify("Choose expedition chapters from the campaign library");return false;}
            try
            {
                var document=RiverContent070.Build(Rules,id);
                document.starts[0].civilization=PlayerCivilization;document.starts[1].civilization=OpponentCivilization;
                if(!SelectAuthoredScenario(document))return false;
                MatchSeed=document.seed;SelectedMap=MapTopology.Scenario;SkirmishMode="duel";SelectedObjective="elimination";
                Notify("Traders' Bend selected. Choose civilizations and begin the skirmish.");return true;
            }
            catch(Exception error){Notify("River map could not be selected: "+error.Message);return false;}
        }
    }
}
