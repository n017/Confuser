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
            None = 1,
            Try = 2,
            TryStart = 3,
            TryEnd = 4,
            Handler = 5,
            HandlerStart = 6,
            HandlerEnd = 7,
            Filter = 8,
            FilterStart = 9,
            FilterEnd = 10
        }
        private struct Level
        {
            public Level(ExceptionHandler eh, LevelType t)
            {
                Handler = new List<ExceptionHandler>() { eh };
                Type = new List<LevelType>() { t };
            }

            public List<ExceptionHandler> Handler;
            public List<LevelType> Type;

            public int GetEndOffset()
            {
                int ret = -1;
                foreach (ExceptionHandler eh in Handler)
                {
                    if (eh.TryEnd.Offset > ret) ret = eh.TryEnd.Offset;
                    if (eh.HandlerEnd.Offset > ret) ret = eh.HandlerEnd.Offset;
                    if (eh.FilterEnd != null && eh.FilterEnd.Offset > ret) ret = eh.FilterEnd.Offset;
                }
                return ret;
            }
            public LevelType GetOnlyLevelType()
            {
                if (Type.Count != 1) return 0;
                return Type[0];
            }

            public static bool operator ==(Level a, Level b)
            {
                if (a.Handler.Count != b.Handler.Count ||
                    a.Type.Count != b.Type.Count)
                    return false;

                for (int i = 0; i < a.Handler.Count; i++)
                    if (a.Handler[i] != b.Handler[i])
                        return false;
                for (int i = 0; i < a.Type.Count; i++)
                    if (a.Type[i] != b.Type[i])
                        return false;
                return true;
            }

            public static bool operator !=(Level a, Level b)
            {
                if (a.Handler.Count != b.Handler.Count ||
                    b.Type.Count != b.Type.Count)
                    return true;

                for (int i = 0; i < a.Handler.Count; i++)
                    if (a.Handler[i] == b.Handler[i])
                        return false;
                for (int i = 0; i < a.Type.Count; i++)
                    if (a.Type[i] == b.Type[i])
                        return false;
                return true;
            }

            public static Level operator +(Level a, Level b)
            {
                Level ret = new Level();
                ret.Handler = new List<ExceptionHandler>();
                ret.Handler.AddRange(a.Handler);
                ret.Handler.AddRange(b.Handler);
                ret.Type = new List<LevelType>();
                ret.Type.AddRange(a.Type);
                ret.Type.AddRange(b.Type);
                return ret;
            }

            public override int GetHashCode()
            {
                int hash = base.GetHashCode();
                foreach (ExceptionHandler eh in Handler)
                    hash ^= eh.GetHashCode();
                foreach (LevelType t in Type)
                    hash ^= t.GetHashCode();
                return hash;
            }

            public override bool Equals(object obj)
            {
                return (obj is Level) && ((Level)obj) == this;
            }

            public override string ToString()
            {
                StringBuilder ret = new StringBuilder();
                for (int i = 0; i < Handler.Count; i++)
                {
                    if (i != 0) ret.Append(",");
                    ret.Append((Handler[i] == null ? "00000000" : Handler[i].GetHashCode().ToString("X8")) + "_" + Type[i].ToString());
                } return ret.ToString();
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
                if (!lvs.ContainsKey(eh.TryStart.Offset))
                    lvs[eh.TryStart.Offset] = new Level(eh, LevelType.TryStart);
                else
                    lvs[eh.TryStart.Offset] += new Level(eh, LevelType.TryStart);

                if (!lvs.ContainsKey(eh.TryEnd.Previous.Offset))
                    lvs[eh.TryEnd.Previous.Offset] = new Level(eh, LevelType.TryEnd);
                else
                    lvs[eh.TryEnd.Previous.Offset] += new Level(eh, LevelType.TryEnd);

                if (!lvs.ContainsKey(eh.HandlerStart.Offset))
                    lvs[eh.HandlerStart.Offset] = new Level(eh, LevelType.HandlerStart);
                else
                    lvs[eh.HandlerStart.Offset] += new Level(eh, LevelType.HandlerStart);

                if (!lvs.ContainsKey(eh.HandlerEnd.Previous.Offset))
                    lvs[eh.HandlerEnd.Previous.Offset] = new Level(eh, LevelType.HandlerEnd);
                else
                    lvs[eh.HandlerEnd.Previous.Offset] += new Level(eh, LevelType.HandlerEnd);

                p = eh.HandlerEnd.Previous.Offset;
                if ((eh.HandlerType & ExceptionHandlerType.Filter) == ExceptionHandlerType.Filter)
                {
                    if (!lvs.ContainsKey(eh.FilterStart.Offset))
                        lvs[eh.FilterStart.Offset] = new Level(eh, LevelType.FilterStart);
                    else
                        lvs[eh.FilterStart.Offset] += new Level(eh, LevelType.FilterStart);

                    if (!lvs.ContainsKey(eh.FilterEnd.Previous.Offset))
                        lvs[eh.FilterEnd.Previous.Offset] = new Level(eh, LevelType.FilterEnd);
                    else
                        lvs[eh.FilterEnd.Previous.Offset] += new Level(eh, LevelType.FilterEnd);

                    p = eh.FilterEnd.Previous.Offset;
                }
            }
            if (!lvs.ContainsKey(0))
                lvs[0] = new Level(null, LevelType.None);

            List<int> ks = lvs.Keys.ToList();
            for (int i = 0; i < ks.Count; i++)
            {
                if (lvs[ks[i]].Handler.Count >= 2 &&
                    lvs[ks[i]].Handler[0] == lvs[ks[i]].Handler[1])
                {
                    if (lvs[ks[i]].Type.Contains(LevelType.TryStart) && lvs[ks[i]].Type.Contains(LevelType.TryEnd))
                    {
                        lvs[ks[i]].Handler.RemoveAt(0);
                        lvs[ks[i]].Type.Remove(LevelType.TryStart);
                        lvs[ks[i]].Type.Remove(LevelType.TryEnd);
                        lvs[ks[i]].Type.Add(LevelType.Try);
                    }
                    if (lvs[ks[i]].Type.Contains(LevelType.HandlerStart) && lvs[ks[i]].Type.Contains(LevelType.HandlerEnd))
                    {
                        lvs[ks[i]].Handler.RemoveAt(0);
                        lvs[ks[i]].Type.Remove(LevelType.HandlerStart);
                        lvs[ks[i]].Type.Remove(LevelType.HandlerEnd);
                        lvs[ks[i]].Type.Add(LevelType.Handler);
                    }
                    if (lvs[ks[i]].Type.Contains(LevelType.FilterStart) && lvs[ks[i]].Type.Contains(LevelType.FilterEnd))
                    {
                        lvs[ks[i]].Handler.RemoveAt(0);
                        lvs[ks[i]].Type.Remove(LevelType.FilterStart);
                        lvs[ks[i]].Type.Remove(LevelType.FilterEnd);
                        lvs[ks[i]].Type.Add(LevelType.Filter);
                    }
                }
                if (i != 0 &&
                    lvs[ks[i - 1]].GetOnlyLevelType().ToString().EndsWith("Start") &&
                    lvs[ks[i]].GetOnlyLevelType().ToString().EndsWith("End"))
                {
                    int o = ks[i - 1];
                    Level lv = lvs[o];
                    switch (lv.GetOnlyLevelType())
                    {
                        case LevelType.TryStart:
                            lv.Type.Clear();
                            lv.Type.Add(LevelType.Try); break;
                        case LevelType.HandlerStart:
                            lv.Type.Clear();
                            lv.Type.Add(LevelType.Handler); break;
                        case LevelType.FilterStart:
                            lv.Type.Clear();
                            lv.Type.Add(LevelType.Filter); break;
                    }
                    lvs.Remove(ks[i]);
                    lvs[o] = lv;
                    ks.Remove(ks[i]);
                    i--;
                }
                if (lvs[ks[i]].Handler[0] != null)
                {
                    int oo = lvs[ks[i]].GetEndOffset();
                    if ((lvs[ks[i]].GetOnlyLevelType().ToString() == "FilterEnd" ||
                        lvs[ks[i]].GetOnlyLevelType().ToString() == "HandlerEnd" ||
                        lvs[ks[i]].GetOnlyLevelType().ToString() == "Handler" ||
                        lvs[ks[i]].GetOnlyLevelType().ToString() == "Filter") &&
                        !lvs.ContainsKey(oo))
                    {
                        lvs.Add(oo, new Level() { Handler = lvs[ks[i]].Handler, Type = new List<LevelType> { LevelType.None } });
                        ks.Add(oo);
                        ks.Sort();
                    }
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
            for (int i = 0; i < lv.Handler.Count; i++)
            {
                if (lv.Handler[i] == null) return;
                switch (lv.Type[i])
                {
                    case LevelType.TryStart:
                        lv.Handler[i].TryStart = blks[0][0];
                        break;
                    case LevelType.TryEnd:
                        lv.Handler[i].TryEnd = blks[blks.Length - 1][blks[blks.Length - 1].Length - 1];
                        break;
                    case LevelType.Try:
                        lv.Handler[i].TryStart = blks[0][0];
                        lv.Handler[i].TryEnd = blks[blks.Length - 1][blks[blks.Length - 1].Length - 1];
                        break;
                    case LevelType.HandlerStart:
                        lv.Handler[i].HandlerStart = blks[0][0];
                        break;
                    case LevelType.HandlerEnd:
                        lv.Handler[i].HandlerEnd = blks[blks.Length - 1][blks[blks.Length - 1].Length - 1];
                        break;
                    case LevelType.Handler:
                        lv.Handler[i].HandlerStart = blks[0][0];
                        lv.Handler[i].HandlerEnd = blks[blks.Length - 1][blks[blks.Length - 1].Length - 1];
                        break;
                    case LevelType.FilterStart:
                        lv.Handler[i].FilterStart = blks[0][0];
                        break;
                    case LevelType.FilterEnd:
                        lv.Handler[i].FilterEnd = blks[blks.Length - 1][blks[blks.Length - 1].Length - 1];
                        break;
                    case LevelType.Filter:
                        lv.Handler[i].FilterStart = blks[0][0];
                        lv.Handler[i].FilterEnd = blks[blks.Length - 1][blks[blks.Length - 1].Length - 1];
                        break;
                    case LevelType.None:
                        break;
                    default:
                        throw new InvalidOperationException();
                }
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
            //if (blks.Length != 1) ret.Add(new Instruction[] { Instruction.Create(OpCodes.Br, blks[0][0]) });
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