using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Agent.Generator;

namespace Agent
{
    class Program
    {
        static void Main(string[] args)
        {
            if (0 == args.Length)
            {
                Console.WriteLine(
    @"Immutable / mutable type pairing code generator.

First, in each type that you want to make immutable, add:
#region immutable_declarations
    private readonly <type> <field name>;
#endregion

Then add
#region immutable_generated
#endregion

Helper classes, methods, properties, ect, will be generated.
No external dependancies are added.
All private fields must be:
* readonly
* of an immutable type:
  * int, long, GUID, ect
  * string
  * A type that overloads == and != (signals immutable in .Net)
* (Type Name)_Collection: (An auto-generated collection type)
");
                return;
            }

            foreach (var arg in args)
            {
                Console.WriteLine("Generating for solution: {0}", arg);
                var immutableCompleter = new DiskImmutableCompleter(arg);
                immutableCompleter.Generate();
            }

            if (System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine("Whack a key!!!");
                Console.ReadKey();
            }
        }
    }
}
