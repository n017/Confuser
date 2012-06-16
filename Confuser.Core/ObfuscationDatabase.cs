using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;
using System.IO;

namespace Confuser.Core
{
    public class ObfuscationModule : Dictionary<string, List<Tuple<string, string>>>
    {
        string GetString(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        public void AddEntry(string table, string name, object value)
        {
            List<Tuple<string, string>> tbl;
            if (!TryGetValue(table, out tbl))
                tbl = this[table] = new List<Tuple<string, string>>();
            tbl.Add(new Tuple<string, string>(name, value is byte[] ? GetString((byte[])value) : value.ToString()));
        }

        public void Serialize(BinaryWriter wtr)
        {
            wtr.Write(Count);
            foreach (var i in this)
            {
                wtr.Write(i.Key);
                wtr.Write(i.Value.Count);
                foreach (var j in i.Value)
                {
                    wtr.Write(j.Item1);
                    wtr.Write(j.Item2);
                }
            }
        }
        public void Deserialize(BinaryReader rdr)
        {
            int count = rdr.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string tblName = rdr.ReadString();
                List<Tuple<string, string>> tbl = new List<Tuple<string, string>>(rdr.ReadInt32());
                for (int j = 0; j < tbl.Capacity; j++)
                    tbl.Add(new Tuple<string, string>(rdr.ReadString(), rdr.ReadString()));
                Add(tblName, tbl);
            }
        }
    }
    public class ObfuscationDatabase : Dictionary<string, ObfuscationModule>
    {
        public ObfuscationDatabase()
            : base(StringComparer.Ordinal)
        {
        }

        ObfuscationModule module;
        public void Module(string name)
        {
            if (!TryGetValue(name, out module))
                module = this[name] = new ObfuscationModule();
        }

        public void AddEntry(string table, string name, object value)
        {
            module.AddEntry(table, name, value);
        }

        public void Serialize(BinaryWriter wtr)
        {
            wtr.Write(0x42445243);
            wtr.Write(Count);
            foreach (var i in this)
            {
                wtr.Write(i.Key);
                i.Value.Serialize(wtr);
            }
        }
        public void Deserialize(BinaryReader rdr)
        {
            if (rdr.ReadInt32() != 0x42445243)
                throw new InvalidOperationException();
            int count = rdr.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string modName = rdr.ReadString();
                ObfuscationModule mod = new ObfuscationModule();
                mod.Deserialize(rdr);
                Add(modName, mod);
            }
        }
    }
}
