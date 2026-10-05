using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Frontier
{
    // Validate the actual DTO shape before JsonUtility can ignore unknown keys,
    // coerce scalar types or supply field defaults. Values are scanned in place;
    // large snapshot arrays do not become a second untyped object tree.
    internal static class ReplayJson
    {
        const int MaxDepth=64,MaxArrayItems=1000000,MaxNumberCharacters=128;
        static readonly Dictionary<Type,Dictionary<string,FieldInfo>> Shapes=new Dictionary<Type,Dictionary<string,FieldInfo>>();
        public static void Validate<T>(string text,string[] rootRequired=null)
        {
            var reader=new Reader(text,rootRequired);reader.Value(typeof(T),0,false);reader.Space();
            if(reader.Position!=text.Length)throw new FormatException("Trailing replay JSON content");
        }
        static Dictionary<string,FieldInfo> Fields(Type type)
        {
            lock(Shapes)
            {
                if(Shapes.TryGetValue(type,out var shape))return shape;
                shape=new Dictionary<string,FieldInfo>(StringComparer.Ordinal);
                foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
                    if(!field.IsStatic&&!field.IsInitOnly&&!field.IsNotSerialized&&(field.IsPublic||field.IsDefined(typeof(SerializeField),true)))
                        shape.Add(field.Name,field);
                if(shape.Count==0)throw new FormatException("Unsupported replay JSON object type");
                Shapes.Add(type,shape);return shape;
            }
        }
        sealed class Reader
        {
            readonly string text;readonly string[] rootRequired;int cursor;
            public int Position=>cursor;
            public Reader(string value,string[] required){text=value??throw new FormatException("Missing replay JSON");rootRequired=required;}
            public void Space(){while(cursor<text.Length&&(text[cursor]==' '||text[cursor]=='\t'||text[cursor]=='\r'||text[cursor]=='\n'))cursor++;}
            bool Take(char c){Space();if(cursor<text.Length&&text[cursor]==c){cursor++;return true;}return false;}
            void Need(char c){if(!Take(c))throw new FormatException("Invalid replay JSON punctuation");}
            bool Literal(string value)
            {
                Space();if(cursor+value.Length>text.Length||string.CompareOrdinal(text,cursor,value,0,value.Length)!=0)return false;
                cursor+=value.Length;return true;
            }
            public void Value(Type type,int depth,bool allowNull=true)
            {
                if(depth>MaxDepth)throw new FormatException("Replay JSON nesting limit exceeded");Space();
                if(cursor>=text.Length)throw new FormatException("Incomplete replay JSON");
                if(Literal("null"))
                {
                    bool list=type.IsGenericType&&type.GetGenericTypeDefinition()==typeof(List<>);
                    bool structure=type==typeof(ReplayDocument)||type==typeof(ReplayIdentity)||type==typeof(ReplayAnchor)||type==typeof(ReplayRecord)||type==typeof(WorldSnapshot)||type==typeof(MatchSimulationSnapshot)||type==typeof(GameCommand)||type==typeof(SimEvent);
                    if(!allowNull||type.IsValueType||type.IsArray||list||structure)throw new FormatException("Replay JSON has an invalid null");return;
                }
                if(type==typeof(string)){String(false);return;}
                if(type==typeof(bool))
                {if(!Literal("true")&&!Literal("false"))throw new FormatException("Replay JSON boolean required");return;}
                if(type.IsEnum||type==typeof(int)||type==typeof(long)||type==typeof(float)||type==typeof(double)||type==typeof(uint))
                {Number(type);return;}
                Type element=type.IsArray?type.GetElementType():type.IsGenericType&&type.GetGenericTypeDefinition()==typeof(List<>)?type.GetGenericArguments()[0]:null;
                if(element!=null)
                {
                    Need('[');if(Take(']'))return;int count=0;
                    do{if(++count>MaxArrayItems)throw new FormatException("Replay JSON array limit exceeded");Value(element,depth+1);}while(Take(','));Need(']');return;
                }
                Need('{');var shape=Fields(type);var seen=new HashSet<string>(StringComparer.Ordinal);
                if(!Take('}'))
                {
                    do
                    {
                        string key=String(true);
                        if(!seen.Add(key))throw new FormatException("Duplicate replay field: "+key);
                        if(!shape.TryGetValue(key,out var field))throw new FormatException("Unknown replay field: "+key);
                        Need(':');Value(field.FieldType,depth+1);
                    }while(Take(','));Need('}');
                }
                if(depth==0&&rootRequired!=null)
                {foreach(string key in rootRequired)if(!seen.Contains(key))throw new FormatException("Missing replay field: "+key);}
                else foreach(string key in shape.Keys)if(!seen.Contains(key))throw new FormatException("Missing replay field: "+key);
            }
            string String(bool key)
            {
                Need('"');StringBuilder result=key?new StringBuilder():null;bool high=false;
                while(cursor<text.Length)
                {
                    char c=text[cursor++];
                    if(c=='"')
                    {if(high)throw new FormatException("Unpaired replay JSON surrogate");return result?.ToString();}
                    if(c<' ')throw new FormatException("Replay JSON string contains a control character");
                    if(c=='\\')
                    {
                        if(cursor>=text.Length)throw new FormatException("Incomplete replay JSON escape");c=text[cursor++];
                        switch(c)
                        {
                            case '"':case '\\':case '/':break;
                            case 'b':c='\b';break;case 'f':c='\f';break;case 'n':c='\n';break;case 'r':c='\r';break;case 't':c='\t';break;
                            case 'u':
                                int code=0;if(cursor+4>text.Length)throw new FormatException("Incomplete replay Unicode escape");
                                for(int i=0;i<4;i++){char h=text[cursor++];int digit=h>='0'&&h<='9'?h-'0':h>='a'&&h<='f'?h-'a'+10:h>='A'&&h<='F'?h-'A'+10:-1;if(digit<0)throw new FormatException("Invalid replay Unicode escape");code=code*16+digit;}
                                c=(char)code;break;
                            default:throw new FormatException("Invalid replay JSON escape");
                        }
                    }
                    if(high)
                    {if(!char.IsLowSurrogate(c))throw new FormatException("Unpaired replay JSON surrogate");high=false;}
                    else if(char.IsHighSurrogate(c))high=true;
                    else if(char.IsLowSurrogate(c))throw new FormatException("Unpaired replay JSON surrogate");
                    if(key){if(result.Length>=256)throw new FormatException("Replay JSON field name limit exceeded");result.Append(c);}
                }
                throw new FormatException("Unterminated replay JSON string");
            }
            void Number(Type type)
            {
                Space();int start=cursor;if(cursor<text.Length&&text[cursor]=='-')cursor++;
                if(cursor>=text.Length||!Digit(text[cursor]))throw new FormatException("Replay JSON number required");
                if(text[cursor]=='0')cursor++;else Digits();
                if(cursor<text.Length&&text[cursor]=='.'){cursor++;if(!Digits())throw new FormatException("Invalid replay JSON fraction");}
                if(cursor<text.Length&&(text[cursor]=='e'||text[cursor]=='E')){cursor++;if(cursor<text.Length&&(text[cursor]=='+'||text[cursor]=='-'))cursor++;if(!Digits())throw new FormatException("Invalid replay JSON exponent");}
                if(cursor-start>MaxNumberCharacters)throw new FormatException("Replay JSON number limit exceeded");
                string value=text.Substring(start,cursor-start);
                if(type==typeof(float))
                {if(!float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out float f)||!SnapshotCodec.Finite(f))throw new FormatException("Replay JSON float is not finite");return;}
                if(type==typeof(double))
                {if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out double d)||double.IsNaN(d)||double.IsInfinity(d))throw new FormatException("Replay JSON double is not finite");return;}
                if(type==typeof(long))
                {if(!long.TryParse(value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out _))throw new FormatException("Replay JSON integer required");return;}
                if(type==typeof(uint))
                {if(!uint.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out _))throw new FormatException("Replay JSON unsigned integer required");return;}
                if(!int.TryParse(value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out _))throw new FormatException("Replay JSON integer required");
                // Unknown command enum integers are legitimate rejected inputs
                // in the audit stream. Core validation decides where an enum
                // must be defined; the JSON codec only requires an exact integer.
            }
            bool Digits(){int start=cursor;while(cursor<text.Length&&Digit(text[cursor]))cursor++;return start!=cursor;}
            static bool Digit(char c)=>c>='0'&&c<='9';
        }
    }
}
