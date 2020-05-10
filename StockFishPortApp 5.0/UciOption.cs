using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace StockFish
{
    public delegate void OnChangeOption(Option opt);

    public sealed class Option
    {
        public string defaultValue, currentValue, type;
        public int min = 0, max = 0;
        public int idx;
        public string name;

        public OnChangeOption on_change = null;

        public sealed class OptionComparer : IComparer<Option>
        {
            int IComparer<Option>.Compare(Option x, Option y)
            {
                return x.idx.CompareTo(y.idx);
            }
        }

        public Option(string name, int indice, OnChangeOption f=null)
        {
            this.name = name;
            type = "button";
            min = 0;
            max = 0;
            idx = indice;
            on_change = f;
        }

        public Option(string name, int indice, bool v, OnChangeOption f=null)
        {
            this.name = name;
            type = "check";
            min = 0;
            max = 0;
            idx = indice;
            on_change = f;
            defaultValue = currentValue = (v ? "true" : "false");
        }

        public Option(string name, int indice, string v, OnChangeOption f=null)
        {
            this.name = name;
            type = "string";
            min = 0;
            max = 0;
            idx = indice;
            on_change = f;
            defaultValue = currentValue = v;
        }

        public Option(string name, int indice, int v, int minv, int maxv, OnChangeOption f=null)
        {
            this.name = name;
            type = "spin";
            min = minv;
            max = maxv;
            idx = indice;
            on_change = f;
            defaultValue = currentValue = v.ToString();
        }

        public int getInt()
        {
            Debug.Assert(type == "check" || type == "spin");
            return (type == "spin" ? Convert.ToInt32(currentValue) : ((currentValue == "true")?1:0));
        }

        public String getString()
        {
            //Debug.Assert(type == "string");
            return currentValue;
        }

        /// <summary>
        /// operator=() updates currentValue and triggers on_change() action. It's up to
        /// the GUI to check for option's limits, but we could receive the new value from
        /// the user by console window, so let's check the bounds anyway.
        /// </summary>
        public Option setCurrentValue(string v)
        {
            Debug.Assert(!String.IsNullOrWhiteSpace(type));

            if (((type != "button") && (v == null || String.IsNullOrEmpty(v)))
               || ((type == "check") && v != "true" && v != "false")
               || ((type == "spin") && (int.Parse(v) < min || int.Parse(v) > max)))
                return this;

            if (type != "button")
                currentValue = v;

            if (on_change != null)
                on_change(this);

            return this;
        }
    }

    public sealed partial class Uci
    {
        // operator<<() is used to print all the options default values in chronological
        // insertion order (the idx field) and in the format defined by the UCI protocol.
        public static string ToString(Dictionary<string, Option> o)
        {
            List<Option> list = new List<Option>();
            list.AddRange(o.Values);
            list.Sort(new Option.OptionComparer());
            StringBuilder sb = new StringBuilder();

            foreach (Option opt in list)
            {
                sb.Append(Types.newline);
                sb.Append("option name ").Append(opt.name).Append(" type ").Append(opt.type);
                if (opt.type != "button")
                    sb.Append(" default ").Append(opt.defaultValue);

                if (opt.type == "spin")
                    sb.Append(" min ").Append(opt.min).Append(" max ").Append(opt.max);
            }
            return sb.ToString();
        }
    }
}
