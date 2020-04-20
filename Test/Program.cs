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
            Lua a = new Lua(
                "i = print\n" +
                "do\n" +
                "print = \"Hello\"\n" +
                "print = print .. \" World!\"\n" +
                "end\n" +
                "i(print)");

            Console.WriteLine("Compiling ... {0}", a.Compile());

            Console.WriteLine(a.ast);

            Console.WriteLine("Running ... ");

            a.vm.debug = true;
            a.Run();


            Console.ReadKey();
            return;
        }
    }
}
