using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Confuser.Core.Analyzers.Baml;
using System.IO;
using System.Resources;

namespace Confuser.Core.Analyzers
{
    struct Identifier
    {
        public string scope;
        public string name;
        public int hash;
    }
    interface IReference
    {
        void UpdateReference(Identifier old, Identifier @new);
    }

    class ResourceReference : IReference
    {
        public ResourceReference(Resource res) { this.res = res; }
        Resource res;
        public void UpdateReference(Identifier old, Identifier @new)
        {
            string oldN = string.IsNullOrEmpty(old.scope) ? old.name : old.scope + "." + old.name;
            string newN = string.IsNullOrEmpty(@new.scope) ? @new.name : @new.scope + "." + @new.name;
            res.Name = res.Name.Replace(oldN, newN);
        }
    }
    class ResourceNameReference : IReference
    {
        public ResourceNameReference(Instruction inst) { this.inst = inst; }
        Instruction inst;
        public void UpdateReference(Identifier old, Identifier @new)
        {
            inst.Operand = @new.scope;
        }
    }
    class SpecificationReference : IReference
    {
        public SpecificationReference(MemberReference refer) { this.refer = refer; }
        MemberReference refer;
        public void UpdateReference(Identifier old, Identifier @new)
        {
            MethodSpecification mSpec = refer as MethodSpecification;
            if (mSpec == null || !(mSpec.DeclaringType.GetElementType() is TypeDefinition))
            {
                TypeSpecification tSpec = refer.DeclaringType as TypeSpecification;
                TypeDefinition par = tSpec.GetElementType() as TypeDefinition;
                if (tSpec != null && par != null)
                {
                    refer.Name = @new.name;
                }
            }
        }
    }
    class CustomAttributeReference : IReference
    {
        public CustomAttributeReference(TypeReference refer) { this.refer = refer; }
        TypeReference refer;
        public void UpdateReference(Identifier old, Identifier @new)
        {
            refer.Namespace = @new.scope;
            refer.Name = @new.name;
        }
    }
    class ReflectionReference : IReference
    {
        public ReflectionReference(Instruction ldstr) { this.ldstr = ldstr; }
        Instruction ldstr;
        public void UpdateReference(Identifier old, Identifier @new)
        {
            string op = (string)ldstr.Operand;
            if (op == old.name)
                ldstr.Operand = @new.name;
            else if (op == old.scope)
                ldstr.Operand = @new.scope;
            else if (op == old.scope + "." + old.name)
                ldstr.Operand = string.IsNullOrEmpty(@new.scope) ? @new.name : @new.scope + "." + @new.name;
        }
    }
    class VirtualMethodReference : IReference
    {
        public VirtualMethodReference(MethodDefinition mtdRefer) { this.mtdRefer = mtdRefer; }
        MethodDefinition mtdRefer;
        public void UpdateReference(Identifier old, Identifier @new)
        {
            mtdRefer.Name = @new.name;
            Identifier id = (Identifier)(mtdRefer as IAnnotationProvider).Annotations["RenId"];
            Identifier n = @new;
            n.scope = mtdRefer.DeclaringType.FullName;
            foreach (IReference refer in (mtdRefer as IAnnotationProvider).Annotations["RenRef"] as List<IReference>)
            {
                refer.UpdateReference(id, n);
            }
        }
    }
    class BamlTypeReference : IReference
    {
        public BamlTypeReference(TypeInfoRecord typeRec) { this.typeRec = typeRec; }

        TypeInfoRecord typeRec;

        public void UpdateReference(Identifier old, Identifier @new)
        {
            typeRec.TypeFullName = string.IsNullOrEmpty(@new.scope) ? @new.name : @new.scope + "." + @new.name;
        }
    }
    class BamlTypeExtReference : IReference
    {
        public BamlTypeExtReference(PropertyWithConverterRecord rec, BamlDocument doc, string assembly)
        {
            this.rec = rec;
            this.doc = doc;
            this.assembly = assembly;
        }

        PropertyWithConverterRecord rec;
        string assembly;
        BamlDocument doc;

        public void UpdateReference(Identifier old, Identifier @new)
        {
            string prefix = rec.Value.Substring(0, rec.Value.IndexOf(':'));
            if (old.scope != @new.scope)
            {
                string xmlNamespace = "clr-namespace:" + @new.scope;
                if (@new.scope == null || !string.IsNullOrEmpty(assembly))
                    xmlNamespace += ";assembly=" + assembly;

                for (int i = 0; i < doc.Count; i++)
                {
                    XmlnsPropertyRecord xmlns = doc[i] as XmlnsPropertyRecord;
                    if (xmlns != null)
                    {
                        if (xmlns.XmlNamespace == xmlNamespace)
                        {
                            prefix = xmlns.Prefix;
                            break;
                        }
                        else if (xmlns.Prefix == prefix)
                        {
                            XmlnsPropertyRecord r = new XmlnsPropertyRecord();
                            r.AssemblyIds = xmlns.AssemblyIds;
                            r.Prefix = prefix = ObfuscationHelper.GetNewName(xmlns.Prefix, NameMode.Letters);
                            r.XmlNamespace = xmlNamespace;
                            doc.Insert(i, r);
                            break;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(prefix))
                rec.Value = prefix + ":" + @new.name;
            else
                rec.Value = @new.name;
        }
    }
    class BamlAttributeReference : IReference
    {
        public BamlAttributeReference(AttributeInfoRecord attrRec) { this.attrRec = attrRec; }

        AttributeInfoRecord attrRec;

        public void UpdateReference(Identifier old, Identifier @new)
        {
            attrRec.Name = @new.name;
        }
    }
    class BamlPropertyReference : IReference
    {
        public BamlPropertyReference(PropertyRecord propRec) { this.propRec = propRec; }

        PropertyRecord propRec;

        public void UpdateReference(Identifier old, Identifier @new)
        {
            propRec.Value = @new.name;
        }
    }
    class BamlPathReference : IReference
    {
        public BamlPathReference(BamlRecord rec, int startIdx, int endIdx)
        {
            this.rec = rec;
            this.startIdx = startIdx;
            this.endIdx = endIdx;
        }

        BamlRecord rec;
        int startIdx;
        int endIdx;
        internal List<BamlPathReference> refers;

        public void UpdateReference(Identifier old, Identifier @new)
        {
            StringBuilder sb;
            if (rec is TextRecord)
                sb = new StringBuilder((rec as TextRecord).Value);
            else
                sb = new StringBuilder((rec as PropertyWithConverterRecord).Value);
            sb.Remove(startIdx, endIdx - startIdx + 1);
            sb.Insert(startIdx, @new.name);
            if (rec is TextRecord)
                (rec as TextRecord).Value = sb.ToString();
            else
                (rec as PropertyWithConverterRecord).Value = sb.ToString();
            int oEndIdx = endIdx;
            endIdx = startIdx + @new.name.Length - 1;
            foreach (var i in refers)
                if (this != i)
                {
                    if (i.startIdx > this.startIdx)
                        i.startIdx = i.startIdx + (endIdx - oEndIdx);
                    if (i.endIdx > this.startIdx)
                        i.endIdx = i.endIdx + (endIdx - oEndIdx);
                }
        }
    }
    class SaveBamlsReference : IReference
    {
        public SaveBamlsReference(ModuleDefinition mod, int resId) { this.mod = mod; this.resId = resId; }

        ModuleDefinition mod;
        int resId;

        public void UpdateReference(Identifier old, Identifier @new)
        {
            EmbeddedResource res = mod.Resources[resId] as EmbeddedResource;
            foreach (KeyValuePair<string, BamlDocument> pair in (res as IAnnotationProvider).Annotations["Gbamls"] as Dictionary<string, BamlDocument>)
            {
                Stream dst = new MemoryStream();
                BamlWriter.WriteDocument(pair.Value, dst);
                ((res as IAnnotationProvider).Annotations["Gresources"] as Dictionary<string, object>)[pair.Key] = dst;
            }
            MemoryStream newRes = new MemoryStream();
            ResourceWriter wtr = new ResourceWriter(newRes);
            foreach (KeyValuePair<string, object> pair in (res as IAnnotationProvider).Annotations["Gresources"] as Dictionary<string, object>)
                wtr.AddResource(pair.Key, pair.Value);
            wtr.Generate();
            mod.Resources[resId] = new EmbeddedResource(res.Name, res.Attributes, newRes.ToArray());
        }
    }
    class IvtMemberReference : IReference
    {
        public IvtMemberReference(MemberReference memRef) { this.memRef = memRef; }

        MemberReference memRef;

        public void UpdateReference(Identifier old, Identifier @new)
        {
            if (memRef is MethodReference || memRef is FieldReference)
            {
                memRef.Name = @new.name;
            }
            else if (memRef is TypeReference)
            {
                memRef.Name = @new.name;
                ((TypeReference)memRef).Namespace = @new.scope;
            }
        }

    }
}
