using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyLua
{
    public interface IAST
    {
        bool Generate(List<Token> tokens, ref int pos, int _level = 0);
        bool Compile(ref LuaVM vm);
    }

    public abstract class Statement : IAST
    {
        public int level = 0;
        public abstract bool Generate(List<Token> tokens, ref int pos, int _level = 0);
        public static bool TryGenerate(out Statement stmt, List<Token> tokens, ref int pos, int _level = 0)
        {
            if (pos >= tokens.Count)
            {
                stmt = null;
                return false;
            }
            int _pos = pos;
            switch (tokens[pos].type)
            {
                case TOKEN_TYPE.DO:
                    stmt = new DoStatement();
                    break;
                case TOKEN_TYPE.WHILE:
                    stmt = new WhileStatement();
                    break;
                case TOKEN_TYPE.FOR:
                    stmt = new ForStatement();
                    break;
                case TOKEN_TYPE.REPEAT:
                    stmt = new RepeatStatement();
                    break;
                case TOKEN_TYPE.IF:
                    stmt = new IfStatement();
                    break;
                case TOKEN_TYPE.LOCAL:
                    //stmt = new LocalStatement();
                    stmt = null;
                    return false;
                case TOKEN_TYPE.IDENT:
                default:
                    if (tokens[pos].type == TOKEN_TYPE.IDENT)
                    {
                        if (pos + 1 < tokens.Count && tokens[pos + 1].type == TOKEN_TYPE.ASSIGN)
                        {
                            stmt = new AssignmentStatement();
                        }
                        else
                        {
                            stmt = new ExpressionStatement();
                        }
                        break;
                    }
                    else
                    {
                            stmt = new ExpressionStatement();
                    }
                    break;
            }
            if(stmt.Generate(tokens, ref pos, _level))
            {
                return true;
            }
            else
            {
                pos = _pos;
                stmt = new NullStatement();
                return stmt.Generate(tokens, ref pos, _level);
            }
        }

        public abstract bool Compile(ref LuaVM vm);
    }
    public abstract class Expression : IAST
    {
        public int level = 0;
        public abstract bool Generate(List<Token> tokens, ref int pos, int _level);
        public static bool TryGenerate(out Expression expr, List<Token> tokens, ref int pos, int _level = 0)
        {
            int _pos = pos;;
            expr = null;
            Unit9 unit9 = new Unit9();
            if(!unit9.Generate(tokens, ref pos, _level))
            {
                pos = _pos;
                return false;
            }
            expr = unit9;
            return true;
        }

        public abstract int SymbolID(ref LuaVM vm);

        public abstract LuaVM.VAR_TYPE GetType(ref LuaVM vm);

        public abstract bool Compile(ref LuaVM vm);
    }

#region Chunk&Block
    public class Chunk : IAST
    {
        IAST block = new Block();

        public bool Compile(ref LuaVM vm)
        {
            return block.Compile(ref vm);
        }

        public bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            return block.Generate(tokens, ref pos, _level);
        }
        public override string ToString()
        {
            return block.ToString();
        }
    }

    public class Block : IAST
    {
        public List<Statement> stmts = new List<Statement>();
        public int level = 0;

        public bool Compile(ref LuaVM vm)
        {
            foreach (Statement statement in stmts)
            {
                if(!statement.Compile(ref vm)) return false;
            }
            return true;
        }

        public bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            level = _level;
            int _pos = pos;
            while (Statement.TryGenerate(out Statement stmt, tokens, ref pos, level))
            {
                stmts.Add(stmt);
                _pos = pos;
            }
            pos = _pos;
            return stmts.Count > 0;
        }
        public override string ToString()
        {
            string res = "";
            foreach (Statement s in stmts)
            {
                res += s.ToString();
            }
            return res;
        }
    }
#endregion

#region Statements
    public class DoStatement : Statement
    {
        Block block = new Block();

        public override bool Compile(ref LuaVM vm)
        {
            return block.Compile(ref vm);
        }

        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            level = _level;
            if (pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.DO) return false;
            ++pos;
            if(!block.Generate(tokens, ref pos, _level+1)) return false;
            if (pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.END) return false;
            ++pos;
            return true;
        }
        public override string ToString()
        {
            string padding = "";
            for(int i=0; i<level; ++i) padding += "  ";
            return String.Format("{0}do\n{1}{2}end\n", padding, block.ToString(), padding);
        }
    }

    public class WhileStatement : Statement
    {
        public Expression expr;
        public Block block = new Block();

        public override bool Compile(ref LuaVM vm)
        {
            /*
             * a:
             *     expr
             *     TOP
             *     POP
             *     JZ b
             *     block
             *     JMP a
             * b:
             */
            int a = vm.program.Count, b = 0;
            int pb;
            if(!expr.Compile(ref vm)) return false;
            vm.program.Add((int)LuaVM.IS.TOP);
            vm.program.Add((int)LuaVM.IS.POP);
            vm.program.Add((int)LuaVM.IS.JZ);
            pb = vm.program.Count;
            vm.program.Add(b);
            if(!block.Compile(ref vm)) return false;
            vm.program.Add((int)LuaVM.IS.JMP);
            vm.program.Add(a);
            b = vm.program.Count;
            vm.program[pb] = b;
            return true;
        }

        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            level = _level;
            if (pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.WHILE) return false;
            ++pos;
            if (!Expression.TryGenerate(out expr, tokens, ref pos, _level)) return false;
            if (pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.DO) return false;
            ++pos;
            if(!block.Generate(tokens, ref pos, _level+1)) return false;
            if (pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.END) return false;
            ++pos;
            return true;
        }
        public override string ToString()
        {
            string padding = "";
            for (int i = 0; i < level; ++i) padding += "  ";
            return String.Format("{0}while {1} do\n{2}{3}end\n", padding, expr.ToString(), block.ToString(), padding);
        }
    }

    public class ForStatement : Statement
    {
        public AssignmentStatement begin = new AssignmentStatement();
        public Expression end = null, step = null;
        public Block block = new Block();
        public override bool Compile(ref LuaVM vm)
        {
            /*
             *     begin
             * pe:
             *     end
             * ps:
             *     step
             * a:
             *      $v != end
             *      TOP
             *      POP
             *      JZ b
             *      block
             *      $v = $v + step
             *      JMP a
             * b:
             */
            if (!begin.Compile(ref vm)) return false;
            int id = begin.SymbolID(ref vm);
            if (!end.Compile(ref vm)) return false;

            if (step != null)
                step.Compile(ref vm);
            else
            {
                vm.program.Add((int)LuaVM.IS.IPUSH);
                vm.program.Add(1);
            }

            int a = vm.program.Count, b = 0;
            int pb;
            // 将 $v 压栈
            vm.program.Add((int)LuaVM.IS.IPUSH);
            vm.program.Add(vm.symbols[id].addr);
            vm.program.Add((int)LuaVM.IS.LFH);
            vm.program.Add((int)LuaVM.IS.POP);
            vm.program.Add((int)LuaVM.IS.PUSH);

            // 读入 end 到 AX
            vm.program.Add((int)LuaVM.IS.IPUSH);
            vm.program.Add(-3); // end
            vm.program.Add((int)LuaVM.IS.LFS);
            vm.program.Add((int)LuaVM.IS.POP);

            // AX 记录两者是否不同
            vm.program.Add((int)LuaVM.IS.NEQ);
            vm.program.Add((int)LuaVM.IS.TOP);
            vm.program.Add((int)LuaVM.IS.POP);

            // 相同则跳出循环
            vm.program.Add((int)LuaVM.IS.JZ);
            pb = vm.program.Count;
            vm.program.Add(b);

            block.Compile(ref vm);

            // 将 $v 压栈
            vm.program.Add((int)LuaVM.IS.IPUSH);
            vm.program.Add(vm.symbols[id].addr);
            vm.program.Add((int)LuaVM.IS.LFH);
            vm.program.Add((int)LuaVM.IS.POP);
            vm.program.Add((int)LuaVM.IS.PUSH);

            // 读入 step 到 AX
            vm.program.Add((int)LuaVM.IS.IPUSH);
            vm.program.Add(-2); // step
            vm.program.Add((int)LuaVM.IS.LFS);
            vm.program.Add((int)LuaVM.IS.POP);

            // 加和
            vm.program.Add((int)LuaVM.IS.ADD);
            vm.program.Add((int)LuaVM.IS.TOP);
            vm.program.Add((int)LuaVM.IS.POP);

            vm.program.Add((int)LuaVM.IS.IPUSH);
            vm.program.Add(vm.symbols[id].addr);
            vm.program.Add((int)LuaVM.IS.STH);
            vm.program.Add((int)LuaVM.IS.POP);

            vm.program.Add((int)LuaVM.IS.JMP);
            vm.program.Add(a);

            b = vm.program.Count;
            vm.program[pb] = b;

            vm.program.Add((int)LuaVM.IS.POP);
            vm.program.Add((int)LuaVM.IS.POP);


            return true;
        }
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            level = _level;
            if (pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.FOR) return false;
            ++pos;

            if (!begin.Generate(tokens, ref pos, _level)) return false;

            if (pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.COMMA) return false;
            ++pos;
            if (!Expression.TryGenerate(out end, tokens, ref pos)) return false;

            if (pos >= tokens.Count || tokens[pos].type == TOKEN_TYPE.COMMA)
            {
                ++pos;
                if (!Expression.TryGenerate(out step, tokens, ref pos)) return false;
            }

            if (pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.DO) return false;
            ++pos;

            if(!block.Generate(tokens, ref pos, _level+1)) return false;

            if (pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.END) return false;
            ++pos;
            return true;
        }
        public override string ToString()
        {
            string padding = "";
            for (int i = 0; i < level; ++i) padding += "  ";
            string text = String.Format("{0}for {1}, {2}", padding, begin.ToString(), end.ToString());
            if(step != null) text += ", " + step.ToString();
            text += " do\n" + block.ToString() + padding + "end\n";
            return text;
        }
    }

    public class RepeatStatement : Statement
    {
        public Expression expr;
        public Block block = new Block();
        public override bool Compile(ref LuaVM vm)
        {
            /*
             * a:
             *     block
             *     exp
             *     TOP
             *     POP
             *     JNZ b
             *     JMP a
             * b:
             */
            int a = vm.program.Count;
            block.Compile(ref vm);
            expr.Compile(ref vm);

            vm.program.Add((int)LuaVM.IS.TOP);
            vm.program.Add((int)LuaVM.IS.POP);

            vm.program.Add((int)LuaVM.IS.JNZ);
            int b = -1, pb = vm.program.Count;
            vm.program.Add(b);
            vm.program.Add((int)LuaVM.IS.JMP);
            vm.program.Add(a);
            b = vm.program.Count;
            vm.program[pb] = b;

            return true;
        }

        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            level = _level;
            if (pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.REPEAT) return false;
            ++pos;
            if(!block.Generate(tokens, ref pos, level + 1)) return false;
            if (pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.UNTIL) return false;
            ++pos;

            if (!Expression.TryGenerate(out expr, tokens, ref pos)) return false;
            return true;
        }
        public override string ToString()
        {
            string padding = "";
            for (int i = 0; i < level; ++i) padding += "  ";
            return String.Format("{0}repeat\n{2}until {3}\n", padding, block.ToString(), padding, expr.ToString());
        }
    }

    public class IfStatement : Statement
    {
        public List<Expression> conds = new List<Expression>();
        public List<Block> branches = new List<Block>();
        public Block elseBranch = new Block();
        public override bool Compile(ref LuaVM vm)
        {
            /*
             *     if-expr
             *     TOP
             *     POP
             *     JZ b[0]:
             *     if-block
             *     JMP a
             * b[0]:
             *     elseif-expr
             *     TOP
             *     POP
             *     JZ b[1]:
             *     elseif-block
             *     JMP a
             * b[1]:
             *     elseif-expr
             *     TOP
             *     POP
             *     JZ b[2]:
             *     elseif-block
             *     JMP a
             * b[2]:
             *     else-block
             * a:
             */
            int b = -1, pb;
            List<int> pas = new List<int>();
            for (int i = 0; i < conds.Count; ++i)
            {
                if(!conds[i].Compile(ref vm)) return false;
                vm.program.Add((int)LuaVM.IS.TOP);
                vm.program.Add((int)LuaVM.IS.POP);
                vm.program.Add((int)LuaVM.IS.JZ);
                pb = vm.program.Count;
                vm.program.Add(b);
                if(!branches[i].Compile(ref vm)) return false;
                vm.program.Add((int)LuaVM.IS.JMP);
                pas.Add(vm.program.Count);
                vm.program.Add(-1);
                b = vm.program.Count;
                vm.program[pb] = b;
            }
            if(!elseBranch.Compile(ref vm)) return false;
            int a = vm.program.Count;
            foreach (int i in pas)
            {
                vm.program[i] = a;
            }
            return true;
        }

        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            if (pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.IF) return false;
            ++pos;

            if (!Expression.TryGenerate(out Expression exp1, tokens, ref pos)) return false;
            conds.Add(exp1);

            if (pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.THEN) return false;
            ++pos;

            Block block1 = new Block();
            if (!block1.Generate(tokens, ref pos, level+1)) return false;
            branches.Add(block1);

            while (pos < tokens.Count && tokens[pos].type == TOKEN_TYPE.ELSEIF)
            {
                ++pos;

                if (!Expression.TryGenerate(out Expression exp2, tokens, ref pos)) return false;
                conds.Add(exp2);

                if (pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.THEN) return false;
                ++pos;

                Block block2 = new Block();
                if (!block2.Generate(tokens, ref pos, level + 1)) return false;
                branches.Add(block2);
            }

            if (pos >= tokens.Count || tokens[pos].type == TOKEN_TYPE.ELSE)
            {
                ++pos;
                if (!elseBranch.Generate(tokens, ref pos, level+1)) return false;
            }

            if (pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.END) return false;
            ++pos;

            return true;
        }
    }

    public class AssignmentStatement : Statement
    {
        public Expression target;
        public Expression val;
        public override bool Compile(ref LuaVM vm)
        {
            if(!target.Compile(ref vm)) return false;

            vm.program.Add((int)LuaVM.IS.POP);
            int id = target.SymbolID(ref vm);
            vm.program.Add((int)LuaVM.IS.IMOV);
            vm.program.Add(vm.symbols[id].addr);
            vm.program.Add((int)LuaVM.IS.PUSH);

            if(!val.Compile(ref vm)) return false;
            vm.symbols[id].type = val.GetType(ref vm);

            vm.program.Add((int)LuaVM.IS.TOP);
            vm.program.Add((int)LuaVM.IS.POP);
            vm.program.Add((int)LuaVM.IS.STH);
            vm.program.Add((int)LuaVM.IS.POP);
            return true;
        }
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            level = _level;
            if (!Expression.TryGenerate(out target, tokens, ref pos)) return false;


            if (pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.ASSIGN) return false;
            ++pos;

            if (!Expression.TryGenerate(out val, tokens, ref pos)) return false;

            return true;
        }
        public int SymbolID(ref LuaVM vm)
        {
            return target.SymbolID(ref vm);
        }
        public override string ToString()
        {
            string padding = "";
            for (int i = 0; i < level; ++i) padding += "  ";
            return String.Format("{0}{1} = {2}\n", padding, target.ToString(), val.ToString());
        }
    }
    public class ExpressionStatement : Statement
    {
        public Expression expr;
        public override bool Compile(ref LuaVM vm)
        {
            if(!expr.Compile(ref vm)) return false;
            vm.program.Add((int)LuaVM.IS.POP);
            return true;
        }
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            level = _level;
            if (!Expression.TryGenerate(out expr, tokens, ref pos)) return false;
            return true;
        }
        public override string ToString()
        {
            string padding = "";
            for (int i = 0; i < level; ++i) padding += "  ";
            return String.Format("{0}{1}\n", padding, expr.ToString());
        }
    }
    
    public class NullStatement : Statement
    {
        public static int last_pos = 0;
        public override bool Compile(ref LuaVM vm)
        {
            return true;
        }
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            if(last_pos == pos) return false;
            level = _level;
            last_pos = pos;
            return true;
        }
        public override string ToString()
        {
            return "";
        }
    }
#endregion

    public class Unit0 : Expression
    {
        public Token tk;
        Expression expr = null;
        public override bool Compile(ref LuaVM vm)
        {
            switch (tk.type)
            {
                case TOKEN_TYPE.IDENT:
                    {
                        int id = vm.GetSymbol((string)tk.value);
                        switch (vm.symbols[id].type)
                        {
                            case LuaVM.VAR_TYPE.NIL:
                                vm.program.Add((int)LuaVM.IS.IPUSH);
                                vm.program.Add(0);
                                break;
                            case LuaVM.VAR_TYPE.INT:
                                vm.program.Add((int)LuaVM.IS.IPUSH);
                                vm.program.Add(vm.symbols[id].addr);
                                vm.program.Add((int)LuaVM.IS.LFH);
                                vm.program.Add((int)LuaVM.IS.POP);
                                vm.program.Add((int)LuaVM.IS.PUSH);
                                break;
                            case LuaVM.VAR_TYPE.STRING:
                                vm.program.Add((int)LuaVM.IS.IPUSH);
                                vm.program.Add(vm.symbols[id].addr);
                                vm.program.Add((int)LuaVM.IS.LFH);
                                vm.program.Add((int)LuaVM.IS.POP);
                                vm.program.Add((int)LuaVM.IS.PUSH);
                                break;
                            case LuaVM.VAR_TYPE.FUNCTION:
                                vm.program.Add((int)LuaVM.IS.IPUSH);
                                vm.program.Add(vm.symbols[id].addr);
                                vm.program.Add((int)LuaVM.IS.LFH);
                                vm.program.Add((int)LuaVM.IS.POP);
                                vm.program.Add((int)LuaVM.IS.PUSH);
                                break;
                        }
                        break;
                    }
                case TOKEN_TYPE.NIL:
                    {
                        vm.program.Add((int)LuaVM.IS.IPUSH);
                        vm.program.Add(0);
                        break;
                    }
                case TOKEN_TYPE.NUMBER:
                    {
                        vm.program.Add((int)LuaVM.IS.IPUSH);
                        vm.program.Add((int)tk.value);
                        break;
                    }
                case TOKEN_TYPE.STRING:
                    {
                        int id = vm.strStack.Count;
                        vm.strStack.Add((string)tk.value);
                        vm.program.Add((int)LuaVM.IS.IPUSH);
                        vm.program.Add((int)id);
                        break;
                    }
                case TOKEN_TYPE.TRUE:
                    {
                        vm.program.Add((int)LuaVM.IS.IPUSH);
                        vm.program.Add(1);
                        break;
                    }
                case TOKEN_TYPE.FALSE:
                    {
                        vm.program.Add((int)LuaVM.IS.IPUSH);
                        vm.program.Add(0);
                        break;
                    }
                case TOKEN_TYPE.OPENPA:
                    {
                        expr.Compile(ref vm);
                        break;
                    }
                default:
                    return false;
            }
            return true;
        }
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            if(pos > tokens.Count) return false;

            switch (tokens[pos].type)
            {
                case TOKEN_TYPE.IDENT:
                case TOKEN_TYPE.NUMBER:
                case TOKEN_TYPE.STRING:
                case TOKEN_TYPE.TRUE:
                case TOKEN_TYPE.FALSE:
                    tk = tokens[pos];
                    ++pos;
                    break;
                case TOKEN_TYPE.OPENPA:
                    tk = tokens[pos];
                    ++pos;
                    if(!Expression.TryGenerate(out expr, tokens, ref pos, _level)) return false;
                    if(pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.CLOSEPA) return false;
                    ++pos;
                    break;
                default:
                    return false;
            }

            return true;
        }
        public override string ToString()
        {
            switch (tk.type)
            {
                case TOKEN_TYPE.IDENT:
                    return (string) tk.value;
                case TOKEN_TYPE.NUMBER:
                    return ((int)tk.value).ToString();
                case TOKEN_TYPE.STRING:
                    return "\"" + (string)tk.value + "\"";
                case TOKEN_TYPE.TRUE:
                    return "true";
                case TOKEN_TYPE.FALSE:
                    return "false";
                case TOKEN_TYPE.OPENPA:
                    return "( " + expr.ToString() + " )";
                default:
                    return String.Format("<Unepected Unit0 type={0}>", tk.type);
            }
        }
        public override int SymbolID(ref LuaVM vm)
        {
            if(expr == null)
                return tk.type == TOKEN_TYPE.IDENT ? vm.GetSymbol((string)tk.value) : -1;
            return expr.SymbolID(ref vm);
        }
        public override LuaVM.VAR_TYPE GetType(ref LuaVM vm)
        {
            if (expr == null)
            {
                switch (tk.type)
                {
                    case TOKEN_TYPE.IDENT:
                        return vm.symbols[vm.GetSymbol((string)tk.value)].type;
                    case TOKEN_TYPE.NUMBER:
                    case TOKEN_TYPE.TRUE:
                    case TOKEN_TYPE.FALSE:
                        return LuaVM.VAR_TYPE.INT;
                    case TOKEN_TYPE.STRING:
                        return LuaVM.VAR_TYPE.STRING;
                    default:
                        return LuaVM.VAR_TYPE.NIL;
                }
            }
            return expr.GetType(ref vm);
        }
    }


    public class Unit1 : Expression
    {
        public Unit0 a = new Unit0();
        Expression b = null;
        public Token op = null;
        public override bool Compile(ref LuaVM vm)
        {
            if (op == null)
            {
                if (!a.Compile(ref vm)) return false;
            }
            else
            {
                switch (op.type)
                {
                    case TOKEN_TYPE.OPENBR:
                        throw new NotImplementedException();
                        break;
                    case TOKEN_TYPE.OPENPA:
                        if (a.GetType(ref vm) != LuaVM.VAR_TYPE.FUNCTION) return false;
                        if (!b.Compile(ref vm)) return false;
                        if (!a.Compile(ref vm)) return false;
                        // 目前函数，默认返回 0
                        vm.program.Add((int)LuaVM.IS.CALLS); // 从栈上获取函数地址
                        vm.program.Add((int)LuaVM.IS.POP); // 弹出之前的压入的参数
                        vm.program.Add((int)LuaVM.IS.IPUSH);
                        vm.program.Add(0);
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            if (!a.Generate(tokens, ref pos, _level)) return false;

            if (pos < tokens.Count)
            {
                switch (tokens[pos].type)
                {
                    case TOKEN_TYPE.OPENPA:
                        op = tokens[pos];
                        ++pos;
                        if (!Expression.TryGenerate(out b, tokens, ref pos, _level)) return false;
                        if(pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.CLOSEPA) return false;
                        ++pos;
                        break;
                    case TOKEN_TYPE.OPENBR:
                        op = tokens[pos];
                        ++pos;
                        if (!Expression.TryGenerate(out b, tokens, ref pos, _level)) return false;
                        if (pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.CLOSEBR) return false;
                        ++pos;
                        break;
                    default:
                        break;
                }
            }

            return true;
        }
        public override string ToString()
        {
            if (op == null)
                return a.ToString();
            switch (op.type)
            {
                case TOKEN_TYPE.OPENPA:
                    return a.ToString() + " ( " + b.ToString() + " )";
                case TOKEN_TYPE.OPENBR:
                    return a.ToString() + " [ " + b.ToString() + " ]";
                default:
                    return String.Format("<Unepected Unit1 type={0}>", op.type);
            }
        }
        public override int SymbolID(ref LuaVM vm)
        {
            return op != null ? -1 : a.SymbolID(ref vm);
        }
        public override LuaVM.VAR_TYPE GetType(ref LuaVM vm)
        {
            return op == null ? a.GetType(ref vm) : LuaVM.VAR_TYPE.INT;
        }
    }
    public class Unit2 : Expression
    {
        public Unit1 a = new Unit1(), b = new Unit1();
        public Token op = null;
        public override bool Compile(ref LuaVM vm)
        {
            if (!a.Compile(ref vm)) return false;
            if (op != null)
            {
                switch (op.type)
                {
                    case TOKEN_TYPE.POW:
                        if (b.GetType(ref vm) == LuaVM.VAR_TYPE.STRING) return false;
                        if (!b.Compile(ref vm)) return false;
                        vm.program.Add((int)LuaVM.IS.TOP);
                        vm.program.Add((int)LuaVM.IS.POP);
                        vm.program.Add((int)LuaVM.IS.POW);
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            if (!a.Generate(tokens, ref pos, _level)) return false;

            if (pos < tokens.Count && tokens[pos].type == TOKEN_TYPE.POW)
            {
                op = tokens[pos];
                ++pos;
                if(!b.Generate(tokens, ref pos, _level)) return false;
            }

            return true;
        }
        public override string ToString()
        {
            if (op == null)
                return a.ToString();
            switch (op.type)
            {
                case TOKEN_TYPE.POW:
                    return a.ToString() + " ^ " + b.ToString();
                default:
                    return String.Format("<Unepected Unit2 type={0}>", op.type);
            }
        }
        public override int SymbolID(ref LuaVM vm)
        {
            return op != null ? -1 : a.SymbolID(ref vm);
        }
        public override LuaVM.VAR_TYPE GetType(ref LuaVM vm)
        {
            return op == null ? a.GetType(ref vm) : LuaVM.VAR_TYPE.INT;
        }
    }
    public class Unit3 : Expression
    {
        public Unit2 a = new Unit2();
        public Token op = null;
        public override bool Compile(ref LuaVM vm)
        {
            if (!a.Compile(ref vm)) return false;
            if (op != null)
            {
                switch (op.type)
                {
                    case TOKEN_TYPE.NOT:
                        if (a.GetType(ref vm) == LuaVM.VAR_TYPE.STRING) return false;
                        vm.program.Add((int)LuaVM.IS.TOP);
                        vm.program.Add((int)LuaVM.IS.POP);
                        vm.program.Add((int)LuaVM.IS.NOT);
                        break;
                    case TOKEN_TYPE.SUB:
                        if (a.GetType(ref vm) == LuaVM.VAR_TYPE.STRING) return false;
                        vm.program.Add((int)LuaVM.IS.TOP);
                        vm.program.Add((int)LuaVM.IS.POP);
                        vm.program.Add((int)LuaVM.IS.NEG);
                        break;
                    case TOKEN_TYPE.LENGTH:
                        if (a.GetType(ref vm) != LuaVM.VAR_TYPE.STRING) return false;
                        vm.program.Add((int)LuaVM.IS.TOP);
                        vm.program.Add((int)LuaVM.IS.POP);
                        vm.program.Add((int)LuaVM.IS.STRLEN);
                        vm.program.Add((int)LuaVM.IS.PUSH);
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {

            if (pos < tokens.Count && (tokens[pos].type == TOKEN_TYPE.NOT || tokens[pos].type == TOKEN_TYPE.LENGTH || tokens[pos].type == TOKEN_TYPE.SUB))
            {
                op = tokens[pos];
                ++pos;
            }

            if (!a.Generate(tokens, ref pos, _level)) return false;
            return true;
        }
        public override string ToString()
        {
            if (op == null)
                return a.ToString();
            switch (op.type)
            {
                case TOKEN_TYPE.NOT:
                    return "not " + a.ToString();
                case TOKEN_TYPE.LENGTH:
                    return "# " + a.ToString();
                case TOKEN_TYPE.SUB:
                    return "- " + a.ToString();
                default:
                    return String.Format("<Unepected Unit3 type={0}>", op.type);
            }
        }
        public override int SymbolID(ref LuaVM vm)
        {
            return op != null ? -1 : a.SymbolID(ref vm);
        }
        public override LuaVM.VAR_TYPE GetType(ref LuaVM vm)
        {
            return op == null ? a.GetType(ref vm) : LuaVM.VAR_TYPE.INT;
        }
    }

    public class Unit4 : Expression
    {
        public Unit3 a = new Unit3(), b = new Unit3();
        public Token op = null;
        public override bool Compile(ref LuaVM vm)
        {
            if (!a.Compile(ref vm)) return false;
            if (op != null)
            {
                if (!b.Compile(ref vm)) return false;
                if (a.GetType(ref vm) == LuaVM.VAR_TYPE.STRING || b.GetType(ref vm) == LuaVM.VAR_TYPE.STRING) return false;
                vm.program.Add((int)LuaVM.IS.TOP);
                vm.program.Add((int)LuaVM.IS.POP);
                switch (op.type)
                {
                    case TOKEN_TYPE.MUL:
                        vm.program.Add((int)LuaVM.IS.MUL);
                        break;
                    case TOKEN_TYPE.DIV:
                        vm.program.Add((int)LuaVM.IS.DIV);
                        break;
                    case TOKEN_TYPE.MOD:
                        vm.program.Add((int)LuaVM.IS.MOD);
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            if (!a.Generate(tokens, ref pos, _level)) return false;

            if (pos < tokens.Count && (tokens[pos].type == TOKEN_TYPE.MUL || tokens[pos].type == TOKEN_TYPE.DIV || tokens[pos].type == TOKEN_TYPE.MOD))
            {
                op = tokens[pos];
                ++pos;
                if (!b.Generate(tokens, ref pos, _level)) return false;
            }
            return true;
        }
        public override string ToString()
        {
            if (op == null)
                return a.ToString();
            switch (op.type)
            {
                case TOKEN_TYPE.MUL:
                    return a.ToString() + " * " + b.ToString();
                case TOKEN_TYPE.DIV:
                    return a.ToString() + " / " + b.ToString();
                case TOKEN_TYPE.MOD:
                    return a.ToString() + " % " + b.ToString();
                default:
                    return String.Format("<Unepected Unit4 type={0}>", op.type);
            }
        }
        public override int SymbolID(ref LuaVM vm)
        {
            return op != null ? -1 : a.SymbolID(ref vm);
        }
        public override LuaVM.VAR_TYPE GetType(ref LuaVM vm)
        {
            return op == null ? a.GetType(ref vm) : LuaVM.VAR_TYPE.INT;
        }
    }
    public class Unit5 : Expression
    {
        public Unit4 a = new Unit4(), b = new Unit4();
        public Token op = null;
        public override bool Compile(ref LuaVM vm)
        {
            if (!a.Compile(ref vm)) return false;
            if (op != null)
            {
                LuaVM.VAR_TYPE at = a.GetType(ref vm);
                LuaVM.VAR_TYPE bt = b.GetType(ref vm);
                if (op.type == TOKEN_TYPE.ADD)
                {
                    if (!b.Compile(ref vm)) return false;
                    if ((at == LuaVM.VAR_TYPE.NIL || at == LuaVM.VAR_TYPE.INT) && (bt == LuaVM.VAR_TYPE.NIL || bt == LuaVM.VAR_TYPE.INT))
                    {
                        vm.program.Add((int)LuaVM.IS.TOP);
                        vm.program.Add((int)LuaVM.IS.POP);
                        vm.program.Add((int)LuaVM.IS.ADD);
                        return true;
                    }
                    if (at == LuaVM.VAR_TYPE.STRING && bt == LuaVM.VAR_TYPE.INT)
                    {
                        vm.program.Add((int)LuaVM.IS.IPUSH);
                        vm.program.Add(-2);
                        vm.program.Add((int)LuaVM.IS.LFS);
                        vm.program.Add((int)LuaVM.IS.POP);
                        vm.program.Add((int)LuaVM.IS.STRADD);
                        return true;
                    }


                }
                else if (op.type == TOKEN_TYPE.SUB)
                {
                    if (!b.Compile(ref vm)) return false;
                    vm.program.Add((int)LuaVM.IS.TOP);
                    vm.program.Add((int)LuaVM.IS.POP);
                    if (at == LuaVM.VAR_TYPE.STRING || bt == LuaVM.VAR_TYPE.STRING) return false;
                    vm.program.Add((int)LuaVM.IS.SUB);
                }
                else
                {
                    return false;
                }
            }
            return true;
        }
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            if (!a.Generate(tokens, ref pos, _level)) return false;

            if (pos < tokens.Count && (tokens[pos].type == TOKEN_TYPE.ADD || tokens[pos].type == TOKEN_TYPE.SUB))
            {
                op = tokens[pos];
                ++pos;
                if (!b.Generate(tokens, ref pos, _level)) return false;
            }
            return true;
        }
        public override string ToString()
        {
            if (op == null)
                return a.ToString();
            switch (op.type)
            {
                case TOKEN_TYPE.ADD:
                    return a.ToString() + " + " + b.ToString();
                case TOKEN_TYPE.SUB:
                    return a.ToString() + " - " + b.ToString();
                default:
                    return String.Format("<Unepected Unit5 type={0}>", op.type);
            }
        }
        public override int SymbolID(ref LuaVM vm)
        {
            return op != null ? -1 : a.SymbolID(ref vm);
        }
        public override LuaVM.VAR_TYPE GetType(ref LuaVM vm)
        {
            return op == null ? a.GetType(ref vm) : LuaVM.VAR_TYPE.INT;
        }
    }
    public class Unit6 : Expression
    {
        public Unit5 a = new Unit5(), b = new Unit5();
        public Token op = null;
        public override bool Compile(ref LuaVM vm)
        {
            if (!a.Compile(ref vm)) return false;
            // string a
            if (op != null)
            {
                LuaVMHelper.StringFmt(ref vm, a.GetType(ref vm));
                if (!b.Compile(ref vm)) return false;
                LuaVMHelper.StringFmt(ref vm, b.GetType(ref vm));
                // string b
                vm.program.Add((int)LuaVM.IS.IPUSH);
                vm.program.Add(-2);
                vm.program.Add((int)LuaVM.IS.LFS);
                vm.program.Add((int)LuaVM.IS.POP);
                // ax = string a
                switch (op.type)
                {
                    case TOKEN_TYPE.DOTDOT:
                        vm.program.Add((int)LuaVM.IS.STRCON);
                        vm.program.Add((int)LuaVM.IS.POP);
                        vm.program.Add((int)LuaVM.IS.PUSH);
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            if (!a.Generate(tokens, ref pos, _level)) return false;

            if (pos < tokens.Count && tokens[pos].type == TOKEN_TYPE.DOTDOT)
            {
                op = tokens[pos];
                ++pos;
                if (!b.Generate(tokens, ref pos, _level)) return false;
            }
            return true;
        }
        public override string ToString()
        {
            if (op == null)
                return a.ToString();
            switch (op.type)
            {
                case TOKEN_TYPE.DOTDOT:
                    return a.ToString() + " .. " + b.ToString();
                default:
                    return String.Format("<Unepected Unit6 type={0}>", op.type);
            }
        }
        public override int SymbolID(ref LuaVM vm)
        {
            return op != null ? -1 : a.SymbolID(ref vm);
        }
        public override LuaVM.VAR_TYPE GetType(ref LuaVM vm)
        {
            return op == null ? a.GetType(ref vm) : LuaVM.VAR_TYPE.STRING;
        }
    }
    public class Unit7 : Expression
    {
        public Unit6 a = new Unit6(), b = new Unit6();
        public Token op = null;
        public override bool Compile(ref LuaVM vm)
        {
            if(!a.Compile(ref vm)) return false;
            if (op != null)
            {
                if(!b.Compile(ref vm)) return false;
                vm.program.Add((int)LuaVM.IS.TOP);
                vm.program.Add((int)LuaVM.IS.POP);
                switch (op.type)
                {
                    case TOKEN_TYPE.EQ:
                        vm.program.Add((int)LuaVM.IS.EQ);
                        break;
                    case TOKEN_TYPE.NEQ:
                        vm.program.Add((int)LuaVM.IS.NEQ);
                        break;
                    case TOKEN_TYPE.LT:
                        vm.program.Add((int)LuaVM.IS.LT);
                        break;
                    case TOKEN_TYPE.LE:
                        vm.program.Add((int)LuaVM.IS.LE);
                        break;
                    case TOKEN_TYPE.GT:
                        vm.program.Add((int)LuaVM.IS.GT);
                        break;
                    case TOKEN_TYPE.GE:
                        vm.program.Add((int)LuaVM.IS.GE);
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            if (!a.Generate(tokens, ref pos, _level)) return false;

            if (pos < tokens.Count && (tokens[pos].type >= TOKEN_TYPE.EQ && tokens[pos].type < TOKEN_TYPE.GE + 1))
            {
                op = tokens[pos];
                ++pos;

                if (!b.Generate(tokens, ref pos, _level)) return false;
            }
            return true;
        }
        public override string ToString()
        {
            if (op == null)
                return a.ToString();
            switch (op.type)
            {
                case TOKEN_TYPE.EQ:
                    return a.ToString() + " == " + b.ToString();
                case TOKEN_TYPE.NEQ:
                    return a.ToString() + " ~= " + b.ToString();
                case TOKEN_TYPE.LT:
                    return a.ToString() + " < " + b.ToString();
                case TOKEN_TYPE.LE:
                    return a.ToString() + " <= " + b.ToString();
                case TOKEN_TYPE.GT:
                    return a.ToString() + " > " + b.ToString();
                case TOKEN_TYPE.GE:
                    return a.ToString() + " >= " + b.ToString();
                default:
                    return String.Format("<Unepected Unit7 type={0}>", op.type);
            }
        }
        public override int SymbolID(ref LuaVM vm)
        {
            return op != null ? -1 : a.SymbolID(ref vm);
        }
        public override LuaVM.VAR_TYPE GetType(ref LuaVM vm)
        {
            return op == null ? a.GetType(ref vm) : LuaVM.VAR_TYPE.INT;
        }
    }
    public class Unit8 : Expression
    {
        public Unit7 a = new Unit7(), b = new Unit7();
        public Token op = null;
        public override bool Compile(ref LuaVM vm)
        {
            if (!a.Compile(ref vm)) return false;
            if (op != null)
            {
                if (!b.Compile(ref vm)) return false;
                vm.program.Add((int)LuaVM.IS.AND);
            }
            return true;
        }
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            if (!a.Generate(tokens, ref pos, _level)) return false;

            if (pos < tokens.Count && tokens[pos].type == TOKEN_TYPE.AND)
            {
                op = tokens[pos];
                ++pos;
                if (!b.Generate(tokens, ref pos, _level)) return false;
            }
            return true;
        }
        public override string ToString()
        {
            if (op == null)
                return a.ToString();
            return a.ToString() + " and " + b.ToString();
        }
        public override int SymbolID(ref LuaVM vm)
        {
            return op != null ? -1 : a.SymbolID(ref vm);
        }

        public override LuaVM.VAR_TYPE GetType(ref LuaVM vm)
        {
            return op == null ? a.GetType(ref vm) : LuaVM.VAR_TYPE.INT;
        }
    }
    public class Unit9 : Expression
    {
        public Unit8 a = new Unit8(), b = new Unit8();
        public Token op = null;
        public override bool Compile(ref LuaVM vm)
        {
            if(!a.Compile(ref vm)) return false;
            if (op != null)
            {
                if(!b.Compile(ref vm)) return false;
                vm.program.Add((int)LuaVM.IS.OR);
            }
            return true;
        }
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            if (!a.Generate(tokens, ref pos, _level)) return false;

            if (pos < tokens.Count && tokens[pos].type == TOKEN_TYPE.OR)
            {
                op = tokens[pos];
                ++pos;
                if (!b.Generate(tokens, ref pos, _level)) return false;
            }
            return true;
        }

        public override string ToString()
        {
            if(op == null)
                return a.ToString();
            return a.ToString() + " or " + b.ToString();
        }
        public override int SymbolID(ref LuaVM vm)
        {
            return op != null ? -1 : a.SymbolID(ref vm);
        }

        public override LuaVM.VAR_TYPE GetType(ref LuaVM vm)
        {
            return op == null ? a.GetType(ref vm) : LuaVM.VAR_TYPE.INT;
        }
    }
}
