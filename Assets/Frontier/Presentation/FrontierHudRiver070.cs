using UnityEngine;

namespace Frontier
{
    public sealed partial class FrontierHud
    {
        public void ShowRiverContent070()
        {
            if(game.IsLan||!game.InMenu){game.Notify("Return to the solo menu before choosing river content");return;}
            var box=Modal("RIVER EXPEDITIONS",720);
            Label(box,"A standard skirmish map and an original three-chapter expedition.\nYou can also open an editable copy of each layout in the workshop.",new Rect(25,75,815,60),18,muted);
            string[] ids={RiverContent070.Skirmish,RiverContent070.Scout,RiverContent070.Convoy,RiverContent070.Retreat};
            for(int i=0;i<ids.Length;i++)
            {
                string id=ids[i];var definition=GuidedPlayCatalog.Find(id);bool normal=id==RiverContent070.Skirmish;
                bool unlocked=normal||game.Progress.IsUnlocked(id);
                string title=normal?"TRADERS' BEND · STANDARD SKIRMISH":definition.Title;
                string description=normal?"Age I · two bridges · trade, forward resources and naval flanking":definition.Description;
                if(!unlocked)description+="\nRequires: "+GuidedPlayCatalog.Find(definition.UnlockAfterId).Title;
                Button(box,(!unlocked?"[LOCKED] ":"")+title+"\n"+description,new Rect(25,150+i*112,610,100),
                    ()=>{if(normal){if(game.SelectRiverContent(id)){CloseModal();ShowMenu();}}else game.StartGuided(id);},
                    normal?"Choose this map, then review civilizations, difficulty and era in the main menu.":definition.StartingSetupDescription,unlocked);
                Button(box,"EDIT COPY",new Rect(650,150+i*112,190,100),()=>game.OpenScenarioDocument(RiverContent070.Build(game.Rules,id)),
                    "Opens an independent author draft. Workshop playtest does not earn campaign unlocks.");
            }
            Button(box,"ALL CAMPAIGNS",new Rect(25,645,390,48),()=>ShowGuidedLibrary(true));
            Button(box,"MENU",new Rect(445,645,395,48),()=>{CloseModal();ShowMenu();});
        }
    }
}
