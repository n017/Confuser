using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Confuser.Core.Confusions
{
    public class ControlFlowConfusion : StructureConfusion
    {
        public override Priority Priority
        {
            get { return Priority.MethodLevel; }
        }
        public override string Name
        {
            get { return "Control Flow Confusion"; }
        }
        public override Phases Phases
        {
            get { return Phases.Phase3; }
        }
        public override bool StandardCompatible
        {
            get { return false; }
        }

        private enum LevelType
        {
            Try,
            TryStart,
            TryEnd,
            Handler,
            HandlerStart,
            HandlerEnd,
            Filter,
            FilterStart,
            FilterEnd,
            None
        }
        private struct Level
        {
            public ExceptionHandler Handler;
            public LevelType Type;

            public static bool operator ==(Level a, Level b)
            {
                return a.Handler == b.Handler && a.Type == b.Type;
            }

            public static bool operator !=(Level a, Level b)
            {
                return a.Handler != b.Handler || a.Type != b.Type;
            }

            public override int GetHashCode()
            {
                return Handler == null ? (int)Type : Handler.GetHashCode() * (int)Type;
            }

            public override bool Equals(object obj)
            {
                return (obj is Level) && ((Level)obj).Handler == this.Handler && ((Level)obj).Type == this.Type;
            }

            public override string ToString()
            {
                return (Handler == null ? "00000000" : Handler.GetHashCode().ToString("X8")) + "_" + Type.ToString();
            }
        }

        Random rad;
        public override void Confuse(int phase, Confuser cr, AssemblyDefinition asm, IMemberDefinition[] defs)
        {
            if (phase != 3) throw new InvalidOperationException();
            rad = new Random();
            foreach (IMemberDefinition mtd in defs)
                ProcessMethod(cr, mtd as MethodDefinition);
        }

        private void ProcessMethod(Confuser cr, MethodDefinition mtd)   
        {
            if (!mtd.HasBody) return;
            MethodBody bdy = mtd.Body;
            bdy.SimplifyMacros();
            bdy.ComputeHeader();
            Dictionary<Instruction, Level> Ids = GetIds(bdy);
            Level[] lvs = GetLvs(Ids);
            List<Instruction[]> blks = new List<Instruction[]>();
            foreach (Level lv in lvs)
                blks.Add(GetInstructionsByLv(lv, Ids));

            bdy.Instructions.Clear();
            ILProcessor wkr = bdy.GetILProcessor();
            Dictionary<Instruction, Instruction> HdrTbl = new Dictionary<Instruction, Instruction>();
            for (int i = 0; i < blks.Count; i++)
            {
                Instruction[] blk = blks[i];
                Instruction[][] iblks = SplitInstructions(blk);
                ProcessInstructions(bdy, ref iblks);
                Reorder(ref iblks);

                HdrTbl.Add(blk[0], iblks[0][0]);

                foreach (Instruction[] iblk in iblks)
                {
                    wkr.Append(iblk[0]);
                    for (int ii = 1; ii < iblk.Length; ii++)
                    {
                        Instruction tmp;
                        if (iblk[ii].Operand is Instruction)
                        {
                            if (HdrTbl.TryGetValue(iblk[ii].Operand as Instruction, out tmp))
                                iblk[ii].Operand = tmp;
                        }
                        else if (iblk[ii].Operand is Instruction[])
                        {
                            Instruction[] op = iblk[ii].Operand as Instruction[];
                            for (int iii = 0; iii < op.Length; iii++)
                                if (HdrTbl.TryGetValue(op[iii], out tmp))
                                    op[iii] = tmp;
                            iblk[ii].Operand = op;
                        }
                        wkr.Append(iblk[ii]);
                    }
                }
                SetLvHandler(lvs[i], bdy, iblks);
            }

            foreach (ExceptionHandler eh in bdy.ExceptionHandlers)
            {
                eh.TryEnd = eh.TryEnd.Next;
                eh.HandlerEnd = eh.HandlerEnd.Next;
                if ((eh.HandlerType & ExceptionHandlerType.Filter) == ExceptionHandlerType.Filter)
                {
                    eh.FilterEnd = eh.FilterEnd.Next;
                }
            }

            bdy.OptimizeMacros();
            bdy.PreserveMaxStackSize = true;
        }

        private Dictionary<Instruction, Level> GetIds(MethodBody bdy)
        {
            SortedDictionary<int, Level> lvs = new SortedDictionary<int, Level>();
            int p = -1;
            foreach (ExceptionHandler eh in bdy.ExceptionHandlers)
            {
                lvs[eh.TryStart.Offset] = new Level() { Handler = eh, Type = LevelType.TryStart };
                lvs[eh.TryEnd.Previous.Offset] = new Level() { Handler = eh, Type = LevelType.TryEnd };
                lvs[eh.HandlerStart.Offset] = new Level() { Handler = eh, Type = LevelType.HandlerStart };
                lvs[eh.HandlerEnd.Previous.Offset] = new Level() { Handler = eh, Type = LevelType.HandlerEnd };
                p = eh.HandlerEnd.Previous.Offset;
                if ((eh.HandlerType & ExceptionHandlerType.Filter) == ExceptionHandlerType.Filter)
                {
                    lvs[eh.FilterStart.Offset] = new Level() { Handler = eh, Type = LevelType.FilterStart };
                    lvs[eh.FilterEnd.Previous.Offset] = new Level() { Handler = eh, Type = LevelType.FilterEnd };
                    p = eh.FilterEnd.Previous.Offset;
                }
            }
            if (!lvs.ContainsKey(0))
                lvs.Add(0, new Level() { Handler = null, Type = LevelType.None });

            List<int> ks = lvs.Keys.ToList();
            for (int i = 0; i < ks.Count; i++)
            {
                if (lvs[ks[i]].Handler != null)
                {
                    int oo = (lvs[ks[i]].Handler.HandlerType & ExceptionHandlerType.Filter) == ExceptionHandlerType.Filter ? lvs[ks[i]].Handler.FilterEnd.Offset : lvs[ks[i]].Handler.HandlerEnd.Offset;
                    if ((lvs[ks[i]].Type.ToString() == "FilterEnd" ||
                        lvs[ks[i]].Type.ToString() == "HandlerEnd") &&
                        !lvs.ContainsKey(oo))
                    {
                        lvs.Add(oo, new Level() { Handler = lvs[ks[i]].Handler, Type = LevelType.None });
                        ks.Add(oo);
                        ks.Sort();
                    }
                }
                if (i != 0 &&
                    lvs[ks[i - 1]].Type.ToString().EndsWith("Start") &&
                    lvs[ks[i]].Type.ToString().EndsWith("End"))
                {
                    int o = ks[i - 1];
                    Level lv = lvs[o];
                    switch (lv.Type)
                    {
                        case LevelType.TryStart: lv.Type = LevelType.Try; break;
                        case LevelType.HandlerStart: lv.Type = LevelType.Handler; break;
                        case LevelType.FilterStart: lv.Type = LevelType.Filter; break;
                    }
                    lvs.Remove(o);
                    lvs.Remove(ks[i]);
                    lvs.Add(o, lv);
                    ks.Remove(ks[i]);
                    ks.Remove(o);
                    i--;
                }
            }


            Dictionary<Instruction, Level> ret = new Dictionary<Instruction, Level>();
            int offset = 0;
            foreach (Instruction inst in bdy.Instructions)
            {
                if (inst.Offset >= offset && lvs.ContainsKey(inst.Offset))
                    offset = inst.Offset;
                ret.Add(inst, lvs[offset]);
            }
            return ret;
        }
        private Instruction[] GetInstructionsByLv(Level lv, Dictionary<Instruction, Level> ids)
        {
            List<Instruction> ret = new List<Instruction>();
            foreach (KeyValuePair<Instruction, Level> i in ids)
                if (i.Value == lv)
                    ret.Add(i.Key);

            return ret.ToArray();
        }
        private Level[] GetLvs(Dictionary<Instruction, Level> ids)
        {
            List<Level> ret = new List<Level>();
            foreach (Level lv in ids.Values)
                if (!ret.Contains(lv))
                    ret.Add(lv);
            return ret.ToArray();
        }
        private void SetLvHandler(Level lv, MethodBody bdy, Instruction[][] blks)
        {
            if (lv.Handler == null) return;
            switch (lv.Type)
            {
                case LevelType.TryStart:
                    lv.Handler.TryStart = blks[0][0];
                    break;
                case LevelType.TryEnd:
                    lv.Handler.TryEnd = blks[blks.Length - 1][blks[blks.Length - 1].Length - 1];
                    break;
                case LevelType.Try:
                    lv.Handler.TryStart = blks[0][0];
                    lv.Handler.TryEnd = blks[blks.Length - 1][blks[blks.Length - 1].Length - 1];
                    break;
                case LevelType.HandlerStart:
                    lv.Handler.HandlerStart = blks[0][0];
                    break;
                case LevelType.HandlerEnd:
                    lv.Handler.HandlerEnd = blks[blks.Length - 1][blks[blks.Length - 1].Length - 1];
                    break;
                case LevelType.Handler:
                    lv.Handler.HandlerStart = blks[0][0];
                    lv.Handler.HandlerEnd = blks[blks.Length - 1][blks[blks.Length - 1].Length - 1];
                    break;
                case LevelType.FilterStart:
                    lv.Handler.FilterStart = blks[0][0];
                    break;
                case LevelType.FilterEnd:
                    lv.Handler.FilterEnd = blks[blks.Length - 1][blks[blks.Length - 1].Length - 1];
                    break;
                case LevelType.Filter:
                    lv.Handler.FilterStart = blks[0][0];
                    lv.Handler.FilterEnd = blks[blks.Length - 1][blks[blks.Length - 1].Length - 1];
                    break;
                case LevelType.None:
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        private Instruction[][] SplitInstructions(Instruction[] insts)
        {
            List<Instruction[]> ret = new List<Instruction[]>();
            List<Instruction> blk = new List<Instruction>();
            for (int i = 0; i < insts.Length; i++)
            {
                blk.Add(insts[i]);
                if ((rad.NextDouble() > 0.5 ||
                    insts[i].OpCode.Name.StartsWith("new") ||
                    insts[i].OpCode.Name == "pop" ||
                    insts[i].OpCode.Name.StartsWith("ldloc")) &&
                    insts[i].OpCode.Name[0] != 'c' &&
                    insts[i].OpCode.OpCodeType != OpCodeType.Prefix &&
                    insts[i].OpCode != OpCodes.Ldftn && insts[i].OpCode != OpCodes.Ldvirtftn &&
                    (i + 1 == insts.Length || (insts[i + 1].OpCode != OpCodes.Ldftn && insts[i + 1].OpCode != OpCodes.Ldvirtftn)) &&
                    (i - 1 == -1 || (insts[i - 1].OpCode != OpCodes.Ldftn && insts[i - 1].OpCode != OpCodes.Ldvirtftn)))
                {
                    ret.Add(blk.ToArray());
                    blk = new List<Instruction>();
                }
            }
            if (blk.Count != 0)
                ret.Add(blk.ToArray());
            return ret.ToArray();
        }
        private void ProcessInstructions(MethodBody bdy, ref Instruction[][] blks)
        {
            List<Instruction[]> ret = new List<Instruction[]>();
            if (blks.Length != 1) ret.Add(new Instruction[] { Instruction.Create(OpCodes.Br, blks[0][0]) });
            for (int i = 0; i < blks.Length; i++)
            {
                Instruction[] blk = blks[i];
                List<Instruction> newBlk = new List<Instruction>();
                for (int ii = 0; ii < blk.Length; ii++)
                        newBlk.Add(blk[ii]);

                if (i + 1 < blks.Length)
                    AddJump(bdy.GetILProcessor(), newBlk, blks[i + 1][0]);
                ret.Add(newBlk.ToArray());
            }
            blks = ret.ToArray();
        }
        private void Reorder(ref Instruction[][] insts)
        {
            int[] idx = new int[insts.Length];
            int[] ran = new int[insts.Length];
            Instruction[][] ret = new Instruction[insts.Length][];
            while (true)
            {
                for (int i = 0; i < insts.Length; i++)
                {
                    idx[i] = i;
                    ran[i] = rad.Next();
                }
                ran[0] = int.MinValue;
                ran[insts.Length - 1] = int.MaxValue;
                Array.Sort(ran, idx);
                bool f = true;
                for (int i = 1; i < insts.Length - 1; i++)
                    if (idx[i] == i)
                    {
                        f = false;
                        break;
                    }
                if (f || insts.Length - 2 == 1) break;
            }
            for (int i = 0; i < insts.Length; i++)
            {
                ret[idx[i]] = insts[i];
            }
            insts = ret;
        }
        private void AddJump(ILProcessor wkr, List<Instruction> insts, Instruction target)
        {
            int i = rad.Next(0, 3);
            switch (i)
            {
                case 0:
                case 1:
                case 2:
                    insts.Add(wkr.Create(OpCodes.Br, target));
                    break;
            }
        }

        public override string Description
        {
            get { return "This confusion obfuscate the code in the methods so that decompilers cannot decompile the methods."; }
        }

        public override Target Target
        {
            get { return Target.Methods; }
        }
    }
}