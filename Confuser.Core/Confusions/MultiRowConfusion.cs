using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Metadata;

namespace Confuser.Core.Confusions
{
    public class MultiRowConfusion : AdvancedConfusion
    {
        public override void PreConfuse(Confuser cr, MetadataProcessor.MetadataAccessor accessor)
        {
            accessor.TableHeap.GetTable<ModuleTable>(Table.Module).AddRow(accessor.StringHeap.GetStringIndex(Guid.NewGuid().ToString()));
            cr.Log("<mod/>");

            accessor.TableHeap.GetTable<AssemblyTable>(Table.Assembly).AddRow(new Row<AssemblyHashAlgorithm, ushort, ushort, ushort, ushort, AssemblyAttributes, uint, uint, uint>(
                AssemblyHashAlgorithm.None, 0, 0, 0, 0, AssemblyAttributes.SideBySideCompatible, 0,
                accessor.StringHeap.GetStringIndex(Guid.NewGuid().ToString()), 0));
            cr.Log("<asm/>");
        }

        public override void DoConfuse(Confuser cr, MetadataProcessor.MetadataAccessor accessor)
        {
            throw new InvalidOperationException();
        }

        public override void PostConfuse(Confuser cr, MetadataProcessor.MetadataAccessor accessor)
        {
            throw new InvalidOperationException();
        }

        public override Priority Priority
        {
            get { return Priority.MetadataLevel; }
        }

        public override string Name
        {
            get { return "Multiple Row Confusion"; }
        }

        public override ProcessType Process
        {
            get { return ProcessType.Pre; }
        }

        public override bool StandardCompatible
        {
            get { return false; }
        }
    }
}
