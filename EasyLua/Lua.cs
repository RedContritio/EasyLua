using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyLua
{
    public enum TOKEN_TYPE
    {
        ERROR = -2, EOF = -1,

        NUMBER = 128, STRING, IDENT,

        IF, ELSEIF, ELSE,
        GOTO, BREAK,
        FOR, WHILE, REPEAT, UNTIL,
        THEN, DO, END,
        TRUE, FALSE, NIL,
        AND, OR, NOT, IN,
        LOCAL, FUNCTION, RETURN,

        ASSIGN, ADD, SUB, MUL, DIV, MOD, POW,
        LENGTH, DOTDOT,
        EQ, NEQ, LT, LE, GT, GE,
        OPENPA, CLOSEPA, OPENBR, CLOSEBR,
        COMMA
    }

    public class Token
    {
        private static readonly EnumHelper<TOKEN_TYPE> helper = new EnumHelper<TOKEN_TYPE>();

        public int row, col;
        public TOKEN_TYPE type;
        public object value = null;
        public Token(TOKEN_TYPE _type, int _row, int _col, object _value = null)
        {
            row = _row; col = _col;
            type = _type; value = _value;
        }
        public static Token convert(Token src)
        {
            if (src.type != TOKEN_TYPE.IDENT || src.value == null) return src;
            int v = helper.getTokenValue((string)src.value);
            if (v != -1) return new Token((TOKEN_TYPE)v, src.row, src.col);
            return src;
        }

        public override string ToString()
        {
            if (type == TOKEN_TYPE.IDENT || type == TOKEN_TYPE.STRING)
                return String.Format("<{0}, {1}>", helper.getTokenName((int)type), (string)value);
            if (type == TOKEN_TYPE.NUMBER)
                return String.Format("<{0}, {1}>", helper.getTokenName((int)type), (int)value);
            else
                return String.Format("<{0}>", helper.getTokenName((int)type));
        }
    }
    public class Lua
    {
        public List<Token> tokens;
        public IAST ast = new Chunk();
        public Lua(string script)
        {
            tokens = Tokenize(script);
        }

        public bool Compile()
        {
            int pos = 0;
            return ast.Generate(tokens, ref pos);
        }
        public void Run()
        {
            return ;
        }

        private static List<Token> Tokenize(String text)
        {
            List<Token> tokens = new List<Token>();
            List<string> lines = text.Split('\n').ToList<string>();
            int row = 0, col = 0;
            Token t;
            do
            {
                t = Next(lines, ref col, ref row);
                if (t.type < 0) break;
                tokens.Add(Token.convert(t));
            } while (true);
            return tokens;
        }

        private static Token Next(List<string> lines, ref int col, ref int row)
        {
            int last_pos;
            int token_val;
            while (row < lines.Count)
            {
                last_pos = col;
                // 处理换行
                if (col >= lines[row].Length)
                {
                    ++row;
                    col = 0;
                    continue;
                }

                char ch = lines[row][col];
                ++col;

                
                // 处理标识符
                if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch == '_'))
                {
                    // parse identifier

                    while (col < lines[row].Length && ((lines[row][col] >= 'a' && lines[row][col] <= 'z') || (lines[row][col] >= 'A' && lines[row][col] <= 'Z') || (lines[row][col] >= '0' && lines[row][col] <= '9') || (lines[row][col] == '_')))
                    {
                        ++col;
                    }

                    string name = lines[row].Substring(last_pos, col - last_pos);
                    return new Token(TOKEN_TYPE.IDENT, row, last_pos, name);
                }
                // 处理常数
                else if (ch >= '0' && ch <= '9')
                {
                    token_val = ch - '0';

                    while (col < lines[row].Length && lines[row][col] >= '0' && lines[row][col] <= '9')
                    {
                        token_val = token_val * 10 + lines[row][col] - '0';
                        ++col;
                    }
                    // token_val 表示其值

                    return new Token(TOKEN_TYPE.NUMBER, row, last_pos, token_val);
                }
                // 处理字符串
                else if (ch == '\"' || ch == '\'')
                {
                    // 字符串，仅接受 \n 的转义
                    string text = "";
                    while (col < lines[row].Length && lines[row][col] != ch)
                    {
                        token_val = lines[row][col];
                        ++col;
                        if (token_val == '\\')
                        {
                            // 斜杠后面有字符就尝试转义，不然就放弃分析
                            if (col < lines[row].Length)
                            {
                                token_val = lines[row][col];
                                ++col;
                                if (token_val == 'n')
                                {
                                    token_val = '\n';
                                }
                            }
                            else
                            {
                                return new Token(TOKEN_TYPE.EOF, row, last_pos, "Unexpected Error in Character Escape.");
                            }
                        }

                        text += (char)token_val;
                    }

                    ++col;
                    return new Token(TOKEN_TYPE.STRING, row, last_pos, text);
                }
                // 单行注释
                else if (ch == '-' && col < lines[row].Length && lines[row][col] == '-')
                {
                    ++col;
                    if (col + 1 < lines[row].Length && lines[row][col] == '[' && lines[row][col + 1] == '[')
                    {
                        col += 2;
                        while (row < lines.Count)
                        {
                            if (lines[row].Substring(col).Contains("--]]"))
                            {
                                col = lines[row].IndexOf("--]]", col) + 4;
                                break;
                            }
                            else
                            {
                                ++row;
                                col = 0;
                            }
                        }
                    }
                    else
                    {
                        ++row;
                        col = 0;
                    }
                }
                else if (ch == '.' && col < lines[row].Length && lines[row][col] == '.')
                    return new Token(TOKEN_TYPE.DOTDOT, row, (++col) - 2, null);
                else if (ch == '=' && col < lines[row].Length && lines[row][col] == '=')
                    return new Token(TOKEN_TYPE.EQ, row, (++col) - 2, null);
                else if (ch == '~' && col < lines[row].Length && lines[row][col] == '=')
                    return new Token(TOKEN_TYPE.NEQ, row, (++col) - 2, null);
                else if (ch == '<' && col < lines[row].Length && lines[row][col] == '=')
                    return new Token(TOKEN_TYPE.LE, row, (++col) - 2, null);
                else if (ch == '>' && col < lines[row].Length && lines[row][col] == '=')
                    return new Token(TOKEN_TYPE.GE, row, (++col) - 2, null);
                else if (ch == '=') { return new Token(TOKEN_TYPE.ASSIGN, row, col - 1, null); }

                else if (ch == '+') { return new Token(TOKEN_TYPE.ADD, row, col - 1, null); }
                else if (ch == '-') { return new Token(TOKEN_TYPE.SUB, row, col - 1, null); }
                else if (ch == '*') { return new Token(TOKEN_TYPE.MUL, row, col - 1, null); }
                else if (ch == '/') { return new Token(TOKEN_TYPE.DIV, row, col - 1, null); }
                else if (ch == '%') { return new Token(TOKEN_TYPE.MOD, row, col - 1, null); }
                else if (ch == '^') { return new Token(TOKEN_TYPE.POW, row, col - 1, null); }

                else if (ch == '<') { return new Token(TOKEN_TYPE.LT, row, col - 1, null); }
                else if (ch == '>') { return new Token(TOKEN_TYPE.GT, row, col - 1, null); }

                else if (ch == '#') { return new Token(TOKEN_TYPE.LENGTH, row, col - 1, null); }

                else if (ch == '(') { return new Token(TOKEN_TYPE.OPENPA, row, col - 1, null); }
                else if (ch == ')') { return new Token(TOKEN_TYPE.CLOSEPA, row, col - 1, null); }
                else if (ch == '[') { return new Token(TOKEN_TYPE.OPENBR, row, col - 1, null); }
                else if (ch == ']') { return new Token(TOKEN_TYPE.CLOSEBR, row, col - 1, null); }

                else if (ch == ',') { return new Token(TOKEN_TYPE.COMMA, row, col - 1, null); }
            }
            return new Token(TOKEN_TYPE.EOF, row, col - 1, "End Of File.");
        }
    }

    public class EnumHelper<T>
    {
        private Array ISvalues = Enum.GetValues(typeof(T));
        private Dictionary<int, string> dict_tk2str = new Dictionary<int, string>();
        private Dictionary<string, int> dict_str2tk = new Dictionary<string, int>();
        public EnumHelper()
        {
            foreach (var value in ISvalues)
            {
                dict_tk2str.Add(Convert.ToInt32(value), value.ToString());
                dict_str2tk.Add(value.ToString(), Convert.ToInt32(value));
            }
        }
        public string getTokenName(int tk)
        {
            if(dict_tk2str.ContainsKey(tk))
                return dict_tk2str[tk];
            return null;
        }
        public int getTokenValue(string name)
        {
            if(dict_str2tk.ContainsKey(name))
                return dict_str2tk[name];
            return -1;
        }
    }
}
