using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyLua
{
    public class LuaVM
    {
        public enum VAR_TYPE
        {
            NIL, INT, STRING, DICT, FUNCTION
        }

        public enum IS
        {
            NOP = 90,
            IMOV = 128, PUSH, IPUSH, TOP, POP,
            LFS, STS, LFH, STH,
            JMP, JZ, JNZ,
            CALL, CALLS, RET,
            ADD, SUB, NEG, MUL, DIV, MOD, POW,
            AND, OR, NOT,
            EQ, NEQ, LT, LE, GT, GE,
            STRSUB, STRCON, STRLEN, STRCPY, STRADD, STRCMP, STRFMT,
            PRINT
        }
        public class Symbol
        {
            public string name;
            public VAR_TYPE type = VAR_TYPE.NIL;
            public int addr = 0;
            public int level = 0;
            public Symbol(string _name, int _addr, int _level = 0)
            {
                name = _name;
                addr = _addr;
            }

            private Symbol(VAR_TYPE _type, int _addr, int _level = 0)
            {
                type = _type;
                addr = _addr;
            }
            public Symbol(string _name, VAR_TYPE _type, int _addr, int _level = 0)
            {
                name = _name;
                type = _type;
                addr = _addr;
            }
            public static Symbol GenerateAnonymousSymbol(VAR_TYPE _type, int _addr, int _level = 0)
            {
                return new Symbol(_type, _addr, _level);
            }
        }

        public List<Symbol> symbols = new List<Symbol>();
        public List<Dictionary<int, Symbol>> dictStack = new List<Dictionary<int, Symbol>>();
        public List<string> strStack = new List<string>();
        public List<int> datStack = new List<int>();
        public List<int> datHeap = new List<int>();
        public bool debug = false;
        public int PC = 0, SP = 0, AX = 0;

        public List<int> program = new List<int>();
        public int GetSymbol(string name, int _level = 0)
        {
            for (int i = symbols.Count - 1; i >= 0; --i)
            {
                if (symbols[i].level <= _level && symbols[i].name == name)
                    return i;
            }
            symbols.Add(new Symbol(name, symbols.Count, _level));
            return symbols.Count - 1;
        }
        private void InitializeFunction()
        {
            symbols.Add(new Symbol("print", VAR_TYPE.FUNCTION, program.Count));
            program.Add((int)IS.PUSH);
            program.Add((int)IS.IPUSH);
            program.Add(-3); // -2 为 PC_1, -1 为 AX, 0 为 -3
            program.Add((int)IS.LFS);
            program.Add((int)IS.PRINT);
            program.Add((int)IS.POP); // 弹出 -3
            program.Add((int)IS.TOP);
            program.Add((int)IS.POP); // 弹出 AX
            program.Add((int)IS.RET);
        }
        public LuaVM()
        {
            strStack.Add("nil");
            InitializeFunction();
            PC = program.Count;
        }

        public bool Run()
        {
            while (PC < program.Count)
            {
                if (debug)
                {
                    this.ShowProgram();
                    this.ShowHeap();
                    this.ShowStack();
                }
                switch (program[PC])
                {
                    case (int)IS.NOP: ++PC; break;
                    case (int)IS.IMOV: ++PC; AX = program[PC]; ++PC; break;
                    case (int)IS.PUSH: datStack.Set(SP++, AX); ++PC; break;
                    case (int)IS.IPUSH: ++PC; datStack.Set(SP++, program[PC]); ++PC; break;
                    case (int)IS.TOP: AX = datStack[SP - 1]; ++PC; break;
                    case (int)IS.POP: --SP; ++PC; break;

                    case (int)IS.LFS: AX = datStack[SP - 1 + datStack[SP - 1]]; ++PC; break;
                    case (int)IS.STS: datStack.Set(SP - 1 + datStack[SP - 1], AX); ++PC; break;
                    case (int)IS.LFH: AX = datHeap.Get(datStack[SP - 1]); ++PC; break;
                    case (int)IS.STH: datHeap.Set(datStack[SP - 1], AX); ++PC; break;

                    case (int)IS.JMP: ++PC; PC = program[PC]; break;
                    case (int)IS.JZ: ++PC; PC = AX == 0 ? program[PC] : PC + 1; break;
                    case (int)IS.JNZ: ++PC; PC = AX != 0 ? program[PC] : PC + 1; break;

                    case (int)IS.CALL: ++PC; datStack.Set(SP++, PC + 1); PC = program[PC]; break;
                    case (int)IS.CALLS: ++PC; int addr = datStack[--SP]; datStack.Set(SP++, PC); PC = addr; break;
                    case (int)IS.RET: PC = datStack[--SP]; break;

                    case (int)IS.ADD: datStack[SP - 1] = datStack[SP - 1] + AX; ++PC; break;
                    case (int)IS.SUB: datStack[SP - 1] = datStack[SP - 1] - AX; ++PC; break;
                    case (int)IS.NEG: datStack.Set(SP++, -AX); ++PC; break;
                    case (int)IS.MUL: datStack[SP - 1] = datStack[SP - 1] * AX; ++PC; break;
                    case (int)IS.DIV: datStack[SP - 1] = datStack[SP - 1] / AX; ++PC; break;
                    case (int)IS.MOD: datStack[SP - 1] = datStack[SP - 1] % AX; ++PC; break;
                    case (int)IS.POW: datStack[SP - 1] = LuaVMHelper.Pow(datStack[SP - 1], AX); ++PC; break;

                    case (int)IS.AND: datStack[SP - 1] = (datStack[SP - 1] != 0 && AX != 0) ? 1 : 0; ++PC; break;
                    case (int)IS.OR: datStack[SP - 1] = (datStack[SP - 1] != 0 || AX != 0) ? 1 : 0; ++PC; break;
                    case (int)IS.NOT: datStack.Set(SP, (AX != 0) ? 0 : 1); ++SP; ++PC; break;

                    case (int)IS.EQ: datStack[SP - 1] = (datStack[SP - 1] == AX) ? 1 : 0; ; ++PC; break;
                    case (int)IS.NEQ: datStack[SP - 1] = (datStack[SP - 1] != AX) ? 1 : 0; ; ++PC; break;
                    case (int)IS.LT: datStack[SP - 1] = (datStack[SP - 1] < AX) ? 1 : 0; ; ++PC; break;
                    case (int)IS.LE: datStack[SP - 1] = (datStack[SP - 1] <= AX) ? 1 : 0; ; ++PC; break;
                    case (int)IS.GT: datStack[SP - 1] = (datStack[SP - 1] > AX) ? 1 : 0; ; ++PC; break;
                    case (int)IS.GE: datStack[SP - 1] = (datStack[SP - 1] >= AX) ? 1 : 0; ; ++PC; break;

                    case (int)IS.STRSUB:
                        strStack.Add(strStack[AX].Substring(datStack[SP - 2], datStack[SP - 1]));
                        SP -= 2;
                        AX = strStack.Count - 1;
                        ++PC;
                        break;
                    case (int)IS.STRCON:
                        strStack[AX] = strStack[AX] + strStack[datStack[--SP]];
                        ++PC;
                        break;
                    case (int)IS.STRLEN:
                        AX = strStack[AX].Length;
                        ++PC;
                        break;
                    case (int)IS.STRCPY:
                        strStack.Add(strStack[AX]);
                        AX = strStack.Count - 1;
                        ++PC;
                        break;
                    case (int)IS.STRADD:
                        strStack[AX] += (char)datStack[--SP];
                        ++PC;
                        break;
                    case (int)IS.STRCMP:
                        AX = LuaVMHelper.StrCmp(strStack[datStack[--SP]], strStack[AX]);
                        ++PC;
                        break;
                    case (int)IS.STRFMT:
                        strStack.Add(AX.ToString());
                        AX = strStack.Count - 1;
                        ++PC;
                        break;

                    case (int)IS.PRINT:
                        Console.WriteLine(strStack[AX]);
                        ++PC;
                        break;
                }
                if (debug)
                {
                    Console.ReadKey();
                }
            }
            return true;
        }
    }

    static class LuaVMHelper
    {
        public static void Set<T>(this List<T> list, int id, T val) where T : new()
        {
            if (id < 0) return;
            while (id >= list.Count) list.Add(new T());
            list[id] = val;
        }
        public static T Get<T>(this List<T> list, int id) where T : new()
        {
            if (id < 0) return new T();
            while (id >= list.Count) list.Add(new T());
            return list[id];
        }
        public static int Pow(int a, int x)
        {
            if (x < 0 || a == 0) return 0;
            int res = 1;
            for (int i = 0; i < x; ++i)
            {
                res *= a;
            }
            return res;
        }

        public static int StrCmp(string a, string b)
        {
            for (int i = 0; ; ++i)
            {
                if (i >= a.Length)
                {
                    if (i >= b.Length)
                        return 0;
                    else
                        return -b[i];
                }
                else
                {
                    if (i >= b.Length)
                        return a[i];
                    else
                    {
                        if (a[i] != b[i])
                            return (int)(a[i] - b[i]);
                    }
                }
            }
        }

        public static void ShowSymbols(this LuaVM vm)
        {
            Console.WriteLine("{0} symbols in total:", vm.symbols.Count);
            foreach (LuaVM.Symbol s in vm.symbols)
            {
                Console.WriteLine("{0}: type {1}, addr {2}", s.name, s.type, s.addr);
            }
        }
        public static void ShowHeap(this LuaVM vm)
        {
            Console.WriteLine("{0} data in heap:", vm.datHeap.Count);
            foreach (int x in vm.datHeap)
            {
                Console.Write("{0} ", x);
            }
            Console.WriteLine();
        }
        public static void ShowStack(this LuaVM vm)
        {
            Console.WriteLine("{0} data in stack:", vm.SP);
            for (int i = 0; i < vm.SP; ++i)
            {
                Console.Write("{0} ", vm.datStack[i]);
            }
            Console.WriteLine();
            Console.WriteLine("{0} data in string Stack:", vm.strStack.Count);
            for (int i = 0; i < vm.strStack.Count; ++i)
            {
                Console.Write("\"{0}\" ", vm.strStack[i]);
            }
            Console.WriteLine();
        }
        public static void ShowProgram(this LuaVM vm)
        {
            Console.WriteLine("program segment: PC {0}, AX {1}, StackTop {2}", vm.PC, vm.AX, vm.SP > 0 ? vm.datStack[vm.SP - 1].ToString() : "invalid");
            Array ISvalues = Enum.GetValues(LuaVM.IS.NOP.GetType());
            for (int i = 0; i < vm.program.Count; ++i)
            {
                string ins;
                int id = Array.IndexOf(ISvalues, (LuaVM.IS)vm.program[i]);
                if (id != -1)
                {
                    ins = String.Format("{0}", ISvalues.GetValue(id));
                }
                else
                {
                    ins = String.Format("{0}", vm.program[i]);
                }

                if (i == vm.PC)
                    ins = String.Format("[{0}]", ins);

                Console.Write(ins + " ");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// 栈顶元素出栈，将其转化为 String 类型，并将该新值压栈
        /// </summary>
        /// <param name="vm">虚拟机</param>
        /// <param name="t">待转化的变量类型</param>
        public static void StringFmt(ref LuaVM vm, LuaVM.VAR_TYPE t)
        {
            switch (t)
            {
                case LuaVM.VAR_TYPE.NIL:
                    vm.program.Add((int)LuaVM.IS.POP);
                    vm.program.Add((int)LuaVM.IS.IPUSH);
                    vm.program.Add(0);
                    break;
                case LuaVM.VAR_TYPE.INT:
                    vm.program.Add((int)LuaVM.IS.TOP);
                    vm.program.Add((int)LuaVM.IS.POP);
                    vm.program.Add((int)LuaVM.IS.STRFMT);
                    vm.program.Add((int)LuaVM.IS.PUSH);
                    break;
                case LuaVM.VAR_TYPE.STRING:
                    break;
            }
        }
    }
}
