using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier
{
    public sealed partial class FrontierHud
    {
        int modalGeneration;
        bool lanLobbyVisible;
        string lanAddress="",lanPortText="47621";
        Text lanOptionsStatus,lanLobbyStatus,lanRoster,lanConfiguration,lanMatchStatus,lanPauseStatus;
        RectTransform lanMatchPanel;
        Button lanReady,lanStart,lanCivilization,lanDeck,lanMap,lanSharedPause;

        // A terminal receipt belongs to the submitted match and modal. Closing
        // it or opening another page invalidates the callback immediately.
        Action<CommandResult> ResolveModalCommand(Transform box,Action<CommandResult> resolved)
        {
            var world=game.World;string match=game.MatchId;int player=game.LocalPlayerSlot,generation=modalGeneration;
            return result=>
            {
                if(this==null||generation!=modalGeneration||box==null||modal==null||!box.IsChildOf(modal)||
                    !ReferenceEquals(world,game.World)||match!=game.MatchId||player!=game.LocalPlayerSlot)return;
                resolved?.Invoke(result);
            };
        }
        void SubmitModalCommand(GameCommand command,Transform box,Action<CommandResult> resolved)
        {SubmitUiCommand(command,ResolveModalCommand(box,resolved));}
        void SubmitUiCommand(GameCommand command,Action<CommandResult> resolved=null,long expectedQueueId=0)
        {
            if(game.IsLan&&!game.CanSubmitOrders){game.Notify("Orders are unavailable while the match is paused or reconnecting");return;}
            game.SubmitCommand(command,resolved,expectedQueueId);
        }
        void CloseLanModal()
        {
            lanLobbyVisible=false;lanOptionsStatus=null;lanLobbyStatus=null;lanRoster=null;lanConfiguration=null;
            lanReady=null;lanStart=null;lanCivilization=null;lanDeck=null;lanMap=null;
            lanPauseStatus=null;lanSharedPause=null;
        }

        InputField LanInput(Transform parent,string title,string value,Rect bounds,bool numeric=false)
        {
            var field=Rect(parent,title,bounds);field.gameObject.AddComponent<Image>().color=panelColor;
            var input=field.gameObject.AddComponent<InputField>();
            input.textComponent=Label(field,"",new Rect(12,7,bounds.width-24,bounds.height-14),20);
            var hint=Label(field,title,new Rect(12,7,bounds.width-24,bounds.height-14),18,muted);
            input.placeholder=hint;input.characterLimit=numeric?5:253;
            if(numeric){input.contentType=InputField.ContentType.IntegerNumber;input.characterValidation=InputField.CharacterValidation.Integer;}
            input.text=value;return input;
        }
        public void ShowLanOptions()
        {
            if(game.IsLan){ShowLanLobby();return;}
            var box=Modal("LAN CO-OP",545);
            Label(box,"Two players cooperate against two AI armies.\nUse the same game build on both Macs and connect to the host's LAN address.",new Rect(25,78,815,90),19,muted);
            Label(box,"HOST ADDRESS",new Rect(25,190,540,30),19,gold);
            Label(box,"PORT",new Rect(600,190,240,30),19,gold);
            var address=LanInput(box,"Host IP address",lanAddress,new Rect(25,230,550,48));
            var port=LanInput(box,"Port",lanPortText,new Rect(600,230,240,48),true);
            address.onValueChanged.AddListener(v=>lanAddress=v);port.onValueChanged.AddListener(v=>lanPortText=v);
            bool Port(out int value)
            {
                if(int.TryParse(port.text,out value)&&value>0&&value<=65535)return true;
                game.Notify("Choose a port between 1 and 65535");return false;
            }
            Button(box,"HOST CO-OP",new Rect(25,310,397,60),()=>
            {if(!Port(out int value))return;game.HostLan(value);if(game.IsLan)ShowLanLobby();});
            Button(box,"JOIN HOST",new Rect(443,310,397,60),()=>
            {
                if(string.IsNullOrWhiteSpace(address.text)){game.Notify("Enter the host's LAN address");return;}
                if(!Port(out int value))return;game.JoinLan(address.text.Trim(),value);if(game.IsLan)ShowLanLobby();
            });
            lanOptionsStatus=Label(box,"Choose your civilization and deck before READY. The host chooses the map and starts when both players are ready.",new Rect(25,391,815,62),17,muted);
            Button(box,"BACK",new Rect(25,472,815,48),()=>{CloseModal();ShowMenu();});
        }

        public void ShowLanLobby()
        {
            if(!game.IsLan){ShowLanOptions();return;}
            var box=Modal("LAN CO-OP ROOM",735,()=>game.LeaveLan());lanLobbyVisible=true;
            lanLobbyStatus=Label(box,"Connecting…",new Rect(25,77,815,60),18,gold);
            lanConfiguration=Label(box,"",new Rect(25,142,815,70),18,muted);
            lanRoster=Label(box,"",new Rect(25,222,815,215),19);
            lanCivilization=Button(box,"YOUR CIVILIZATION",new Rect(25,452,397,52),()=>ShowCivilizations(false,0));
            lanDeck=Button(box,"YOUR DECK",new Rect(443,452,397,52),()=>ShowDeckEditor(0));
            lanMap=Button(box,"MAP & OBJECTIVES",new Rect(25,520,397,52),ShowMapOptions);
            Button(box,"DECK PRESET",new Rect(443,520,397,52),()=>
            {
                var local=LocalLobbySlot();if(local?.ready==true)return;
                string[] presets={"economy","military","technology"};game.DeckPreset=presets[(Array.IndexOf(presets,game.DeckPreset)+1)%presets.Length];
                game.ResetDeckPreset();RefreshLanHud();
            });
            lanReady=Button(box,"READY",new Rect(25,591,397,60),()=>game.SetLanReady(!(LocalLobbySlot()?.ready??false)));
            lanStart=Button(box,"START CO-OP",new Rect(443,591,397,60),()=>game.StartLanMatch());
            Button(box,"LEAVE ROOM",new Rect(25,674,815,40),()=>game.LeaveLan());RefreshLanHud();
        }
        public void RefreshLanLobbyState()
        {
            if(!game.IsLan)return;
            if(lanLobbyVisible)RefreshLanHud();
            else if(game.InMenu&&!ModalVisible)ShowLanLobby();
        }
        LanLobbySlot LocalLobbySlot()
        {
            var slots=game.LanLobby?.slots;int local=game.LocalPlayerSlot;
            return slots!=null&&local>=0&&local<slots.Length?slots[local]:null;
        }
        void AddLanHud()
        {
            lanMatchPanel=null;lanMatchStatus=null;if(!game.IsLan)return;
            lanMatchPanel=Panel(gamePanel,"LAN connection",new Rect(976,258,592,99),background);
            lanMatchStatus=Label(lanMatchPanel,"",new Rect(12,8,568,43),17,gold);
            lanMatchStatus.resizeTextForBestFit=true;lanMatchStatus.resizeTextMinSize=13;lanMatchStatus.resizeTextMaxSize=17;lanMatchStatus.verticalOverflow=VerticalWrapMode.Truncate;
            Button(lanMatchPanel,"MATCH MENU",new Rect(12,56,568,34),()=>game.TogglePause());RefreshLanHud();
        }
        public void ShowLanPause()
        {
            if(!game.IsLan)return;
            var box=Modal("LAN MATCH MENU",525,()=>game.CloseLocalMenu());
            lanPauseStatus=Label(box,"",new Rect(25,78,815,80),19,muted);
            Button(box,"CONTINUE",new Rect(25,175,397,58),()=>game.CloseLocalMenu());
            lanSharedPause=Button(box,"PAUSE MATCH",new Rect(443,175,397,58),()=>
            {if(game.World!=null)game.SetLanSharedPause(!game.World.Paused);});
            Button(box,"SETTINGS",new Rect(25,255,397,58),ShowSettings);
            if(game.World?.IsMatchEnded==true)Button(box,"RESULTS",new Rect(443,255,397,58),()=>{game.CloseLocalMenu();ShowResult();});
            else Button(box,"RESIGN",new Rect(443,255,397,58),()=>{game.CloseLocalMenu();game.Resign();});
            if(game.IsLanHost)Button(box,"SAVE REPLAY",new Rect(25,335,815,58),()=>game.SaveReplayAsync());
            Button(box,"LEAVE MATCH",new Rect(25,435,815,58),()=>game.LeaveLan());RefreshLanHud();
        }
        void RefreshLanHud()
        {
            if(lanOptionsStatus!=null&&!string.IsNullOrEmpty(game.Notice))lanOptionsStatus.text=game.Notice;
            if(lanLobbyVisible&&lanLobbyStatus!=null)
                lanLobbyStatus.text=game.LanConnectionText+"\n"+(game.IsLanHost?"Host · port "+game.LanPort:"Partner · you control P"+(game.LocalPlayerSlot+1));
            if(lanLobbyVisible&&game.IsLan&&game.LanLobby!=null)
            {
                var lobby=game.LanLobby;var local=LocalLobbySlot();bool configuring=local?.ready!=true&&!lobby.started;
                lanConfiguration.text=MapTopology.Name(lobby.mapId)+" · seed "+lobby.seed+" · age limit "+lobby.maxAge+"\nAI: "+(game.Rules.Difficulty(lobby.difficulty)?.name??lobby.difficulty)+" · P1 and P3 cooperate";
                var roster=new StringBuilder();
                for(int i=0;i<lobby.slots.Length;i++)
                {
                    var slot=lobby.slots[i];if(slot==null)continue;
                    roster.Append("P").Append(i+1).Append(i==game.LocalPlayerSlot?" · YOU":slot.human?" · PARTNER":" · AI")
                        .Append(" · ").Append(game.Rules.Civ(slot.civilization)?.name??"Choosing civilization")
                        .Append(slot.human?slot.ready?" · READY":" · NOT READY":"")
                        .Append("\n");
                }
                lanRoster.text=roster.ToString();
                lanCivilization.interactable=configuring;lanDeck.interactable=configuring;lanMap.interactable=configuring&&game.IsLanHost;
                lanCivilization.GetComponentInChildren<Text>().text="YOU: "+(game.Rules.Civ(game.PlayerCivilization)?.name??"Choose civilization");
                lanDeck.GetComponentInChildren<Text>().text="DECK: "+game.SelectedDeck.Count+" / "+game.Rules.deckCapacity+" · "+game.DeckPreset.ToUpperInvariant();
                lanReady.GetComponentInChildren<Text>().text=local?.ready==true?"NOT READY":"READY";
                lanReady.interactable=game.IsLanConnected&&!lobby.started&&DeckLibrary.Validate(game.Rules,game.PlayerCivilization,game.SelectedDeck)=="";
                bool bothReady=lobby.slots.Length==4&&lobby.slots[0]?.ready==true&&lobby.slots[2]?.ready==true;
                lanStart.interactable=game.IsLanHost&&game.IsLanConnected&&bothReady&&!lobby.started;
            }
            if(lanMatchPanel!=null&&game.IsLan)
            {
                var size=CanvasSize;float width=size.x*.37f;
                Position(lanMatchPanel,size.x*.61f,258,width,99);Position(lanMatchStatus.rectTransform,12,8,width-24,43);
                var button=lanMatchPanel.GetComponentInChildren<Button>();Position((RectTransform)button.transform,12,56,width-24,34);
                Position(button.GetComponentInChildren<Text>().rectTransform,10,4,width-44,26);
                lanMatchStatus.text="LAN · P"+(game.LocalPlayerSlot+1)+" · "+game.LanConnectionText+"\n"+(game.World?.Paused==true?"MATCH PAUSED · ":"")+game.LanPendingCount+" orders awaiting confirmation";
            }
            if(lanPauseStatus!=null&&game.IsLan)
            {
                bool paused=game.World?.Paused==true;
                lanPauseStatus.text=game.LanConnectionText+"\n"+(paused?"The shared match is paused.":"The match continues while this menu is open.")+"\n"+game.LanPendingCount+" orders awaiting confirmation";
                lanSharedPause.GetComponentInChildren<Text>().text=paused?"RESUME MATCH":"PAUSE MATCH";
                lanSharedPause.interactable=(game.IsLanHost||game.IsLanConnected)&&game.World!=null&&!game.World.IsMatchEnded;
            }
        }
    }
}
