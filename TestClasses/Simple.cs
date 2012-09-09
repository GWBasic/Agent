using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TestClasses
{
    public class Simple
    {
        #region immutable_declarations
        private readonly int intVal;
        private readonly long longVal;
        private readonly string stringVal;
        #endregion

        #region immutable_generated

        // Generated code, do not edit unless you know what you're doing!
        public int IntVal { get { return this.intVal; }}
        public Simple SetIntVal(int intVal) { return new Simple(intVal, this.longVal, this.stringVal); }

        public long LongVal { get { return this.longVal; }}
        public Simple SetLongVal(long longVal) { return new Simple(this.intVal, longVal, this.stringVal); }

        public string StringVal { get { return this.stringVal; }}
        public Simple SetStringVal(string stringVal) { return new Simple(this.intVal, this.longVal, stringVal); }


        public Simple(int intVal = default(int), long longVal = default(long), string stringVal = default(string)) { this.intVal = intVal; this.longVal = longVal; this.stringVal = stringVal; }

        public class Mutable { public int IntVal { get; set; } public long LongVal { get; set; } public string StringVal { get; set; } public Simple ToImmutable() { return new Simple(this.IntVal, this.LongVal, this.StringVal);} }
        public Mutable ToMutable() { return new Mutable() { IntVal = this.intVal, LongVal = this.longVal, StringVal = this.stringVal}; }

        #endregion

        public static void TestConventions()
        {
            Simple s;

            s = new Simple();

            s = new Simple(
                intVal: 3,
                longVal: 8,
                stringVal: "gregsgsd");

            s = new Simple(
                longVal: 90);

            s = s.SetIntVal(345).SetLongVal(7543).SetStringVal("htrsgdsgdsf");

            var mutable = s.ToMutable();
            mutable.IntVal = 566;
            mutable.LongVal = 856;
            mutable.StringVal = "j67rhdhdty";

            s = mutable.ToImmutable();
        }
    }
}