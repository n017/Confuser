using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using Mono.Cecil;

namespace Confuser.Core
{
    public class ConfusionCollection : Collection<Confusion>
    {
        internal void ExecutePreConfusion(StructureConfusion cion, Confuser cr, AssemblyDefinition asm)
        {
            cr.ScreenLog("<pre name='" + cion.Name + "'>");
            cr.AddLv();
            cion.PreConfuse(cr, asm);
            cr.SubLv();
            cr.ScreenLog("</pre>");
        }
        internal void ExecutePostConfusion(StructureConfusion cion, Confuser cr, AssemblyDefinition asm)
        {
            cr.ScreenLog("<post name='" + cion.Name + "'>");
            cr.AddLv();
            cion.PostConfuse(cr, asm);
            cr.SubLv();
            cr.ScreenLog("</post>");
        }
        internal void ExecuteConfusion(StructureConfusion cion, Confuser cr, AssemblyDefinition asm)
        {
            cr.ScreenLog("<confusion name='" + cion.Name + "'>");
            cr.AddLv();
            cion.DoConfuse(cr, asm);
            cr.SubLv();
            cr.ScreenLog("</confusion>");
        }

        internal void ExecutePreConfusion(AdvancedConfusion cion, Confuser cr, MetadataProcessor.MetadataAccessor accessor)
        {
            cr.ScreenLog("<pre name='" + cion.Name + "'>");
            cr.AddLv();
            cion.PreConfuse(cr, accessor);
            cr.SubLv();
            cr.ScreenLog("</pre>");
        }
        internal void ExecutePostConfusion(AdvancedConfusion cion, Confuser cr, MetadataProcessor.MetadataAccessor accessor)
        {
            cr.ScreenLog("<post name='" + cion.Name + "'>");
            cr.AddLv();
            cion.PostConfuse(cr, accessor);
            cr.SubLv();
            cr.ScreenLog("</post>");
        }
        internal void ExecuteConfusion(AdvancedConfusion cion, Confuser cr, MetadataProcessor.MetadataAccessor accessor)
        {
            cr.ScreenLog("<confusion name='" + cion.Name + "'>");
            cr.AddLv();
            cion.DoConfuse(cr, accessor);
            cr.SubLv();
            cr.ScreenLog("</confusion>");
        }
    }

    [Flags]
    public enum ProcessType
    {
        Pre = 1,
        Real = 2,
        Post = 4
    }
    
    public enum Priority
    {
        Safe,
        CodeLevel,
        FieldLevel,
        MethodLevel,
        TypeLevel,
        AssemblyLevel,
        MetadataLevel,
        PELevel
    }

    public abstract class StructureConfusion : Confusion
    {
        public abstract void PreConfuse(Confuser cr, AssemblyDefinition asm);
        public abstract void DoConfuse(Confuser cr, AssemblyDefinition asm);
        public abstract void PostConfuse(Confuser cr, AssemblyDefinition asm);
    }
    public abstract class AdvancedConfusion : Confusion
    {
        public abstract void PreConfuse(Confuser cr, MetadataProcessor.MetadataAccessor accessor);
        public abstract void DoConfuse(Confuser cr, MetadataProcessor.MetadataAccessor accessor);
        public abstract void PostConfuse(Confuser cr, MetadataProcessor.MetadataAccessor accessor);
    }
    public abstract class Confusion
    {
        public abstract Priority Priority { get; }
        public abstract string Name { get; }
        public abstract ProcessType Process { get; }
        public abstract bool StandardCompatible { get; }
        public override string ToString()
        {
            return Name;
        }
    }
}
