using System;
using System.IO;
using UnityEngine;

namespace Frontier
{
    public sealed partial class GameController
    {
        // Eligibility is a preliminary identity check; CreateCopy performs the
        // full world, AI and catalog checks at the moment of copying.
        public bool IsLegacyMatchCandidate(int slot)
        {
            try
            {
                if(Application.version!=NeutralSaveMigration088.LegacyAppVersion)return false;
                var saved=MatchSaveStore.Read(MatchSaveStore.SlotPath(SaveDirectory,slot),Application.version);
                if(!NeutralSaveMigration088.IsNormalMatchId(saved.matchId)||saved.hasGuided||saved.guided!=null||saved.world.hasScenarioDocument||
                   !string.IsNullOrEmpty(saved.world.scenarioHash)||saved.world.mapId==MapTopology.Scenario||
                   saved.world.rulesHash!=NeutralSaveMigration088.LegacyRulesHash)return false;
                NeutralSaveMigration088.ReconstructPreviousRules(Rules);
                return true;
            }
            catch(Exception){return false;}
        }
        public bool CopyLegacyMatch(int sourceSlot,int destinationSlot)
        {
            if(IsLan||IsWatching){Notify("Leave the cooperative or replay session before copying a checkpoint");return false;}
            try
            {
                if(sourceSlot==destinationSlot)throw new IOException("Choose another empty slot");
                NeutralSaveMigration088.CreateCopy(MatchSaveStore.SlotPath(SaveDirectory,sourceSlot),
                    MatchSaveStore.SlotPath(SaveDirectory,destinationSlot),Rules,Application.version);
                Notify("Copied slot "+sourceSlot+" to slot "+destinationSlot+" · original preserved");return true;
            }
            catch(Exception error){Notify("Could not copy checkpoint: "+error.Message);return false;}
        }
    }
}
