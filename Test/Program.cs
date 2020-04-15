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
            Lua a = new Lua("Hello World!");

            Console.ReadKey();
            return;
        }
    }
}
