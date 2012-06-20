using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil.Cil;
using Mono.Cecil;

namespace Confuser.Core
{
    class Mutator
    {
        public int[] IntKeys { get; set; }
        public long[] LongKeys { get; set; }
        public string[] StringKeys { get; set; }
        public Instruction Placeholder { get; private set; }
        public Instruction Delayed0 { get; private set; }
        public Instruction Delayed1 { get; private set; }
        public bool IsDelayed { get; set; }

        public void Mutate(TypeDefinition typeDef)
        {
            foreach (var i in typeDef.NestedTypes)
                Mutate(i);
            foreach (var i in typeDef.Methods)
                if (i.HasBody)
                    Mutate(i.Body);
        }
        public void Mutate(MethodBody body)
        {
            foreach (var i in body.Instructions)
            {
                FieldReference field = i.Operand as FieldReference;
                if (field != null && field.DeclaringType.FullName == "Mutation")
                {
                    switch (field.Name)
                    {
                        case "Key0I":
                            i.Operand = IntKeys[0]; goto case "I";
                        case "Key1I":
                            i.Operand = IntKeys[1]; goto case "I";
                        case "Key2I":
                            i.Operand = IntKeys[2]; goto case "I";
                        case "Key3I":
                            i.Operand = IntKeys[3]; goto case "I";
                        case "Key4I":
                            i.Operand = IntKeys[4]; goto case "I";
                        case "Key5I":
                            i.Operand = IntKeys[5]; goto case "I";
                        case "Key6I":
                            i.Operand = IntKeys[6]; goto case "I";
                        case "Key7I":
                            i.Operand = IntKeys[7]; goto case "I";

                        case "Key0L":
                            i.Operand = LongKeys[0]; goto case "L";
                        case "Key1L":
                            i.Operand = LongKeys[1]; goto case "L";
                        case "Key2L":
                            i.Operand = LongKeys[2]; goto case "L";
                        case "Key3L":
                            i.Operand = LongKeys[3]; goto case "L";
                        case "Key4L":
                            i.Operand = LongKeys[4]; goto case "L";
                        case "Key5L":
                            i.Operand = LongKeys[5]; goto case "L";
                        case "Key6L":
                            i.Operand = LongKeys[6]; goto case "L";
                        case "Key7L":
                            i.Operand = LongKeys[7]; goto case "L";

                        case "Key0S":
                            i.Operand = StringKeys[0]; goto case "S";
                        case "Key1S":
                            i.Operand = StringKeys[1]; goto case "S";
                        case "Key2S":
                            i.Operand = StringKeys[2]; goto case "S";
                        case "Key3S":
                            i.Operand = StringKeys[3]; goto case "S";

                        case "Key0Delayed":
                            if (IsDelayed)
                            {
                                i.Operand = IntKeys[0];
                                goto case "I";
                            }
                            else
                                Delayed0 = i;
                            break;
                        case "Key1Delayed":
                            if (IsDelayed)
                            {
                                i.Operand = IntKeys[1]; 
                                goto case "I";
                            }
                            else
                                Delayed1 = i;
                            break;

                        case "I":
                            i.OpCode = OpCodes.Ldc_I4; break;
                        case "L":
                            i.OpCode = OpCodes.Ldc_I8; break;
                        case "S":
                            i.OpCode = OpCodes.Ldstr; break;
                    }
                }
                MethodReference method = i.Operand as MethodReference;
                if (method != null && method.DeclaringType.FullName == "Mutation")
                {
                    if (method.Name == "Placeholder")
                        Placeholder = i;
                    else if (method.Name == "DeclaringType")
                    {
                        i.OpCode = OpCodes.Ldtoken;
                        i.Operand = body.Method.DeclaringType;
                    }
                }
            }
        }
    }
}
