using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EasyLua;

namespace Test
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Lua a = new Lua("i = 1 - - 2333");

            foreach (var tk in a.tokens)
            {
                Console.Write(tk + " ");
            }

            a.Run();

            Console.ReadKey();
            return;
        }
    }
}
