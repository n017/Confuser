using System;
using System.Collections.Generic;
using System.Text;
using Mono.Cecil.Metadata;
using Mono.Cecil.Binary;
using System.IO;

namespace Mono.Cecil
{
    public abstract class AssemblyProcessor : StructureWriter
    {
        public AssemblyProcessor(AssemblyDefinition asm, BinaryWriter wtr) : base(asm, wtr) { }

        public uint AddUserString(string str)
        {
            return base.m_mdWriter.AddUserString(str);
        }
        public uint AddString(string str)
        {
            return base.m_mdWriter.AddString(str);
        }
        public uint AddBlob(byte[] b)
        {
            return base.m_mdWriter.AddBlob(b);
        }
        public uint AddGuid(Guid g)
        {
            return base.m_mdWriter.AddGuid(g);
        }
        public T CreateRow<T>() where T : class, IMetadataRow
        {
            return Activator.CreateInstance(typeof(T), true) as T; 
        }
        public T GetTable<T>() where T : class, IMetadataTable
        {
            TablesHeap tbls = Assembly.MainModule.Image.MetadataRoot.Streams.TablesHeap;
            int rid = (int)typeof(T).GetField("RId").GetValue(null);
            if (tbls.HasTable(rid))
                return tbls[rid] as T;

            T tbl = Activator.CreateInstance(typeof(T), true) as T;
            tbl.Rows = new RowCollection();
            tbls.Valid |= 1L << tbl.Id;
            tbls.Tables.Add(tbl);
            return tbl;
        }

        protected abstract void PreProcess();
        protected abstract void Process();
        protected abstract void PostProcess();

        public override void VisitAssemblyDefinition(AssemblyDefinition asm)
        {
            PreProcess();
            base.VisitAssemblyDefinition(asm);
            Process();
        }

        public override void TerminateAssemblyDefinition(AssemblyDefinition asm)
        {
            foreach (ModuleDefinition mod in asm.Modules)
            {
                ReflectionWriter writer = mod.Controller.Writer;
                writer.VisitModuleDefinition(mod);
                writer.VisitTypeReferenceCollection(mod.TypeReferences);
                writer.VisitTypeDefinitionCollection(mod.Types);
                writer.VisitMemberReferenceCollection(mod.MemberReferences);
                writer.CompleteTypeDefinitions();

                PostProcess();

                writer.TerminateModuleDefinition(mod);
            }
        }
    }
}
