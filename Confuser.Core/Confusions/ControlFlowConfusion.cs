using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Confuser.Core.Confusions
{
    class ControlFlowConfusion : StructureConfusion
    {
        public override Priority Priority
        {
            get { return Priority.CodeLevel; }
        }
        public override string Name
        {
            get { return "Control Flow Confusion"; }
        }
        public override ProcessType Process
        {
            get { return ProcessType.Real; }
        }
        public override bool StandardCompatible
        {
            get { return false; }
        }
        public override void PreConfuse(Confuser cr, AssemblyDefinition asm)
        {
            throw new InvalidOperationException();
        }
        public override void PostConfuse(Confuser cr, AssemblyDefinition asm)
        {
            throw new InvalidOperationException();
        }

        Random rad;
        public override void DoConfuse(Confuser cr, AssemblyDefinition asm)
        {
            rad = new Random();
            foreach (TypeDefinition t in asm.MainModule.Types)
                ProcessMethods(cr, t);
        }
        private void ProcessMethods(Confuser cr, TypeDefinition def)
        {
            foreach (TypeDefinition t in def.NestedTypes)
            {
                ProcessMethods(cr, t);
            }
            foreach (MethodDefinition mtd in def.Constructors)
            {
                ProcessMethod(cr, mtd);
            }
            foreach (MethodDefinition mtd in def.Methods)
            {
                ProcessMethod(cr, mtd);
            }
        }
        private void ProcessMethod(Confuser cr, MethodDefinition mtd)
        {
            if (!mtd.HasBody) return;
            ManagedMethodBody bdy = mtd.Body as ManagedMethodBody;
            bdy.Simplify();
            Dictionary<string, Instruction> Ids = GetIds(bdy);
            List<Instruction[]> blks = new List<Instruction[]>();
            string[] lvs = GetLvs(Ids);
            foreach (string lv in lvs)
                blks.Add(GetInstructionWithSameLv(lv, Ids));

            bdy.Instructions.Clear();
            CilWorker wkr = bdy.CilWorker;
            List<Instruction> endJmp = new List<Instruction>();
            List<Instruction> blkHdr = new List<Instruction>();
            for (int i = 0; i < blks.Count; i++)
            {
                Instruction[] blk = blks[i];
                Instruction[][] iblks = SplitInstructions(blk);
                ProcessInstructions(bdy, ref iblks);
                //Reorder(ref iblks);

                if (iblks[iblks.Length - 1][0].OpCode.FlowControl == FlowControl.Branch)
                    endJmp.Add(iblks[iblks.Length - 1][0]);

                blkHdr.Add(iblks[0][0]);

                foreach (Instruction[] iblk in iblks)
                {
                    foreach (Instruction inst in iblk)
                    {
                        wkr.Append(inst);
                    }
                }
                SetLvHandler(lvs[i], bdy, iblks);
            }

            foreach (Instruction inst in endJmp)
            {
                bool ok = false;
                for (int i = 0; i < blkHdr.Count; i++)
                    if (inst.Operand == blks[i][0])
                    {
                        inst.Operand = blkHdr[i];
                        ok = true;
                        break;
                    }
                if (!ok)
                    throw new InvalidOperationException();
            }

            foreach (ExceptionHandler eh in bdy.ExceptionHandlers)
            {
                if (eh.TryEnd != null)
                    eh.TryEnd = eh.TryEnd.Next;
                if (eh.HandlerEnd != null)
                    eh.HandlerEnd = eh.HandlerEnd.Next;
                if (eh.FilterEnd != null)
                    eh.FilterEnd = eh.FilterEnd.Next;
            }

            bdy.Optimize();

            cr.Log("<method name='" + bdy.Method.ToString() + "'/>");
        }

        private Dictionary<string, Instruction> GetIds(ManagedMethodBody bdy)
        {
            Dictionary<string, Instruction> ret = new Dictionary<string, Instruction>();
            foreach (Instruction inst in bdy.Instructions)
            {
                string id = inst.GetHashCode().ToString("X");
                int i = 0;
                bool behindEh = false;
                for (int eh = 0; eh < bdy.ExceptionHandlers.Count; eh++)
                {
                    if (bdy.ExceptionHandlers[eh].TryStart.Offset <= inst.Offset)
                    {
                        i++;
                    }
                    if (bdy.ExceptionHandlers[eh].HandlerStart.Offset <= inst.Offset)
                    {
                        i++;
                    }
                    if (bdy.ExceptionHandlers[eh].Type == ExceptionHandlerType.Filter &&
                        bdy.ExceptionHandlers[eh].FilterStart.Offset <= inst.Offset)
                    {
                        i++;
                        if (eh == bdy.ExceptionHandlers.Count - 1) behindEh = true;
                    }

                    if (bdy.ExceptionHandlers[eh].TryStart.Offset <= inst.Offset &&
                        bdy.ExceptionHandlers[eh].HandlerStart.Offset <= inst.Offset &&
                        bdy.ExceptionHandlers[eh].TryEnd.Offset <= inst.Offset &&
                        bdy.ExceptionHandlers[eh].HandlerEnd.Offset <= inst.Offset &&
                        (bdy.ExceptionHandlers[eh].Type != ExceptionHandlerType.Filter ||
                        bdy.ExceptionHandlers[eh].FilterStart.Offset <= inst.Offset) &&
                        eh == bdy.ExceptionHandlers.Count - 1)
                        behindEh = true;
                } if (behindEh) i++;
                id += "_" + i.ToString();
                ret.Add(id, inst);
            }
            return ret;
        }
        private Instruction[] GetInstructionWithSameLv(string str, Dictionary<string, Instruction> ids)
        {
            List<Instruction> ret = new List<Instruction>();
            int idx = 0;
            foreach (char i in str)
                if (i == '_')
                    break;
                else
                    idx++;
            string lv = str.Substring(idx);
            foreach (KeyValuePair<string, Instruction> inst in ids)
            {
                idx = 0;
                foreach (char i in inst.Key)
                    if (i == '_')
                        break;
                    else
                        idx++;
                if (inst.Key.Substring(idx) == lv)
                    ret.Add(inst.Value);
            }

            return ret.ToArray();
        }
        private string[] GetLvs(Dictionary<string, Instruction> ids)
        {
            List<string> ret = new List<string>();
            foreach (KeyValuePair<string, Instruction> inst in ids)
            {
                int idx = 0;
                foreach (char i in inst.Key)
                    if (i == '_')
                        break;
                    else
                        idx++;
                string lv = inst.Key.Substring(idx);
                if (!ret.Contains(lv))
                    ret.Add(lv);
            }
            return ret.ToArray();
        }
        private void SetLvHandler(string lvv, ManagedMethodBody bdy, Instruction[][] blks)
        {
            int lv = int.Parse(lvv.Substring(lvv.IndexOf('_') + 1));
            int now = 0;
            foreach (ExceptionHandler eh in bdy.ExceptionHandlers)
            {
                if (eh.TryStart != null)
                    now++;
                if (now == lv)
                {
                    eh.TryStart = blks[0][0];
                    eh.TryEnd = blks[blks.Length - 1][blks[blks.Length - 1].Length - 1];
                    return;
                }
                if (eh.HandlerStart != null)
                    now++;
                if (now == lv)
                {
                    eh.HandlerStart = blks[0][0];
                    eh.HandlerEnd = blks[blks.Length - 1][blks[blks.Length - 1].Length - 1];
                    return;
                }
                if (eh.Type == ExceptionHandlerType.Filter && eh.FilterStart != null)
                    now++;
                if (now == lv)
                {
                    eh.FilterStart = blks[0][0];
                    eh.FilterEnd = blks[blks.Length - 1][blks[blks.Length - 1].Length - 1];
                    return;
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
                if ((rad.Next() > rad.Next() ||
                    insts[i].OpCode.Name.StartsWith("new") ||
                    insts[i].OpCode.Name == "pop" ||
                    insts[i].OpCode.Name.StartsWith("ldloc")) &&
                    insts[i].OpCode.Name[0] != 'c' &&
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
        private void ProcessInstructions(ManagedMethodBody bdy, ref Instruction[][] blks)
        {
            List<Instruction[]> ret = new List<Instruction[]>();
            Instruction retBlk = blks[blks.Length - 1][blks[blks.Length - 1].Length - 1];

            if (blks.Length != 0) ret.Add(new Instruction[] { bdy.CilWorker.Create(OpCodes.Br, blks[0][0]) });
            for (int i = 0; i < blks.Length; i++)
            {
                Instruction[] blk = blks[i];
                List<Instruction> newBlk = new List<Instruction>();
                for (int ii = 0; ii < blk.Length; ii++)
                {
                    if (blk[ii].OpCode != OpCodes.Ret)
                        newBlk.Add(blk[ii]);
                }

                if (i + 1 < blks.Length)
                    AddJump(bdy.CilWorker, newBlk, blks[i + 1][0]);
                else
                    AddJump(bdy.CilWorker, newBlk, retBlk);
                ret.Add(newBlk.ToArray());
            }
            ret.Add(new Instruction[] { retBlk });
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
        private void AddJump(CilWorker wkr, List<Instruction> insts, Instruction target)
        {
            int i = rad.Next(0, 3);
            switch (i)
            {
                case 0:
                case 1:
                case 2:
                    insts.Add(wkr.Create(OpCodes.Br, target));
                    //switch (rad.Next(0, 3))
                    //{
                    //    case 0:
                    //        insts.Add(wkr.Create(OpCodes.Dup)); break;
                    //    case 1:
                    //        insts.Add(wkr.Create(OpCodes.Ldnull)); break;
                    //    case 2:
                    //        insts.Add(wkr.Create(OpCodes.Ldc_I4, rad.Next())); break;
                    //}
                    break;
            }
        }
    }
}