using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier
{
    public sealed partial class FrontierHud
    {
        private Texture2D mapPreview;
        private Text transportCargoHeading079,transportCargoPassengers079;
        private int transportCargoShip079=-1,transportCargoModalGeneration079=-1;
        private int neutralModalSite=-1;private string neutralModalSignature="";
        private string NeutralSignature(int site)
        {var state=game.World?.NeutralAt(site);if(state==null)return "";var p=game.World.Players[game.ObserverPlayer];return state.owner+"/"+state.claimant+"/"+(int)state.claimProgress+"/"+state.contested+"/"+state.jobs.Count+"/"+(state.jobs.Count>0?(int)state.jobs[0].elapsed:0)+"/"+(int)p.resources.food+"/"+(int)p.resources.wood+"/"+(int)p.resources.coin+"/"+p.technologies.Count+"/"+p.usedPopulation+"/"+p.reservedPopulation;}
        private void UpdateExpansionPanels()
        {RefreshTransportCargo079();if(neutralModalSite>=0&&ModalVisible&&!(uiPointer?.IsPressed??false)&&neutralModalSignature!=NeutralSignature(neutralModalSite))ShowNeutralSettlement(neutralModalSite);}
        private void ShowMapOptions()
        {
            var authored=game.SelectedScenario;var preview=authored==null?new World(game.Rules,game.MatchSeed,game.RosterCivilizations(),game.RosterTeams(),game.AgeLimit,game.SelectedMap):new World(game.Rules,authored,game.RosterCivilizations(),game.RosterTeams(),game.AgeLimit);
            var box=Modal("MAP & MISSION",710);
            Button(box,game.MapName,new Rect(25,75,470,48),()=>{game.CycleMap();ShowMapOptions();},"Cycle authored and generated maps.");
            Label(box,preview.Layout.Generated?"Generated terrain: this seed changes shores, crossings, spawns and resources.":"Authored terrain: the seed preserves this fixed map.",new Rect(25,138,470,70),18,muted);
            Label(box,"SEED",new Rect(25,220,100,32),20,gold);
            var field=Rect(box,"Map seed",new Rect(130,216,210,46));field.gameObject.AddComponent<Image>().color=background;
            var input=field.gameObject.AddComponent<InputField>();var value=Label(field,"",new Rect(10,6,190,36),22);
            input.textComponent=value;input.characterValidation=InputField.CharacterValidation.Integer;input.contentType=InputField.ContentType.IntegerNumber;input.characterLimit=11;input.text=game.MatchSeed.ToString();
            Button(box,"APPLY",new Rect(354,216,140,46),()=>{if(int.TryParse(input.text,out int seed)){game.MatchSeed=seed;ShowMapOptions();}else game.Notify("Enter a signed 32-bit integer seed");});
            var terrain=Rect(box,"Terrain preview",new Rect(530,75,280,280));var image=terrain.gameObject.AddComponent<RawImage>();
            image.texture=RenderMapPreview(preview);image.raycastTarget=false;
            Label(box,"P1 blue · P2 red · P3 green · P4 purple\nGold: bridges · White: harbor sites",new Rect(530,365,280,68),17,muted);
            Label(box,preview.Layout.RequiresTransport?"Two separated shores: an army needs a transport to cross.":"Land routes and shores are checked when the match is created.",new Rect(25,279,470,70),18,gold);
            Button(box,"RIVER EXPEDITIONS",new Rect(25,365,470,58),ShowRiverContent070,"Choose Traders' Bend or three linked original missions; each can also be edited in the workshop.");
            string[] modes={"elimination","capture_harbor","defend_bridge","escort_caravan"};
            for(int i=0;i<modes.Length;i++)
            {
                string id=modes[i];var definition=preview.ObjectiveDefinition(id);string reason=preview.HasScenario&&(authored.objectives.Length>0||!authored.enableElimination)&&id!="elimination"?
                    "This document uses its authored objectives. Edit them in the workshop.":preview.MapObjectiveAvailability(id);
                Button(box,(game.SelectedObjective==id?"[SELECTED] ":"")+(definition?.name??"Elimination"),new Rect(25+(i%2)*400,446+(i/2)*75,385,62),()=>{game.SelectedObjective=id;ShowMapOptions();},reason==""?"Use this mission at match start. Progress and failure conditions appear in the game HUD.":reason,reason=="");
            }
            string selectedReason=preview.MapObjectiveAvailability(game.SelectedObjective);
            Label(box,selectedReason==""?"Mission: "+(preview.ObjectiveDefinition(game.SelectedObjective)?.name??"Elimination"):selectedReason,new Rect(25,602,780,35),17,selectedReason==""?muted:gold);
            Button(box,"DONE",new Rect(590,647,220,48),()=>{CloseModal();ShowMenu();});
        }
        private Texture2D RenderMapPreview(World world)
        {
            if(mapPreview==null){mapPreview=new Texture2D(128,128,TextureFormat.RGBA32,false);mapPreview.filterMode=FilterMode.Point;}
            var pixels=new Color32[128*128];
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                var point=new Vector2(x+.5f,y+.5f);
                pixels[y*128+x]=world.Layout.IsBridge(point)?new Color32(195,164,89,255):world.Layout.IsWater(point)?new Color32(36,99,135,255):new Color32(110,123,67,255);
            }
            void Mark(Vector2 p,Color32 color,int radius){for(int y=-radius;y<=radius;y++)for(int x=-radius;x<=radius;x++){int px=Mathf.RoundToInt(p.x)+x,py=Mathf.RoundToInt(p.y)+y;if(px>=0&&px<128&&py>=0&&py<128&&x*x+y*y<=radius*radius)pixels[py*128+px]=color;}}
            foreach(var coast in world.Layout.DockSites)foreach(var point in coast)Mark(point,new Color32(235,228,200,255),2);
            for(int i=0;i<world.Players.Length;i++)Mark(world.OpeningTownPosition(i),ProceduralArt.OwnerColor(i),3);
            mapPreview.SetPixels32(pixels);mapPreview.Apply(false);return mapPreview;
        }
        private string ObjectiveSummary()
        {
            if(game.World.HasScenario&&game.World.ScenarioDocument.objectives.Length>0)return ScenarioObjectiveSummary069();
            var state=game.World.MapObjective;if(state==null||state.mode=="elimination")return "";
            var d=game.World.ObjectiveDefinition(state.mode);if(d==null)return "";
            string progress=state.mode=="escort_caravan"?"Checkpoint "+Mathf.Min(state.waypoint,state.route.Length)+" / "+state.route.Length:state.progress.ToString("0")+" / "+d.holdSeconds.ToString("0")+"s";
            return d.name.ToUpperInvariant()+" · "+state.status.ToUpperInvariant()+"\n"+progress+" · "+(state.contested?"CONTESTED":state.reason)+" · "+Mathf.Max(0,d.deadline-state.elapsed).ToString("0")+"s remaining";
        }
        private void ShowNeutralSettlement(int site)
        {
            var world=game.World;var state=world.NeutralAt(site);if(state==null)return;var d=world.NeutralDefinition(state.id);
            var unit=world.Rules.Entity(d.unit);var tech=world.Rules.Tech(d.tech);var box=Modal(d.name.ToUpperInvariant(),560);
            neutralModalSite=site;neutralModalSignature=NeutralSignature(site);
            Label(box,unit.description+"\n\n"+tech.description,new Rect(25,82,780,120),19);
            Label(box,"Treaty: "+(state.owner<0?state.claimant<0?"unclaimed":"negotiating":"player "+(state.owner+1))+" · "+(state.contested?"contested":"open")+(state.jobs.Count>0?" · Service "+Mathf.Max(0,state.jobs[0].duration-state.jobs[0].elapsed).ToString("0")+"s":"")+"\nBuild your trading post here and keep a land unit nearby to use settlement services.",new Rect(25,214,780,85),18,muted);
            void Service(CommandKind kind,string title,string description,int y)
            {
                string reason=world.NeutralCommandReason(game.LocalPlayerSlot,site,kind);
                Button(box,title,new Rect(25,y,780,62),()=>{SubmitModalCommand(new GameCommand{player=game.LocalPlayerSlot,kind=kind,target=site},box,r=>ShowNeutralSettlement(site));},description+"\n"+reason,game.CanSubmitOrders&&reason=="");
            }
            Service(CommandKind.NeutralAlliance,"NEGOTIATE · "+ShortCost(d.allianceCost),"Paid treaty; enemy presence pauses or disrupts service.",310);
            Service(CommandKind.NeutralRecruit,"RECRUIT "+unit.name+" · "+ShortCost(world.UnitCost(game.LocalPlayerSlot,unit)),unit.description,385);
            Service(CommandKind.NeutralResearch,"RESEARCH "+tech.name+" · "+ShortCost(tech.cost),tech.description,460);
        }
        private void ShowTransportCargo(EntityState ship)
        {
            var box=Modal("TRANSPORT PASSENGERS",560);
            transportCargoShip079=ship.id;transportCargoModalGeneration079=modalGeneration;
            transportCargoHeading079=Label(box,"",new Rect(25,80,780,85),20,gold);
            transportCargoPassengers079=Label(box,"",new Rect(25,186,780,330),18);RefreshTransportCargo079();
        }
        public static string FormatTransportCargoSummary079(TransportCargoSummary summary)=>
            "Cargo "+summary.AboardSlots+" + "+summary.IncomingSlots+" incoming /"+summary.Capacity+" slots · "+summary.AboardUnits+" aboard · "+summary.IncomingUnits+" incoming";
        public string TransportCargoSummary079(EntityState ship)=>game.World!=null&&game.World.TryGetTransportCargoSummary(game.ObserverPlayer,ship,out var summary)?FormatTransportCargoSummary079(summary):"";
        private void RefreshTransportCargo079()
        {
            if(!ModalVisible||transportCargoModalGeneration079!=modalGeneration||transportCargoHeading079==null||transportCargoPassengers079==null)return;
            var ship=game.World?.Get(transportCargoShip079);
            string summary=TransportCargoSummary079(ship);
            if(summary=="")
            {transportCargoHeading079.text="Passenger details are available for your own living transport.";transportCargoPassengers079.text="";return;}
            transportCargoHeading079.text=summary+"\nSinking loses the passengers. Use UNLOAD and choose an open land shore.";
            string names="";var seen=new HashSet<int>();
            foreach(int id in ship.occupants)
            {
                var unit=game.World.Get(id);
                if(unit?.Alive==true&&unit.owner==game.ObserverPlayer&&unit.garrisonedIn==ship.id&&seen.Add(id))
                    names+=unit.def.name+" · "+game.World.TransportSlotCost(unit)+" slots · HP "+unit.hp.ToString("0")+"\n";
            }
            transportCargoPassengers079.text=names==""?"No passengers aboard":names;
        }
        private void OnDestroy(){if(mapPreview!=null)Destroy(mapPreview);if(editorMap069!=null)Destroy(editorMap069);}
    }
}
