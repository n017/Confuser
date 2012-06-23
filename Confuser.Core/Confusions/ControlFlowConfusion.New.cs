using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Confuser.Core.Poly;
using System.IO;
using Confuser.Core.Poly.Visitors;
using System.Collections.Specialized;
using Mono.Cecil.Metadata;
using Mono;

namespace Confuser.Core.Confusions
{
    public class ControlFlowConfusion : StructurePhase, IConfusion
    {
        enum LevelType
        {
            //00: None      None
            //01: Try       Whole
            //10: Handler   Start
            //11: Filter    End
            //    00        00

            Invalid = 0,
            None = 1,
            Try = 5,
            TryStart = 6,
            TryEnd = 7,
            Handler = 9,
            HandlerStart = 10,
            HandlerEnd = 11,
            Filter = 13,
            FilterStart = 14,
            FilterEnd = 15
        }
        struct Level
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
                    if (eh.FilterStart != null && eh.HandlerStart.Offset > ret) ret = eh.HandlerStart.Offset;
                }
                return ret;
            }
            public LevelType GetOnlyLevelType()
            {
                if (Type.Count != 1) return LevelType.Invalid;
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
                    if (eh != null)
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
        class Scope
        {
            public Level Level;
            public Instruction[] Instructions;
        }
        class ScopeDetector
        {
            static bool IsEnd(LevelType type)
            {
                return ((int)type & 3) == 3;
            }
            static bool IsStart(LevelType type)
            {
                return ((int)type & 3) == 2;
            }
            static bool IsWhole(LevelType type)
            {
                return ((int)type & 3) == 1;
            }
            private static Dictionary<Instruction, Level> GetIds(MethodBody body)
            {
                SortedDictionary<int, Level> lvs = new SortedDictionary<int, Level>();
                int p = -1;
                foreach (ExceptionHandler eh in body.ExceptionHandlers)
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

                        if (!lvs.ContainsKey(eh.HandlerStart.Previous.Offset))
                            lvs[eh.HandlerStart.Previous.Offset] = new Level(eh, LevelType.FilterEnd);
                        else
                            lvs[eh.HandlerStart.Previous.Offset] += new Level(eh, LevelType.FilterEnd);

                        p = eh.HandlerStart.Previous.Offset;
                    }
                }
                if (!lvs.ContainsKey(0))
                    lvs[0] = new Level(null, LevelType.None);

                List<int> ks = lvs.Keys.ToList();
                for (int i = 0; i < ks.Count; i++)
                {
                    //xxxStart & xxxEnd
                    //->
                    //xxx
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
                    //xxxStart
                    //xxxEnd
                    //->
                    //xxx
                    if (i != 0 &&
                        IsStart(lvs[ks[i - 1]].GetOnlyLevelType()) &&
                        IsEnd(lvs[ks[i]].GetOnlyLevelType()))
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
                        ks.RemoveAt(i);
                        i--;
                    }
                    //xxxEnd
                    //xxxxxx
                    //->
                    //xxxEnd
                    //None
                    //xxxxxx
                    if (lvs[ks[i]].Handler[0] != null)
                    {
                        int oo = lvs[ks[i]].GetEndOffset();
                        if ((lvs[ks[i]].GetOnlyLevelType() == LevelType.FilterEnd ||
                             lvs[ks[i]].GetOnlyLevelType() == LevelType.HandlerEnd ||
                             lvs[ks[i]].GetOnlyLevelType() == LevelType.Handler ||
                             lvs[ks[i]].GetOnlyLevelType() == LevelType.Filter) &&
                             !lvs.ContainsKey(oo))
                        {
                            lvs.Add(oo, new Level() { Handler = lvs[ks[i]].Handler, Type = new List<LevelType> { LevelType.None } });
                            ks.Add(oo);
                            ks.Sort();
                        }
                    }
                    //None
                    //xxxEnd
                    //->
                    //xxxEnd
                    if (lvs[ks[i]].GetOnlyLevelType() == LevelType.None)
                    {
                        if (i < ks.Count - 1 && IsEnd(lvs[ks[i + 1]].GetOnlyLevelType()))
                        {
                            Level lv = lvs[ks[i + 1]];
                            lvs.Remove(ks[i + 1]);
                            ks.RemoveAt(i + 1);
                            lvs[ks[i]] = lv;
                        }
                    }
                }


                Dictionary<Instruction, Level> ret = new Dictionary<Instruction, Level>();
                int offset = 0;
                foreach (Instruction inst in body.Instructions)
                {
                    if (inst.Offset >= offset && lvs.ContainsKey(inst.Offset))
                        offset = inst.Offset;
                    ret.Add(inst, lvs[offset]);
                }
                return ret;
            }
            private static Instruction[] GetInstructionsByLv(Level lv, Dictionary<Instruction, Level> ids)
            {
                List<Instruction> ret = new List<Instruction>();
                foreach (KeyValuePair<Instruction, Level> i in ids)
                    if (i.Value == lv)
                        ret.Add(i.Key);

                return ret.ToArray();
            }

            public static IEnumerable<Scope> DetectScopes(MethodBody body)
            {
                Dictionary<Instruction, Level> Ids = GetIds(body);
                var lvs = Ids.Values.Distinct();

                Dictionary<Level, List<Instruction>> scopes = new Dictionary<Level, List<Instruction>>();
                foreach (var i in Ids)
                {
                    List<Instruction> scope;
                    if (!scopes.TryGetValue(i.Value, out scope))
                        scope = scopes[i.Value] = new List<Instruction>();
                    scope.Add(i.Key);
                }
                return scopes.Select(_ => new Scope() { Level = _.Key, Instructions = _.Value.ToArray() }).ToArray();
            }
        }

        enum StatementType
        {
            Normal,
            Branch
        }
        class Statement
        {
            public StatementType Type;
            public Instruction[] Instructions;
            public int Key;
        }

        public string Name
        {
            get { return "Control Flow Confusion"; }
        }
        public string Description
        {
            get { return "This confusion obfuscate the code in the methods so that decompilers cannot decompile the methods."; }
        }
        public string ID
        {
            get { return "ctrl flow"; }
        }
        public bool StandardCompatible
        {
            get { return true; }
        }
        public Target Target
        {
            get { return Target.Methods; }
        }
        public Preset Preset
        {
            get { return Preset.Aggressive; }
        }
        public Phase[] Phases
        {
            get { return new Phase[] { this }; }
        }
        public bool SupportLateAddition
        {
            get { return true; }
        }
        public Behaviour Behaviour
        {
            get { return Behaviour.AlterCode; }
        }

        public override Priority Priority
        {
            get { return Priority.MethodLevel; }
        }
        public override IConfusion Confusion
        {
            get { return this; }
        }
        public override int PhaseID
        {
            get { return 3; }
        }
        public override bool WholeRun
        {
            get { return false; }
        }
        public override void Initialize(ModuleDefinition mod)
        {
            if (mod.Architecture != TargetArchitecture.I386)
                Log("Junk code is not supported on target architecture, it won't be generated.");

            generator = new ExpressionGenerator(Random.Next());
        }
        public override void DeInitialize()
        {
            //
        }

        Dictionary<ModuleDefinition, Tuple<byte[], FieldDefinition>> txts = new Dictionary<ModuleDefinition, Tuple<byte[], FieldDefinition>>();
        public void Init() { txts.Clear(); }
        public void Deinit() { txts.Clear(); }

        bool genJunk;
        int level;
        bool fakeBranch;
        MethodDefinition method;
        Expression exp;
        Expression invExp;

        ExpressionGenerator generator;
        public override void Process(ConfusionParameter parameter)
        {
            method = parameter.Target as MethodDefinition;
            if (!method.HasBody) return;

            level = 5;
            if (Array.IndexOf(parameter.Parameters.AllKeys, "level") != -1)
            {
                if (!int.TryParse(parameter.Parameters["level"], out level) && (level <= 0 || level > 10))
                {
                    Log("Invalid level, 5 will be used.");
                    level = 5;
                }
            }

            genJunk = false;
            if (method.Module.Architecture != TargetArchitecture.I386)
                genJunk = false;
            else if (Array.IndexOf(parameter.Parameters.AllKeys, "genjunk") != -1)
            {
                if (!bool.TryParse(parameter.Parameters["genjunk"], out genJunk))
                {
                    Log("Invalid junk code parameter, junk code would not be generated.");
                    genJunk = false;
                }
            }

            fakeBranch = false;
            if (Array.IndexOf(parameter.Parameters.AllKeys, "fakeBranch") != -1)
            {
                if (!bool.TryParse(parameter.Parameters["fakeBranch"], out fakeBranch))
                {
                    Log("Invalid fake branch parameter, fake branch would not be generated.");
                    fakeBranch = false;
                }
            }

            MethodBody body = method.Body;
            body.SimplifyMacros();
            body.ComputeHeader();
            body.MaxStackSize += 0x10;

            PropagateSeqPoints(body);

            VariableDefinition stateVar = new VariableDefinition(method.Module.TypeSystem.Int32);
            body.Variables.Add(stateVar);
            body.InitLocals = true;

            //Compute stacks
            var stacks = GetStacks(body);

            Dictionary<Instruction, Instruction> ReplTbl = new Dictionary<Instruction, Instruction>();
            List<Scope> scopes = new List<Scope>();
            foreach (var scope in ScopeDetector.DetectScopes(body))
            {
                scopes.Add(scope);

                //Split statements when stack = empty
                List<Statement> sts = new List<Statement>();
                foreach (var i in SplitStatements(body, scope.Instructions, stacks))
                    sts.Add(new Statement()
                    {
                        Instructions = i,
                        Type = StatementType.Normal,
                        Key = 0
                    });

                //Constructor fix
                if (body.Method.IsConstructor && body.Method.HasThis)
                {
                    Statement init = new Statement();
                    init.Type = StatementType.Normal;
                    List<Instruction> z = new List<Instruction>();
                    while (sts.Count != 0)
                    {
                        z.AddRange(sts[0].Instructions);
                        Instruction lastInst = sts[0].Instructions[sts[0].Instructions.Length - 1];
                        sts.RemoveAt(0);
                        if (lastInst.OpCode == OpCodes.Call &&
                            (lastInst.Operand as MethodReference).Name == ".ctor")
                            break;
                    }
                    init.Instructions = z.ToArray();
                    sts.Insert(0, init);
                }

                if (sts.Count == 1) continue;

                //Merge statements for level
                for (int i = 0; i < sts.Count - 1; i++)
                {
                    if (Random.Next(1, 10) > level)
                    {
                        Statement newSt = new Statement();
                        newSt.Type = sts[i + 1].Type;
                        newSt.Instructions = new Instruction[sts[i].Instructions.Length + sts[i + 1].Instructions.Length];
                        Array.Copy(sts[i].Instructions, 0, newSt.Instructions, 0, sts[i].Instructions.Length);
                        Array.Copy(sts[i + 1].Instructions, 0, newSt.Instructions, sts[i].Instructions.Length, sts[i + 1].Instructions.Length);
                        sts[i] = newSt;
                        sts.RemoveAt(i + 1);
                        i--;
                    }
                }

                //Detect branches
                int k = 0;
                foreach (var st in sts)
                {
                    Instruction last = st.Instructions[st.Instructions.Length - 1];
                    if (last.Operand is Instruction &&
                        sts.Exists(_ => _.Instructions[0] == last.Operand))
                        st.Type = StatementType.Branch;
                    st.Key = k; k++;
                }

                //Shuffle the statements
                List<Instruction> insts = new List<Instruction>();
                for (int i = 1; i < sts.Count; i++)
                {
                    int j = Random.Next(1, sts.Count);
                    var tmp = sts[j];
                    sts[j] = sts[i];
                    sts[i] = tmp;
                }
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < sts.Count; i++)
                {
                    if (i != 0) sb.Append(", ");
                    sb.Append(sts[i].Key);
                }
                Database.AddEntry("CtrlFlow", method.FullName, sb);

                Instruction[] stHdrs = new Instruction[sts.Count];
                for (int i = 0; i < sts.Count; i++)
                {
                    sts[i].Key = i;
                    if (sts[i].Instructions.Length > 0)
                        stHdrs[i] = sts[i].Instructions[0];
                }
                Func<object, Statement> resolveHdr = inst =>
                {
                    int _ = Array.IndexOf(stHdrs, inst as Instruction);
                    return _ == -1 ? null : sts[_];
                };


                exp = generator.Generate(level);
                invExp = ExpressionInverser.InverseExpression(exp);
                Instruction[] ldloc = new Instruction[] { Instruction.Create(OpCodes.Ldloc, stateVar) };// new CecilVisitor(invExp, new Instruction[] { Instruction.Create(OpCodes.Ldloc, stateVar) }).GetInstructions();
                Instruction begin = ldloc[0];
                Instruction swit = Instruction.Create(OpCodes.Switch, Empty<Instruction>.Array);
                Instruction end = Instruction.Create(OpCodes.Nop);
                List<Instruction> targets = new List<Instruction>();
                Statement beginSt = resolveHdr(scope.Instructions[0]);

                //Convert branches -> switch
                bool firstSt = true;
                foreach (var st in sts)
                {
                    List<Instruction> stInsts = new List<Instruction>(st.Instructions);
                    Instruction last = st.Instructions[st.Instructions.Length - 1];
                    if (st.Type == StatementType.Branch)
                    {
                        if (last.OpCode.Code == Code.Br)  //uncond
                        {
                            int index = stInsts.Count - 1;
                            stInsts.RemoveAt(index);
                            if (fakeBranch)
                            {
                                Statement targetSt = resolveHdr(last.Operand);
                                Statement fallSt = resolveHdr(last.Next);

                                ReplTbl[last] = GenFakeBranch(st, targetSt, fallSt, stInsts, stateVar, begin);
                            }
                            else
                            {
                                Statement targetSt = resolveHdr(last.Operand);
                                ReplTbl[last] = EncryptNum(st.Key, stateVar, targetSt.Key, stInsts);
                                stInsts.Add(Instruction.Create(OpCodes.Br, begin));
                                stInsts.AddRange(GetJunk(stateVar));
                            }
                            stInsts[index].SequencePoint = last.SequencePoint;
                        }
                        else if (last.OpCode.Code != Code.Leave)  //cond
                        {
                            int index = stInsts.Count - 1;
                            stInsts.RemoveAt(index);
                            Statement targetSt = resolveHdr(last.Operand);
                            Statement fallSt = resolveHdr(last.Next);

                            if (fallSt == null) //fall into exception block
                            {
                                ReplTbl[last] = EncryptNum(st.Key, stateVar, targetSt.Key, stInsts);
                                stInsts.Add(Instruction.Create(last.OpCode, begin));
                                stInsts.Add(Instruction.Create(OpCodes.Br, last.Next));
                                stInsts.AddRange(GetJunk(stateVar));
                            }
                            else
                            {
                                ReplTbl[last] = EncryptNum(st.Key, stateVar, targetSt.Key, stInsts);
                                stInsts.Add(Instruction.Create(last.OpCode, begin));
                                EncryptNum(st.Key, stateVar, fallSt.Key, stInsts);
                                stInsts.Add(Instruction.Create(OpCodes.Br, begin));
                                stInsts.AddRange(GetJunk(stateVar));
                            }
                            stInsts[index].SequencePoint = last.SequencePoint;
                        }
                    }
                    else
                    {
                        Statement fallSt = resolveHdr(last.Next);
                        if (fallSt != null)
                        {
                            if (fakeBranch)
                            {
                                Statement fakeSt = sts[Random.Next(0, sts.Count)];
                                GenFakeBranch(st, fallSt, fakeSt, stInsts, stateVar, begin);
                            }
                            else
                            {
                                EncryptNum(st.Key, stateVar, fallSt.Key, stInsts);
                                stInsts.Add(Instruction.Create(OpCodes.Br, begin));
                                stInsts.AddRange(GetJunk(stateVar));
                            }
                        }
                        else
                            stInsts.Add(Instruction.Create(OpCodes.Br, end));
                    }
                    if (!firstSt)
                    {
                        targets.Add(stInsts[0]);
                        insts.AddRange(stInsts.ToArray());
                    }
                    else
                    {
                        insts.AddRange(stInsts.ToArray());
                        insts.AddRange(ldloc);
                        insts.Add(swit);
                        targets.Add(stInsts[0]);
                        firstSt = false;
                    }
                }
                swit.Operand = targets.ToArray();
                insts.Add(end);


                //fix peverify
                if (scope.Level.Type.Contains(LevelType.Filter) ||
                    scope.Level.Type.Contains(LevelType.FilterStart))
                {
                    insts.Add(Instruction.Create(OpCodes.Ldc_I4, Random.Next()));
                    insts.Add(Instruction.Create(OpCodes.Endfilter));
                }
                else if (scope.Level.Type.Contains(LevelType.Try) ||
                         scope.Level.Type.Contains(LevelType.TryEnd))
                {
                    Instruction last = scope.Instructions[scope.Instructions.Length - 1];
                    insts.Add(Instruction.Create(OpCodes.Leave, last.OpCode != OpCodes.Leave ? scope.Level.Handler[0].HandlerEnd : last.Operand as Instruction));
                }
                else if (scope.Level.Type.Contains(LevelType.Handler) ||
                         scope.Level.Type.Contains(LevelType.HandlerEnd))
                {
                    if (scope.Level.Handler[0].HandlerType == ExceptionHandlerType.Finally ||
                        scope.Level.Handler[0].HandlerType == ExceptionHandlerType.Fault)
                        insts.Add(Instruction.Create(OpCodes.Endfinally));
                    else
                    {
                        Instruction last = scope.Instructions[scope.Instructions.Length - 1];
                        insts.Add(Instruction.Create(OpCodes.Leave, last.OpCode != OpCodes.Leave ? scope.Level.Handler[0].HandlerEnd : last.Operand as Instruction));
                    }
                }

                ReplTbl[scope.Instructions[0]] = insts[0];
                scope.Instructions = insts.ToArray();
            }

            //emit
            body.Instructions.Clear();
            foreach (var scope in scopes)
                foreach (var i in scope.Instructions)
                {
                    if (i.Operand is Instruction &&
                        ReplTbl.ContainsKey(i.Operand as Instruction))
                    {
                        i.Operand = ReplTbl[i.Operand as Instruction];
                    }
                    else if (i.Operand is Instruction[])
                    {
                        Instruction[] insts = i.Operand as Instruction[];
                        for (int j = 0; j < insts.Length; j++)
                            if (ReplTbl.ContainsKey(insts[j]))
                                insts[j] = ReplTbl[insts[j]];
                    }
                }
            foreach (var scope in scopes)
            {
                SetLvHandler(scope, body, scope.Instructions);
                foreach (var i in scope.Instructions)
                    body.Instructions.Add(i);
            }

            //fix peverify
            if (!method.ReturnType.IsTypeOf("System", "Void"))
            {
                body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
                body.Instructions.Add(Instruction.Create(OpCodes.Unbox_Any, method.ReturnType));
            }
            body.Instructions.Add(Instruction.Create(OpCodes.Ret));


            foreach (ExceptionHandler eh in body.ExceptionHandlers)
            {
                eh.TryEnd = eh.TryEnd.Next;
                eh.HandlerEnd = eh.HandlerEnd.Next;
            }

            body.ComputeOffsets();
            body.PreserveMaxStackSize = true;

            ReduceSeqPoints(body);
        }
        static void SetLvHandler(Scope scope, MethodBody body, IList<Instruction> block)
        {
            for (int i = 0; i < scope.Level.Handler.Count; i++)
            {
                if (scope.Level.Handler[i] == null) return;
                switch (scope.Level.Type[i])
                {
                    case LevelType.TryStart:
                        scope.Level.Handler[i].TryStart = block[0];
                        break;
                    case LevelType.TryEnd:
                        scope.Level.Handler[i].TryEnd = block[block.Count - 1];
                        break;
                    case LevelType.Try:
                        scope.Level.Handler[i].TryStart = block[0];
                        scope.Level.Handler[i].TryEnd = block[block.Count - 1];
                        break;
                    case LevelType.HandlerStart:
                        scope.Level.Handler[i].HandlerStart = block[0];
                        break;
                    case LevelType.HandlerEnd:
                        scope.Level.Handler[i].HandlerEnd = block[block.Count - 1];
                        break;
                    case LevelType.Handler:
                        scope.Level.Handler[i].HandlerStart = block[0];
                        scope.Level.Handler[i].HandlerEnd = block[block.Count - 1];
                        break;
                    case LevelType.FilterStart:
                        scope.Level.Handler[i].FilterStart = block[0];
                        break;
                    case LevelType.FilterEnd:
                        break;
                    case LevelType.Filter:
                        scope.Level.Handler[i].FilterStart = block[0];
                        break;
                    case LevelType.None:
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        void PropagateSeqPoints(MethodBody body)
        {
            SequencePoint active = null;
            foreach (var i in body.Instructions)
            {
                if (i.SequencePoint != null &&
                    i.OpCode.FlowControl != FlowControl.Branch &&
                    i.OpCode.FlowControl != FlowControl.Cond_Branch)
                    active = i.SequencePoint;
                else if (i.OpCode.FlowControl != FlowControl.Branch &&
                    i.OpCode.FlowControl != FlowControl.Cond_Branch)
                    i.SequencePoint = active;
            }
        }

        void ReduceSeqPoints(MethodBody body)
        {
            SequencePoint active = null;
            foreach (var i in body.Instructions)
            {
                if (i.SequencePoint == active)
                    i.SequencePoint = null;
                else
                    active = i.SequencePoint;
            }
        }

        int ComputeRandNum(IList<Instruction> insts)
        {
            ExpressionGenerator gen = new ExpressionGenerator(Random.Next());
            Expression exp = gen.Generate(2);
            int r = Random.Next(-10, 10);
            int i = ExpressionEvaluator.Evaluate(exp, r);
            foreach (var inst in new CecilVisitor(exp, new Instruction[] { Instruction.Create(OpCodes.Ldc_I4, r) }).GetInstructions())
                insts.Add(inst);
            return i;
        }
        Instruction EncryptNum(int original, VariableDefinition varDef, int num, IList<Instruction> insts)
        {
            //original = original < 0 ? 0 : original;

            //ExpressionGenerator gen = new ExpressionGenerator();
            //Expression exp = gen.Generate(3);
            //int i = ExpressionEvaluator.Evaluate(exp, (num ^ original));
            //exp = ExpressionInverser.InverseExpression(exp);
            //bool first = true;
            Instruction ret = null;
            //foreach (var inst in new CecilVisitor(exp, new Instruction[] { Instruction.Create(OpCodes.Ldc_I4, i) }).GetInstructions())
            //{
            //    if (first)
            //    {
            //        ret = inst;
            //        first = false;
            //    }
            //    insts.Add(inst);
            //}
            //insts.Add(Instruction.Create(OpCodes.Ldloc, varDef));
            //insts.Add(Instruction.Create(OpCodes.Xor));
            //insts.Add(ret = Instruction.Create(OpCodes.Ldc_I4, ExpressionEvaluator.Evaluate(exp, num)));
            insts.Add(ret = Instruction.Create(OpCodes.Ldc_I4, num));
            insts.Add(Instruction.Create(OpCodes.Stloc, varDef));
            return ret;
        }

        static void PopStack(MethodBody body, Instruction inst, ref int stack)
        {
            switch (inst.OpCode.StackBehaviourPop)
            {
                case StackBehaviour.Pop0:
                    break;
                case StackBehaviour.Pop1:
                case StackBehaviour.Popi:
                case StackBehaviour.Popref:
                    stack--; break;
                case StackBehaviour.Pop1_pop1:
                case StackBehaviour.Popi_pop1:
                case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4:
                case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1:
                case StackBehaviour.Popref_popi:
                    stack -= 2; break;
                case StackBehaviour.Popi_popi_popi:
                case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4:
                case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref:
                    stack -= 3; break;
                case StackBehaviour.Varpop:
                    switch (inst.OpCode.Code)
                    {
                        case Code.Newobj:
                            stack++;
                            goto case Code.Call;
                        case Code.Call:
                        case Code.Calli:
                        case Code.Callvirt:
                            IMethodSignature sig = inst.Operand as IMethodSignature;
                            if (!sig.ExplicitThis && sig.HasThis)
                                stack--;
                            stack -= sig.Parameters.Count;
                            break;
                        case Code.Ret:
                            if (body.Method.ReturnType.MetadataType != MetadataType.Void)
                                stack--;
                            if (stack != 0)
                                throw new InvalidOperationException();
                            break;
                    } break;
                case StackBehaviour.PopAll:
                    stack = 0; break;
            }
        }
        static void PushStack(MethodBody body, Instruction inst, ref  int stack)
        {
            switch (inst.OpCode.StackBehaviourPush)
            {
                case StackBehaviour.Push0:
                    break;
                case StackBehaviour.Pushi:
                case StackBehaviour.Push1:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Pushref:
                    stack++; break;
                case StackBehaviour.Push1_push1:
                    stack += 2; break;
                case StackBehaviour.Varpush:
                    IMethodSignature sig = inst.Operand as IMethodSignature;
                    if (sig.ReturnType.MetadataType != MetadataType.Void)
                        stack++;
                    break;
            }
        }
        static int[] GetStacks(MethodBody body)
        {
            int[] stacks = new int[body.Instructions.Count];

            List<Instruction> handlerStarts = body.ExceptionHandlers.
                Where(_ => _.HandlerType == ExceptionHandlerType.Catch)
                .Select(_ => _.HandlerStart).ToList();
            List<Instruction> filterStarts = body.ExceptionHandlers.
                Where(_ => _.FilterStart != null)
                .Select(_ => _.HandlerStart).ToList();
            for (int i = 0; i < stacks.Length; i++)
            {
                if (handlerStarts.Contains(body.Instructions[i]) ||
                    filterStarts.Contains(body.Instructions[i]))
                    stacks[i] = 1;
                else if (i == 0)
                    stacks[i] = 0;
                else
                    stacks[i] = int.MinValue;
            }

            Queue<int> ps = new Queue<int>();
            ps.Enqueue(0);
            do
            {
                bool br = false;
                for (int now = ps.Dequeue(); now < body.Instructions.Count && !br; now++)
                {
                    Instruction inst = body.Instructions[now];
                    int stack = stacks[now];
                    PopStack(body, inst, ref stack);
                    PushStack(body, inst, ref stack);
                    switch (inst.OpCode.FlowControl)
                    {
                        case FlowControl.Branch:
                            {
                                int targetIdx = (inst.Operand as Instruction).Index;
                                if (stacks[targetIdx] != stack)
                                {
                                    ps.Enqueue(targetIdx);
                                    stacks[targetIdx] = stack;
                                }
                                br = true;
                            } break;
                        case FlowControl.Cond_Branch:
                            {
                                int targetIdx;
                                if (inst.OpCode.Code == Code.Switch)
                                {
                                    foreach (var i in inst.Operand as Instruction[])
                                    {
                                        targetIdx = i.Index;
                                        if (stacks[targetIdx] != stack)
                                        {
                                            ps.Enqueue(targetIdx);
                                            stacks[targetIdx] = stack;
                                        }
                                    }
                                }
                                else
                                {
                                    targetIdx = (inst.Operand as Instruction).Index;
                                    if (stacks[targetIdx] != stack)
                                    {
                                        ps.Enqueue(targetIdx);
                                        stacks[targetIdx] = stack;
                                    }
                                }
                                targetIdx = now + 1;
                                if (targetIdx < body.Instructions.Count && stacks[targetIdx] != stack)
                                {
                                    ps.Enqueue(targetIdx);
                                    stacks[targetIdx] = stack;
                                }
                                br = true;
                            } break;
                        case FlowControl.Return:
                        case FlowControl.Throw:
                            br = true;
                            break;
                        default:
                            if (now + 1 < body.Instructions.Count)
                                stacks[now + 1] = stack;
                            break;
                    }
                }
            } while (ps.Count != 0);
            return stacks;
        }
        static IList<Instruction[]> SplitStatements(MethodBody body, Instruction[] scope, int[] stacks)
        {
            //scope is continuous in order
            int baseIndex = body.Instructions.IndexOf(scope[0]);
            List<Instruction[]> ret = new List<Instruction[]>();
            List<Instruction> insts = new List<Instruction>();
            for (int i = 0; i < scope.Length; i++)
            {
                if (stacks[baseIndex + i] == 0 && insts.Count != 0 &&
                    (i == 0 || scope[i - 1].OpCode.OpCodeType != OpCodeType.Prefix))
                {
                    ret.Add(insts.ToArray());
                    insts.Clear();
                }
                insts.Add(scope[i]);
            }
            if (insts.Count != 0)
                ret.Add(insts.ToArray());
            return ret;
        }

        static ushort[] junkCode = new ushort[]  {  0x24ff, 0x77ff, 0x78ff, 0xa6ff, 0xa7ff, 
                                                    0xa8ff, 0xa9ff, 0xaaff, 0xabff, 0xacff,
                                                    0xadff, 0xaeff, 0xafff, 0xb0ff, 0xb1ff,
                                                    0xb2ff, 0xbbff, 0xbcff, 0xbdff, 0xbeff,
                                                    0xbfff, 0xc0ff, 0xc1ff, 0xc4ff, 0xc5ff,
                                                    0xc7ff, 0xc8ff, 0xc9ff, 0xcaff, 0xcbff,
                                                    0xccff, 0xcdff, 0xceff, 0xcfff, 0x08fe,
                                                    0x19fe, 0x1bfe, 0x1ffe};
        IEnumerable<Instruction> GetJunk_(VariableDefinition stateVar)
        {
            TypeDefinition type = method.DeclaringType;
            if (type.Methods.Count > 0 && Random.Next() % 2 == 0)
                yield return Instruction.Create(OpCodes.Ldtoken, type.Methods[Random.Next(0, type.Methods.Count)]);
            else if (type.Fields.Count > 0)
                yield return Instruction.Create(OpCodes.Ldtoken, type.Fields[Random.Next(0, type.Fields.Count)]);
            else
                yield return Instruction.Create(OpCodes.Ldtoken, type);
            if (genJunk)
            {
                switch (Random.Next(0, 5))
                {
                    case 0:
                        yield return Instruction.Create(OpCodes.Pop);
                        break;
                    case 1:
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next(0, 9));
                        yield return Instruction.Create(OpCodes.Stloc, stateVar);
                        yield return Instruction.Create(OpCodes.Pop);
                        break;
                    case 2:
                        Instruction inst = Instruction.Create(OpCodes.Pop);
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next(0, 9));
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next(0, 9));
                        switch (Random.Next(0, 4))
                        {
                            case 0:
                                yield return Instruction.Create(OpCodes.Bne_Un, inst);
                                break;
                            case 1:
                                yield return Instruction.Create(OpCodes.Beq, inst);
                                break;
                            case 2:
                                yield return Instruction.Create(OpCodes.Bgt, inst);
                                break;
                            case 3:
                                yield return Instruction.Create(OpCodes.Ble, inst);
                                break;
                        }
                        Instruction e = Instruction.Create(OpCodes.Break);
                        yield return Instruction.Create(OpCodes.Pop);
                        yield return Instruction.Create(OpCodes.Br, e);
                        yield return inst;
                        yield return e;
                        break;
                    case 3:
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next(0, 9));
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next(0, 9));
                        switch (Random.Next(0, 4))
                        {
                            case 0:
                                yield return Instruction.Create(OpCodes.Add);
                                break;
                            case 1:
                                yield return Instruction.Create(OpCodes.Sub);
                                break;
                            case 2:
                                yield return Instruction.Create(OpCodes.Mul);
                                break;
                            case 3:
                                yield return Instruction.Create(OpCodes.Xor);
                                break;
                        }
                        yield return Instruction.Create(OpCodes.Stloc, stateVar);
                        yield return Instruction.Create(OpCodes.Pop);
                        break;
                    case 4:
                        yield return Instruction.CreateJunkCode(junkCode[Random.Next(0, junkCode.Length)]);
                        break;
                }
            }
            else
            {
                switch (Random.Next(0, 4))
                {
                    case 0:
                        yield return Instruction.Create(OpCodes.Pop);
                        break;
                    case 1:
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next(-1, 9));
                        yield return Instruction.Create(OpCodes.Stloc, stateVar);
                        yield return Instruction.Create(OpCodes.Pop);
                        break;
                    case 2:
                        Instruction inst = Instruction.Create(OpCodes.Pop);
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next(0, 9));
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next(0, 9));
                        switch (Random.Next(0, 4))
                        {
                            case 0:
                                yield return Instruction.Create(OpCodes.Bne_Un, inst);
                                break;
                            case 1:
                                yield return Instruction.Create(OpCodes.Beq, inst);
                                break;
                            case 2:
                                yield return Instruction.Create(OpCodes.Bgt, inst);
                                break;
                            case 3:
                                yield return Instruction.Create(OpCodes.Ble, inst);
                                break;
                        }
                        Instruction e = Instruction.Create(OpCodes.Break);
                        yield return Instruction.Create(OpCodes.Pop);
                        yield return Instruction.Create(OpCodes.Br, e);
                        yield return inst;
                        yield return e;
                        break;
                    case 3:
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next());
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next());
                        switch (Random.Next(0, 4))
                        {
                            case 0:
                                yield return Instruction.Create(OpCodes.Add);
                                break;
                            case 1:
                                yield return Instruction.Create(OpCodes.Sub);
                                break;
                            case 2:
                                yield return Instruction.Create(OpCodes.Mul);
                                break;
                            case 3:
                                yield return Instruction.Create(OpCodes.Xor);
                                break;
                        }
                        yield return Instruction.Create(OpCodes.Stloc, stateVar);
                        yield return Instruction.Create(OpCodes.Pop);
                        break;
                }
            }
        }
        IEnumerable<Instruction> GetJunk(VariableDefinition stateVar)
        {
            //return new Instruction[0];
            return GetJunk_(stateVar);
        }

        Instruction GenFakeBranch(Statement self, Statement target, Statement fake, IList<Instruction> insts,
            VariableDefinition stateVar, Instruction begin)
        {
            Instruction ret;
            int num = ComputeRandNum(insts);
            switch (Random.Next(0, 4))
            {
                case 0: //if (r == r) goto target; else goto fake;
                    insts.Add(ret = Instruction.Create(OpCodes.Ldc_I4, num));
                    EncryptNum(self.Key, stateVar, target.Key, insts);
                    insts.Add(Instruction.Create(OpCodes.Beq, begin));
                    EncryptNum(self.Key, stateVar, fake.Key, insts);
                    insts.Add(Instruction.Create(OpCodes.Br, begin));
                    break;
                case 1: //if (r == r + x) goto fake; else goto target;
                    insts.Add(ret = Instruction.Create(OpCodes.Ldc_I4, num + (Random.Next() % 2 == 0 ? -1 : 1)));
                    EncryptNum(self.Key, stateVar, fake.Key, insts);
                    insts.Add(Instruction.Create(OpCodes.Beq, begin));
                    EncryptNum(self.Key, stateVar, target.Key, insts);
                    insts.Add(Instruction.Create(OpCodes.Br, begin));
                    break;
                case 2: //if (r != r) goto fake; else goto target;
                    insts.Add(ret = Instruction.Create(OpCodes.Ldc_I4, num));
                    EncryptNum(self.Key, stateVar, fake.Key, insts);
                    insts.Add(Instruction.Create(OpCodes.Bne_Un, begin));
                    EncryptNum(self.Key, stateVar, target.Key, insts);
                    insts.Add(Instruction.Create(OpCodes.Br, begin));
                    break;
                case 3: //if (r != r + x) goto target; else goto fake;
                    insts.Add(ret = Instruction.Create(OpCodes.Ldc_I4, num + (Random.Next() % 2 == 0 ? -1 : 1)));
                    EncryptNum(self.Key, stateVar, target.Key, insts);
                    insts.Add(Instruction.Create(OpCodes.Bne_Un, begin));
                    EncryptNum(self.Key, stateVar, fake.Key, insts);
                    insts.Add(Instruction.Create(OpCodes.Br, begin));
                    break;
                default:
                    throw new InvalidOperationException();
            }
            foreach (var i in GetJunk(stateVar))
                insts.Add(i);
            return ret;
        }
    }
}