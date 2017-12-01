﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;

namespace Assembler {
    [Serializable]
    public class ParseException : Exception {
        public ParseException() { }
        public ParseException(string message) : base(message) { }
        public ParseException(string message, Exception inner) : base(message, inner) { }
        protected ParseException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
    class Program {
        static void WriteBE(List<byte> c, uint v) {
            c.Add((byte)((v >> 24) & 0xFF));
            c.Add((byte)((v >> 16) & 0xFF));
            c.Add((byte)((v >> 8) & 0xFF));
            c.Add((byte)(v & 0xFF));
        }
        static void WriteBE(List<byte> c, long v) {
            for (int s = 56; s >= 0; s -= 8) {
                c.Add((byte)((v >> s) & 0xFF));
            }
        }
        static string[] OpcodeLongNameArray = new string[] {
	        "Ret", "Call", "TailCall", "Jump", "TrueJump", "FalseJump",
	        "BoolNot", "IntNot", "BoolAnd", "IntAnd", "BoolOr", "IntOr",
	        "StrictEqual", "StrictNeq", "NumEqual", "NumNeq",
	        "NumLt", "NumGt", "NumLeq", "NumGeq",
	        "Null", "BoolFalse", "BoolTrue", 
	        "IntLiteral", "RealLiteral", "TypeLiteral", 
	        "Add", "Sub", "Mul", "Neg", 
	        "IntDiv", "IntMod", "RealDiv", "RealMod", 
	        "ToInt", "ToReal", 
	        "GetType", 
	        "CreateObject", "ShallowCopy", "GetGlobalObject", "GetFunctionObject", 
	        "GetMember", "SetMember", "GetMemberDynamic", "SetMemberDynamic", 
	        "PushFrame", "PopFrame", "Pick", "Bury", "PopDiscard",
	        "ClearCode", "ClearText", "AppendCode", "AppendCodeLiteral", "AppendText", "AppendFormat", "PrintText",
	        "DebugLine",
            "PerformGC",
        };
        static string[] OpcodeShortNameArray = new string[] {
            "R", "C", "TC", "JMP", "JT", "JF",
            "!", "~", "&&", "&", "||", "|",
            "===", "!==", "==", "!=",
            "<", ">", "<=", ">=",
            "N", "F", "T",
            "IL", "RL", "TL",
            "+", "-", "*", "--",
            "/", "%", "//", "%%",
            "TO_I", "TO_R",
            "GT",
            "NEW", "CPY", "GLOBAL", "THIS",
            "GET", "SET", "GETD", "SETD",
            "PUSHF", "POPF", "P", "B", "POP",
            "CL_C", "CL_T", "+C", "+L", "+T", "+F", "PRINT",
            "LINE",
            "GC",
        };
        static void Main(string[] args) {
            string source = null, destination = null;
            if (args.Length >= 1) source = args[0];
            if (args.Length >= 2) destination = args[1];
            if (source == null) {
                Console.WriteLine("Source file:");
                source = Console.ReadLine();
            }
            if (destination == null) {
                Console.WriteLine("Destination file:");
                destination = Console.ReadLine();
            }
            Dictionary<string, byte> Opcodes = new Dictionary<string, byte>();
            StringBuilder OpcodePattern = new StringBuilder();
            OpcodePattern.Append(@"\G(?<opcode>");
            for (int i = 0; i < OpcodeLongNameArray.Length; i++) {
                Opcodes.Add(OpcodeLongNameArray[i].ToUpper(), (byte)i);
                if (i > 0) {
                    OpcodePattern.Append(@"|");
                }
                OpcodePattern.Append(Regex.Escape(OpcodeLongNameArray[i]));
            }
            for (int i = 0; i < OpcodeShortNameArray.Length; i++) {
                Opcodes.Add(OpcodeShortNameArray[i].ToUpper(), (byte)i);
                OpcodePattern.Append(@"|");
                OpcodePattern.Append(Regex.Escape(OpcodeShortNameArray[i]));
            }
            OpcodePattern.Append(@")(\s+|$)");
            Regex OpcodeRegex = new Regex(OpcodePattern.ToString(), RegexOptions.IgnoreCase);
            Regex RealRegex = new Regex(@"\G(?<val>[0-9.Ee+-]+)[FfRr]\s*");
            Regex Int4Regex = new Regex(@"\G(?<val>(\+-)?[0-9]+)[Ii]\s*");
            Regex Int1Regex = new Regex(@"\G(?<val>\+?[0-9]+)\s*");
            Regex LongStringRegex = new Regex(@"\G""(?<text>([^""]|\\"")*)(?<!\\)""\s*");
            Regex ShortStringRegex = new Regex(@"\G'(?<text>([^']|\\')*)(?<!\\)'\s*");
            Regex SpecialRegex = new Regex(@"\G[\{\}]");
            Regex LabelRegex = new Regex(@"\G:(?<label>.+?)(\s+|$)");
            Regex ToLabelRegex = new Regex(@"\G->(?<label>.+?)(\s+|$)");
            Regex NextTokenRegex = new Regex(@"\G(?<token>.*?)(\s+|$)");
            string textToParse = File.ReadAllText(source, Encoding.UTF8);
            int parsePos = 0;
            var OutBytes = new Stack<Tuple<List<byte>, Dictionary<string, uint>>>();
            OutBytes.Push(Tuple.Create(new List<byte>(), new Dictionary<string, uint>()));
            try {
                while (parsePos < textToParse.Length) {
                    List<byte> ByteList = OutBytes.Peek().Item1;
                    Dictionary<string, uint> Labels = OutBytes.Peek().Item2;
                    Match m;
                    if ((m = OpcodeRegex.Match(textToParse, parsePos)).Success) {
                        ByteList.Add(Opcodes[m.Groups["opcode"].Value.ToUpper()]);
                    } else if ((m = RealRegex.Match(textToParse, parsePos)).Success) {
                        string strval = m.Groups["val"].Value;
                        double val;
                        if (double.TryParse(strval, out val)) {
                            WriteBE(ByteList, BitConverter.DoubleToInt64Bits(val));
                        } else {
                            throw new ParseException(string.Format("Invalid real value: {0}", strval));
                        }
                    } else if ((m = Int4Regex.Match(textToParse, parsePos)).Success) {
                        string strval = m.Groups["val"].Value;
                        long val;
                        if (long.TryParse(strval, out val)) {
                            WriteBE(ByteList, val);
                        } else {
                            throw new ParseException(string.Format("Invalid integer value: {0}", strval));
                        }
                    } else if ((m = Int1Regex.Match(textToParse, parsePos)).Success) {
                        string strval = m.Groups["val"].Value;
                        byte val;
                        if (byte.TryParse(strval, out val)) {
                            ByteList.Add(val);
                        } else {
                            throw new ParseException(string.Format("Invalid byte value: {0}", strval));
                        }
                    } else if ((m = LongStringRegex.Match(textToParse, parsePos)).Success) {
                        string text = m.Groups["text"].Value.Replace("\"\"", "\"");
                        byte[] textBytes = Encoding.UTF8.GetBytes(text);
                        WriteBE(ByteList, (uint)textBytes.Length);
                        ByteList.AddRange(textBytes);
                    } else if ((m = ShortStringRegex.Match(textToParse, parsePos)).Success) {
                        string text = m.Groups["text"].Value.Replace("''", "'");
                        byte[] textBytes = Encoding.UTF8.GetBytes(text);
                        if (textBytes.Length > 255) {
                            throw new ParseException(string.Format(
                                "Short-string literal must be less than 256 bytes, actually {0} bytes", textBytes.Length));
                        } else {
                            ByteList.Add((byte)textBytes.Length);
                            ByteList.AddRange(textBytes);
                        }
                    } else if ((m = SpecialRegex.Match(textToParse, parsePos)).Success) {
                        switch (m.Value) {
                        case "{":
                            OutBytes.Push(Tuple.Create(new List<byte>(), new Dictionary<string, uint>()));
                            break;
                        case "}":
                            if (OutBytes.Count <= 1) {
                                throw new ParseException("Unexpected } (end of code literal)");
                            }
                            List<byte> InnerList = OutBytes.Pop().Item1;
                            ByteList = OutBytes.Peek().Item1;
                            WriteBE(ByteList, (uint)InnerList.Count);
                            ByteList.AddRange(InnerList);
                            break;
                        }
                    } else if ((m = LabelRegex.Match(textToParse, parsePos)).Success) {
                        string label = m.Groups["label"].Value;
                        if (Labels.ContainsKey(label)) {
                            throw new ParseException(string.Format("Label {0} already defined", label));
                        } else {
                            Labels.Add(label, (uint)ByteList.Count);
                        }
                    } else if ((m = ToLabelRegex.Match(textToParse, parsePos)).Success) {
                        string label = m.Groups["label"].Value;
                        if (Labels.ContainsKey(label)) {
                            WriteBE(ByteList, Labels[label]);
                        } else {
                            throw new ParseException(string.Format("Label {0} is not defined", label));
                        }
                    } else if ((m = NextTokenRegex.Match(textToParse, parsePos)).Success) {
                        throw new ParseException("Unknown token: " + m.Groups["token"].Value + ";");
                    } else {
                        throw new ParseException(textToParse.Substring(parsePos));
                    }
                    parsePos += m.Length;
                }
                if (OutBytes.Count > 1) {
                    throw new ParseException("Unmatched { (start of code literal)");
                }
                using (FileStream fs = new FileStream(destination, FileMode.Create)) {
                    byte[] OutByteArr = OutBytes.Pop().Item1.ToArray();
                    fs.Write(OutByteArr, 0, OutByteArr.Length);
                }
            } catch (ParseException ex) {
                Console.WriteLine("ERROR:{0}", ex.Message);
                Console.ReadKey();
            }

        }
    }
}