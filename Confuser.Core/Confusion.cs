using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using Mono.Cecil;

namespace Confuser.Core
{
    [Flags]
    public enum Phases
    {
        Phase1 = 1,
        Phase2 = 2,
        Phase3 = 4
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
        public abstract void Confuse(int phase, Confuser cr, AssemblyDefinition asm, IMemberDefinition[] defs);
    }
    public abstract class AdvancedConfusion : Confusion
    {
        public abstract void Confuse(int phase, Confuser cr, MetadataProcessor.MetadataAccessor accessor);
        public override Target Target
		{
			get
			{
                return Target.Whole;
			}
		}
    }

    [Flags]
    public enum Target
    {
        Types = 1,
        Methods = 2,
        Fields = 4,
        Events = 8,
        Properties = 16,
        All = 31,
        Whole = 64,
    }
    public abstract class Confusion
    {
        Confuser cr;
        internal Confuser Confuser { get { return cr; } set { cr = value; } }
        protected void Log(string message) { cr.LogMessage(message); }

        public abstract Priority Priority { get; }
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract Phases Phases { get; }
        public abstract bool StandardCompatible { get; }
        public abstract Target Target { get; }
        public override string ToString()
        {
            return Name;
        }
    }
}
