using System;
using System.Collections.Generic;
namespace Frontier
{
    public static class DeckLibrary
    {
        public static List<CardDef> Available(RuleCatalog rules,string civilization)
        {
            var cards=new List<CardDef>();foreach(var card in rules.cards)
                if(string.IsNullOrEmpty(card.civilization)||card.civilization==civilization)cards.Add(card);
            cards.Sort((a,b)=>a.age!=b.age?a.age.CompareTo(b.age):string.CompareOrdinal(a.id,b.id));return cards;
        }
        public static List<string> Preset(RuleCatalog rules,string civilization,string preset)
        {
            var cards=Available(rules,civilization);cards.Sort((a,b)=>{
                int scoreA=Priority(a,preset),scoreB=Priority(b,preset);
                return scoreA!=scoreB?scoreA.CompareTo(scoreB):a.age!=b.age?a.age.CompareTo(b.age):string.CompareOrdinal(a.id,b.id);
            });
            var result=new List<string>();for(int i=0;i<Math.Min(rules.deckCapacity,cards.Count);i++)result.Add(cards[i].id);return result;
        }
        private static int Priority(CardDef card,string preset)
        {
            bool military=!string.IsNullOrEmpty(card.unit)&&card.unit!="villager";
            return (preset=="military"?military:!military)?0:1;
        }
        public static string Validate(RuleCatalog rules,string civilization,IList<string> deck)
        {
            if(deck==null||deck.Count==0||deck.Count>rules.deckCapacity)return "Choose 1–"+rules.deckCapacity+" cards";
            var unique=new HashSet<string>();foreach(var id in deck)
            {
                var c=rules.Card(id);if(c==null)return "Unknown card";if(!unique.Add(id))return "Duplicate card";
                if(!string.IsNullOrEmpty(c.civilization)&&c.civilization!=civilization)return "Card belongs to another civilization";
            }
            return "";
        }
    }
}
