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
                            stmt = new OtherStatement();
                        }
                        break;
                    }
                    else
                    {
                        stmt = new OtherStatement();
                    }
                    break;
            }
            return stmt.Generate(tokens, ref pos);
        }
    }
    public abstract class Expression : IAST
    {
        public abstract bool Generate(List<Token> tokens, ref int pos, int _level);
        public static bool TryGenerate(out Expression expr, List<Token> tokens, ref int pos, int _level = 0)
        {
            int _pos = pos;
            expr = null;
            Unit9 unit9 = new Unit9();
            if(!unit9.Generate(tokens, ref pos))
            {
                pos = _pos;
                return false;
            }
            expr = unit9;
            return true;
        }

        public int SymbolID()
        {
            return -1;
        }
    }

    public class Chunk : IAST
    {
        IAST block = new Block();
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

    public class DoStatement : Statement
    {
        Block block = new Block();
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
            return String.Format("{0}do\n{1}\n{2}end\n", padding, block.ToString(), padding);
        }
    }

    public class WhileStatement : Statement
    {
        public Expression expr;
        public Block block = new Block();
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
            return String.Format("{0}while {1} do\n{2}\n{3}end\n", padding, expr.ToString(), block.ToString(), padding);
        }
    }

    public class ForStatement : Statement
    {
        public AssignmentStatement begin = new AssignmentStatement();
        public Expression end = null, step = null;
        public Block block = new Block();
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            level = _level;
            if (pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.FOR) return false;
            ++pos;

            if (!begin.Generate(tokens, ref pos)) return false;

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
            text += " do\n" + block.ToString() + "\n" + padding + "end\n";
            return text;
        }
    }

    public class RepeatStatement : Statement
    {
        public Expression expr;
        public Block block = new Block();

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
            return String.Format("{0}repeat\n{1}\n{2}until {3}\n", padding, block.ToString(), padding, expr.ToString());
        }
    }

    public class IfStatement : Statement
    {
        public List<Expression> conds = new List<Expression>();
        public List<Block> branches = new List<Block>();
        public Block elseBranch = new Block();

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

        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            level = _level;
            if (!Expression.TryGenerate(out target, tokens, ref pos)) return false;


            if (pos >= tokens.Count || tokens[pos].type != TOKEN_TYPE.ASSIGN) return false;
            ++pos;

            if (!Expression.TryGenerate(out val, tokens, ref pos)) return false;

            return true;
        }
        public override string ToString()
        {
            string padding = "";
            for (int i = 0; i < level; ++i) padding += "  ";
            return String.Format("{0}{1} = {2}\n", padding, target.ToString(), val.ToString());
        }
    }
    public class OtherStatement : Statement
    {
        public Expression expr;

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

    public class Unit0 : Expression
    {
        public Token tk;
        Expression expr = null;
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
    }


    public class Unit1 : Expression
    {
        public Unit0 a = new Unit0();
        Expression b = null;
        public Token op = null;
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            if (!a.Generate(tokens, ref pos)) return false;

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
    }
    public class Unit2 : Expression
    {
        public Unit1 a = new Unit1(), b = new Unit1();
        public Token op = null;
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            if (!a.Generate(tokens, ref pos)) return false;

            if (pos < tokens.Count && tokens[pos].type == TOKEN_TYPE.POW)
            {
                op = tokens[pos];
                ++pos;
                if(!b.Generate(tokens, ref pos)) return false;
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
    }
    public class Unit3 : Expression
    {
        public Unit2 a = new Unit2();
        public Token op = null;
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {

            if (pos < tokens.Count && (tokens[pos].type == TOKEN_TYPE.NOT || tokens[pos].type == TOKEN_TYPE.LENGTH || tokens[pos].type == TOKEN_TYPE.SUB))
            {
                op = tokens[pos];
                ++pos;
            }

            if (!a.Generate(tokens, ref pos)) return false;
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
    }

    public class Unit4 : Expression
    {
        public Unit3 a = new Unit3(), b = new Unit3();
        public Token op = null;
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            if (!a.Generate(tokens, ref pos)) return false;

            if (pos < tokens.Count && (tokens[pos].type == TOKEN_TYPE.MUL || tokens[pos].type == TOKEN_TYPE.DIV || tokens[pos].type == TOKEN_TYPE.MOD))
            {
                op = tokens[pos];
                ++pos;
                if (!b.Generate(tokens, ref pos)) return false;
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
    }
    public class Unit5 : Expression
    {
        public Unit4 a = new Unit4(), b = new Unit4();
        public Token op = null;
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            if (!a.Generate(tokens, ref pos)) return false;

            if (pos < tokens.Count && (tokens[pos].type == TOKEN_TYPE.ADD || tokens[pos].type == TOKEN_TYPE.SUB))
            {
                op = tokens[pos];
                ++pos;
                if (!b.Generate(tokens, ref pos)) return false;
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
    }
    public class Unit6 : Expression
    {
        public Unit5 a = new Unit5(), b = new Unit5();
        public Token op = null;
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            if (!a.Generate(tokens, ref pos)) return false;

            if (pos < tokens.Count && tokens[pos].type == TOKEN_TYPE.DOTDOT)
            {
                op = tokens[pos];
                ++pos;
                if (!b.Generate(tokens, ref pos)) return false;
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
    }
    public class Unit7 : Expression
    {
        public Unit6 a = new Unit6(), b = new Unit6();
        public Token op = null;
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            if (!a.Generate(tokens, ref pos)) return false;

            if (pos < tokens.Count && (tokens[pos].type >= TOKEN_TYPE.EQ && tokens[pos].type < TOKEN_TYPE.GE + 1))
            {
                op = tokens[pos];
                ++pos;

                if (!b.Generate(tokens, ref pos)) return false;
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
    }
    public class Unit8 : Expression
    {
        public Unit7 a = new Unit7(), b = new Unit7();
        public Token op = null;
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            if (!a.Generate(tokens, ref pos)) return false;

            if (pos < tokens.Count && tokens[pos].type == TOKEN_TYPE.AND)
            {
                op = tokens[pos];
                ++pos;
                if (!b.Generate(tokens, ref pos)) return false;
            }
            return true;
        }
        public override string ToString()
        {
            if (op == null)
                return a.ToString();
            return a.ToString() + " and " + b.ToString();
        }
    }
    public class Unit9 : Expression
    {
        public Unit8 a = new Unit8(), b = new Unit8();
        public Token op = null;
        public override bool Generate(List<Token> tokens, ref int pos, int _level = 0)
        {
            if (!a.Generate(tokens, ref pos)) return false;

            if (pos < tokens.Count && tokens[pos].type == TOKEN_TYPE.OR)
            {
                op = tokens[pos];
                ++pos;
                if (!b.Generate(tokens, ref pos)) return false;
            }
            return true;
        }

        public override string ToString()
        {
            if(op == null)
                return a.ToString();
            return a.ToString() + " or " + b.ToString();
        }
    }
}
