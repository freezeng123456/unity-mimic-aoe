using System;
using System.Collections.Generic;
namespace Frontier
{
    public static class DeckLibrary
    {
        public static bool EntityAllowed(RuleCatalog rules,string civilization,string id)
        {
            var entity=rules.Entity(id);if(entity==null)return false;
            if(!string.IsNullOrEmpty(entity.civilization)&&entity.civilization!=civilization)return false;
            return entity.excludedCivilizations==null||Array.IndexOf(entity.excludedCivilizations,civilization)<0;
        }
        public static bool CardAllowed(RuleCatalog rules,string civilization,CardDef card)
        {
            if(card==null||card.revolutionOnly||(!string.IsNullOrEmpty(card.civilization)&&card.civilization!=civilization))return false;
            if(!string.IsNullOrEmpty(card.unit)&&!EntityAllowed(rules,civilization,card.unit))return false;
            if(!string.IsNullOrEmpty(card.tech))
            {
                var tech=rules.Tech(card.tech);if(tech==null||(!string.IsNullOrEmpty(tech.civilization)&&tech.civilization!=civilization))return false;
                // Shared multi-unit technology remains useful if at least one target is available.
                if(tech.units!=null&&tech.units.Length>0&&!Array.Exists(tech.units,id=>EntityAllowed(rules,civilization,id)))return false;
            }
            return true;
        }
        public static List<CardDef> Available(RuleCatalog rules,string civilization)
        {
            var cards=new List<CardDef>();foreach(var card in rules.cards)
                if(CardAllowed(rules,civilization,card))cards.Add(card);
            cards.Sort((a,b)=>a.age!=b.age?a.age.CompareTo(b.age):string.CompareOrdinal(a.id,b.id));return cards;
        }
        public static List<string> Preset(RuleCatalog rules,string civilization,string preset)
        {
            var cards=Available(rules,civilization);cards.Sort((a,b)=>{
                int scoreA=Priority(rules,a,civilization,preset),scoreB=Priority(rules,b,civilization,preset);
                return scoreA!=scoreB?scoreA.CompareTo(scoreB):a.age!=b.age?a.age.CompareTo(b.age):string.CompareOrdinal(a.id,b.id);
            });
            var result=new List<string>();for(int i=0;i<Math.Min(rules.deckCapacity,cards.Count);i++)result.Add(cards[i].id);
            // New specialist reinforcements must not crowd every opening option
            // out of a finite military or technology preset.
            if(result.Count>0&&!result.Exists(id=>rules.Card(id).age==1))
            {
                var opening=cards.Find(card=>card.age==1);if(opening!=null)result[result.Count-1]=opening.id;
            }
            // Keep a late-game option even in economy and technology decks with many early upgrades.
            if(result.Count>0&&!result.Exists(id=>rules.Card(id).age==5))
            {
                var late=cards.Find(card=>card.age==5);
                int replace=result.FindLastIndex(id=>rules.Card(id).age!=1);
                if(replace<0&&result.Count>1)replace=result.Count-1;
                if(late!=null&&replace>=0)result[replace]=late.id;
            }
            return result;
        }
        private static int Priority(RuleCatalog rules,CardDef card,string civilization,string preset)
        {
            // Every preset includes the defining economic mechanism and one specialist force.
            if(CoreCard(card.id,civilization))return -100;
            // Industrial play needs the real, once-only wagon shipment in a
            // finite deck. Never mistake its civilian wagon for military aid.
            if(card.unit=="factory_wagon")return -75;
            var unit=string.IsNullOrEmpty(card.unit)?null:rules.Entity(card.unit);
            var tech=string.IsNullOrEmpty(card.tech)?null:rules.Tech(card.tech);
            bool military=unit!=null&&(unit.tags&Tags.Villager)==0&&!unit.civilian;
            bool militaryTech=tech!=null&&(tech.attack>0||tech.speed>0||(tech.health>0&&
                (tech.tags&(Tags.Infantry|Tags.Cavalry|Tags.Artillery))!=0));
            if(tech!=null&&tech.units!=null)foreach(var id in tech.units)
            {
                var target=rules.Entity(id);
                if(target!=null&&target.kind==EntityKind.Unit&&(target.tags&Tags.Villager)==0&&!target.civilian)militaryTech=true;
            }
            int category;
            if(preset=="technology")category=tech!=null?0:military?30:20;
            else if(preset=="military")category=military?0:militaryTech?10:20;
            else category=military?30:militaryTech?20:0;
            // Faction identity wins ties over shared reinforcements. Card age still orders each group.
            return category+(card.civilization==civilization?0:4);
        }
        private static bool CoreCard(string id,string civilization)
        {
            switch(civilization)
            {
                case "coast":return id=="settlers"||id=="card_coast_manor_frames"||id=="coast_bow_patrol";
                case "river":return id=="bankers"||id=="river_coin"||id=="reserve_carabineer";
                case "france":return id=="france_coureurs_1"||id=="card_france_hunting_parties"||id=="france_cuirassiers_3";
                case "ottoman":return id=="ottoman_settlers_1"||id=="card_ottoman_millet_support"||id=="ottoman_janissaries_2"||id=="ottoman_abus_2";
                case "wayfarer":return id=="wayfarer_cargo_wagons"||id=="card_wayfarer_cargo_bonds"||id=="wayfarer_escort_patrol";
                case "highland":return id=="card_highland_standard_jigs"||id=="highland_engineer_team"||id=="highland_culverin_battery";
                default:return false;
            }
        }
        public static string Validate(RuleCatalog rules,string civilization,IList<string> deck)
        {
            if(deck==null||deck.Count==0||deck.Count>rules.deckCapacity)return "Choose 1–"+rules.deckCapacity+" cards";
            var unique=new HashSet<string>();foreach(var id in deck)
            {
                var c=rules.Card(id);if(c==null)return "Unknown card";if(!unique.Add(id))return "Duplicate card";
                if(!CardAllowed(rules,civilization,c))return "Card or reward unavailable to this civilization";
            }
            return "";
        }
    }
}
