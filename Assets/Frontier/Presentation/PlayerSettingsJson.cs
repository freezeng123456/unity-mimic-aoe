using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Frontier
{
    // Small bounded JSON reader for preference data. Reject duplicates and
    // malformed scalar types before they can become JsonUtility defaults.
    internal sealed class SettingsJson
    {
        private readonly string text;private int cursor;
        private SettingsJson(string text){this.text=text;}
        public static object Parse(string text)
        {var reader=new SettingsJson(text);var value=reader.Value(0);reader.Space();if(reader.cursor!=text.Length)throw new FormatException("Unexpected settings content");return value;}
        private void Space(){while(cursor<text.Length&&(text[cursor]==' '||text[cursor]=='\r'||text[cursor]=='\n'||text[cursor]=='\t'))cursor++;}
        private bool Take(char c){Space();if(cursor<text.Length&&text[cursor]==c){cursor++;return true;}return false;}
        private void Require(char c){if(!Take(c))throw new FormatException("Malformed preferences JSON");}
        private object Value(int depth)
        {
            if(depth>8)throw new FormatException("Preferences nesting is too deep");Space();if(cursor>=text.Length)throw new FormatException("Truncated preferences JSON");
            char c=text[cursor];
            if(c=='{')
            {
                cursor++;var result=new Dictionary<string,object>(StringComparer.Ordinal);if(Take('}'))return result;
                do{Space();string key=String();Require(':');if(result.ContainsKey(key))throw new FormatException("Duplicate preference field");result.Add(key,Value(depth+1));}while(Take(','));Require('}');return result;
            }
            if(c=='['){cursor++;var result=new List<object>();if(Take(']'))return result;do{if(result.Count>=256)throw new FormatException("Preferences array is too large");result.Add(Value(depth+1));}while(Take(','));Require(']');return result;}
            if(c=='"')return String();
            if(c=='t'){Literal("true");return true;}if(c=='f'){Literal("false");return false;}if(c=='n'){Literal("null");return null;}
            int start=cursor;if(c=='-')cursor++;
            if(cursor>=text.Length)throw new FormatException("Invalid preferences number");
            if(text[cursor]=='0')cursor++;else{if(text[cursor]<'1'||text[cursor]>'9')throw new FormatException("Invalid preferences number");Digits();}
            if(cursor<text.Length&&text[cursor]=='.'){cursor++;if(!Digits())throw new FormatException("Invalid preferences fraction");}
            if(cursor<text.Length&&(text[cursor]=='e'||text[cursor]=='E')){cursor++;if(cursor<text.Length&&(text[cursor]=='+'||text[cursor]=='-'))cursor++;if(!Digits())throw new FormatException("Invalid preferences exponent");}
            if(!double.TryParse(text.Substring(start,cursor-start),NumberStyles.Float,CultureInfo.InvariantCulture,out double number)||double.IsNaN(number)||double.IsInfinity(number))throw new FormatException("Invalid preferences number");return number;
        }
        private bool Digits(){int start=cursor;while(cursor<text.Length&&text[cursor]>='0'&&text[cursor]<='9')cursor++;return cursor>start;}
        private void Literal(string value){for(int i=0;i<value.Length;i++)if(cursor>=text.Length||text[cursor++]!=value[i])throw new FormatException("Invalid preferences literal");}
        private string String()
        {
            Require('"');var output=new StringBuilder();
            while(cursor<text.Length)
            {
                char c=text[cursor++];if(c=='"')return output.ToString();if(c<32)throw new FormatException("Invalid preferences string");
                if(c!='\\'){output.Append(c);continue;}if(cursor>=text.Length)break;c=text[cursor++];
                switch(c){case '"':case '\\':case '/':output.Append(c);break;case 'b':output.Append('\b');break;case 'f':output.Append('\f');break;case 'n':output.Append('\n');break;case 'r':output.Append('\r');break;case 't':output.Append('\t');break;
                    case 'u':if(cursor+4>text.Length||!ushort.TryParse(text.Substring(cursor,4),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out ushort code))throw new FormatException("Invalid preferences escape");output.Append((char)code);cursor+=4;break;
                    default:throw new FormatException("Invalid preferences escape");}
            }
            throw new FormatException("Truncated preferences string");
        }
    }
}
