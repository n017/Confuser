using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections.Specialized;

namespace Confuser.Core.Engines
{
    class BamlCopyer:BamlReader
    {
        class BamlBinaryWriter : BinaryWriter
        {
            public BamlBinaryWriter(Stream stream)
                : base(stream)
            {
            }

            public void WriteCompressedInt32(int val)
            {
                this.Write7BitEncodedInt(val);
            }
        }

        BamlDocument orginaldocument;

        public BamlCopyer(Stream src, Stream dst, BamlDocument document)
        {
            BamlBinaryReader reader = new BamlBinaryReader(src);
            BamlBinaryWriter writer = new BamlBinaryWriter(dst);
            this.orginaldocument = document;

            int length = reader.ReadInt32();
            string format = new string(new BinaryReader(src, Encoding.Unicode).ReadChars(length >> 1));
            if (format != "MSBAML")
            {
                throw new NotSupportedException();
            }

            int readerVersion = reader.ReadInt32();
            int updateVersion = reader.ReadInt32();
            int writerVersion = reader.ReadInt32();
            if ((readerVersion != 0x00600000) || (updateVersion != 0x00600000) || (writerVersion != 0x00600000))
            {
                throw new NotSupportedException();
            }

            writer.Write(0x0000000C);
            writer.Write(Encoding.Unicode.GetBytes("MSBAML"));
            writer.Write(0x00600000);
            writer.Write(0x00600000);
            writer.Write(0x00600000);

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                BamlRecordType recordType = (BamlRecordType)reader.ReadByte();

                long position = reader.BaseStream.Position;
                int size = 0;
                bool doneCopy = false;

                switch (recordType)
                {
                    case BamlRecordType.XmlnsProperty:
                    case BamlRecordType.PresentationOptionsAttribute:
                    case BamlRecordType.PIMapping:
                    case BamlRecordType.AssemblyInfo:
                    case BamlRecordType.Property:
                    case BamlRecordType.PropertyWithConverter:
                    case BamlRecordType.PropertyCustom:
                    case BamlRecordType.DefAttribute:
                    case BamlRecordType.DefAttributeKeyString:
                    case BamlRecordType.TypeInfo:
                    case BamlRecordType.AttributeInfo:
                    case BamlRecordType.StringInfo:
                    case BamlRecordType.Text:
                    case BamlRecordType.TextWithConverter:
                    case BamlRecordType.TextWithId:
                        size = reader.ReadCompressedInt32();
                        break;
                }

                switch (recordType)
                {
                    case BamlRecordType.DocumentStart:
                        document.LoadAsync = reader.ReadBoolean();
                        document.MaxAsyncRecords = reader.ReadInt32();
                        document.DebugBaml = reader.ReadBoolean();
                        break;

                    case BamlRecordType.DocumentEnd:
                        break;

                    case BamlRecordType.ElementStart:
                        this.namespaceManager.OnElementStart();
                        this.ReadElementStart(reader);
                        break;

                    case BamlRecordType.ElementEnd:
                        this.ReadElementEnd();
                        this.namespaceManager.OnElementEnd();
                        break;

                    case BamlRecordType.KeyElementStart:
                        this.ReadKeyElementStart(reader);
                        break;

                    case BamlRecordType.KeyElementEnd:
                        this.ReadKeyElementEnd();
                        break;

                    case BamlRecordType.XmlnsProperty:
                        this.ReadXmlnsProperty(reader);
                        break;

                    case BamlRecordType.PIMapping:
                        this.ReadNamespaceMapping(reader);
                        break;

                    case BamlRecordType.PresentationOptionsAttribute:
                        this.ReadPresentationOptionsAttribute(reader);
                        break;

                    case BamlRecordType.AssemblyInfo:
                        writer.Write((byte)recordType);
                        this.GetAssemblyInfoSize(reader, ref size);
                        writer.WriteCompressedInt32(size);
                        this.ReadAssemblyInfo(reader, writer);
                        doneCopy = true;
                        break;

                    case BamlRecordType.StringInfo:
                        this.ReadStringInfo(reader);
                        break;

                    case BamlRecordType.ConnectionId:
                        (elementStack.Peek() as Connection).ConnectionId = reader.ReadInt32(); // ConnectionId
                        break;

                    case BamlRecordType.Property:
                        this.ReadPropertyRecord(reader);
                        break;

                    case BamlRecordType.PropertyWithConverter:
                        writer.Write((byte)recordType);
                        this.GetPropertyWithConverterSize(reader, ref size);
                        writer.WriteCompressedInt32(size);
                        this.ReadPropertyWithConverter(reader, writer);
                        doneCopy = true;
                        break;

                    case BamlRecordType.PropertyWithExtension:
                        this.ReadPropertyWithExtension(reader);
                        break;

                    case BamlRecordType.PropertyTypeReference:
                        this.ReadPropertyTypeReference(reader);
                        break;

                    case BamlRecordType.PropertyWithStaticResourceId:
                        this.ReadPropertyWithStaticResourceIdentifier(reader);
                        break;

                    case BamlRecordType.ContentProperty:
                        this.ReadContentProperty(reader);
                        break;

                    case BamlRecordType.TypeInfo:
                        writer.Write((byte)recordType);
                        this.GetTypeInfoSize(reader, ref size);
                        writer.WriteCompressedInt32(size);
                        this.ReadTypeInfo(reader, writer);
                        doneCopy = true;
                        break;

                    case BamlRecordType.AttributeInfo:
                        writer.Write((byte)recordType);
                        this.GetAttributeInfoSize(reader, ref size);
                        writer.WriteCompressedInt32(size);
                        this.ReadAttributeInfo(reader, writer);
                        doneCopy = true;
                        break;

                    case BamlRecordType.DefAttribute:
                        this.ReadDefAttribute(reader);
                        break;

                    case BamlRecordType.DefAttributeKeyString:
                        this.ReadDefAttributeKeyString(reader);
                        break;

                    case BamlRecordType.DefAttributeKeyType:
                        this.ReadDefAttributeKeyType(reader);
                        break;

                    case BamlRecordType.Text:
                        this.ReadText(reader);
                        break;

                    case BamlRecordType.TextWithConverter:
                        this.ReadTextWithConverter(reader);
                        break;

                    case BamlRecordType.PropertyCustom:
                        this.ReadPropertyCustom(reader);
                        break;

                    case BamlRecordType.PropertyListStart:
                        this.ReadPropertyListStart(reader);
                        break;

                    case BamlRecordType.PropertyListEnd:
                        this.ReadPropertyListEnd();
                        break;

                    case BamlRecordType.PropertyDictionaryStart:
                        this.ReadPropertyDictionaryStart(reader);
                        break;

                    case BamlRecordType.PropertyDictionaryEnd:
                        this.ReadPropertyDictionaryEnd();
                        break;

                    case BamlRecordType.PropertyComplexStart:
                        this.ReadPropertyComplexStart(reader);
                        break;

                    case BamlRecordType.PropertyComplexEnd:
                        this.ReadPropertyComplexEnd();
                        break;

                    case BamlRecordType.ConstructorParametersStart:
                        this.ReadConstructorParametersStart();
                        break;

                    case BamlRecordType.ConstructorParametersEnd:
                        this.ReadConstructorParametersEnd();
                        break;

                    case BamlRecordType.ConstructorParameterType:
                        this.ReadConstructorParameterType(reader);
                        break;

                    case BamlRecordType.DeferableContentStart:
                        int contentSize = reader.ReadInt32();
                        break;

                    case BamlRecordType.StaticResourceStart:
                        this.ReadStaticResourceStart(reader);
                        break;

                    case BamlRecordType.StaticResourceEnd:
                        this.ReadStaticResourceEnd(reader);
                        break;

                    case BamlRecordType.StaticResourceId:
                        this.ReadStaticResourceIdentifier(reader);
                        break;

                    case BamlRecordType.OptimizedStaticResource:
                        this.ReadOptimizedStaticResource(reader);
                        break;

                    case BamlRecordType.LineNumberAndPosition:
                        {
                            int lineNumber = reader.ReadInt32(); // LineNumber
                            int linePosition = reader.ReadInt32(); // Position
                            writer.Write((byte)recordType); writer.Write(lineNumber); writer.Write(linePosition);
                            doneCopy = true;
                            break;
                        }

                    case BamlRecordType.LinePosition:
                        {
                            int linePosition = reader.ReadInt32(); // Position
                            writer.Write((byte)recordType); writer.Write(linePosition);
                            doneCopy = true;
                            break;
                        }

                    case BamlRecordType.TextWithId:
                        this.ReadTextWithId(reader);
                        break;

                    default:
                        throw new NotSupportedException(recordType.ToString());
                }
                if (doneCopy) continue;

                long pospos = writer.BaseStream.Position;
                if (size > 0)
                {
                    reader.BaseStream.Position = position + size;

                    long pos = reader.BaseStream.Position;

                    writer.Write((byte)recordType);
                    reader.BaseStream.Position = position;
                    byte[] buff = reader.ReadBytes(size);
                    writer.Write(buff);

                    reader.BaseStream.Position = pos;
                }
                else
                {
                    long pos = reader.BaseStream.Position;
                    size = (int)(pos - position);

                    writer.Write((byte)recordType);
                    reader.BaseStream.Position = position;
                    byte[] buff = reader.ReadBytes(size);
                    writer.Write(buff);

                    reader.BaseStream.Position = pos;
                }
            }
        }

        void GetAssemblyInfoSize(BamlBinaryReader reader, ref int size)
        {
            long pos = reader.BaseStream.Position;
            short assemblyIdentifier = reader.ReadInt16();
            string name;
            if (orginaldocument.AssemblyTable.TryGetValue(assemblyIdentifier, out name))
                size = 2 + Encoding.UTF8.GetByteCount(name);
            reader.BaseStream.Position = pos;
        }
        void ReadAssemblyInfo(BamlBinaryReader reader, BamlBinaryWriter writer)
        {
            short assemblyIdentifier = reader.ReadInt16();
            string assemblyName = reader.ReadString();
            document.AssemblyTable.Add(assemblyIdentifier, assemblyName);

            string name;
            if (orginaldocument.AssemblyTable.TryGetValue(assemblyIdentifier, out name))
            {
                writer.Write(assemblyIdentifier);
                writer.Write(name);
            }
            else
            {
                writer.Write(assemblyIdentifier);
                writer.Write(assemblyName);
            }
        }

        void GetTypeInfoSize(BamlBinaryReader reader, ref int size)
        {
            long pos = reader.BaseStream.Position;
            short typeIdentifier = reader.ReadInt16();
            TypeDeclaration t;
            if (orginaldocument.TypeTable.TryGetValue(typeIdentifier, out t))
                size = 4 + Encoding.UTF8.GetByteCount((t.Namespace == string.Empty ? string.Empty : t.Namespace + ".") + t.Name);
            reader.BaseStream.Position = pos;
        }
        void ReadTypeInfo(BamlBinaryReader reader, BamlBinaryWriter writer)
        {
            short typeIdentifier = reader.ReadInt16();
            short assemblyIdentifier = reader.ReadInt16();
            string typeFullName = reader.ReadString();

            short assemblyId = (short)(assemblyIdentifier & 0x0fff);
            string assembly = (string)document.AssemblyTable[assemblyId];

            TypeDeclaration typeDeclaration = null;

            int index = typeFullName.LastIndexOf('.');
            if (index != -1)
            {
                string name = typeFullName.Substring(index + 1);
                string namespaceName = typeFullName.Substring(0, index);
                typeDeclaration = new TypeDeclaration(name, namespaceName, assembly);
            }
            else
            {
                typeDeclaration = new TypeDeclaration(typeFullName, string.Empty, assembly);
            }

            document.TypeTable.Add(typeIdentifier, typeDeclaration);

            TypeDeclaration t;
            if (orginaldocument.TypeTable.TryGetValue(typeIdentifier, out t))
            {
                writer.Write(typeIdentifier);
                writer.Write(assemblyIdentifier);
                writer.Write((t.Namespace == string.Empty ? string.Empty : t.Namespace + ".") + t.Name);
            }
            else
            {
                writer.Write(typeIdentifier);
                writer.Write(assemblyIdentifier);
                writer.Write(typeFullName);
            }
        }

        void GetAttributeInfoSize(BamlBinaryReader reader, ref int size)
        {
            long pos = reader.BaseStream.Position;
            short attributeIdentifier = reader.ReadInt16();
            PropertyDeclaration p;
            if (orginaldocument.PropertyTable.TryGetValue(attributeIdentifier, out p))
                size = 5 + Encoding.UTF8.GetByteCount(p.Name);
            reader.BaseStream.Position = pos;
        }
        void ReadAttributeInfo(BamlBinaryReader reader, BamlBinaryWriter writer)
        {
            short attributeIdentifier = reader.ReadInt16();
            short ownerTypeIdentifier = reader.ReadInt16();
            BamlAttributeUsage attributeUsage = (BamlAttributeUsage)reader.ReadByte();
            string attributeName = reader.ReadString();

            TypeDeclaration declaringType = this.GetTypeDeclaration(ownerTypeIdentifier);
            PropertyDeclaration propertyName = new PropertyDeclaration(attributeName, declaringType);
            document.PropertyTable.Add(attributeIdentifier, propertyName);

            PropertyDeclaration p;
            if (orginaldocument.PropertyTable.TryGetValue(attributeIdentifier, out p))
            {
                writer.Write(attributeIdentifier);
                writer.Write(ownerTypeIdentifier);
                writer.Write((byte)attributeUsage);
                writer.Write(p.Name);
            }
            else
            {
                writer.Write(attributeIdentifier);
                writer.Write(ownerTypeIdentifier);
                writer.Write((byte)attributeUsage);
                writer.Write(attributeName);
            }
        }

        void GetPropertyWithConverterSize(BamlBinaryReader reader, ref int size)
        {
            size = 4 + Encoding.UTF8.GetByteCount((string)this.orginaldocument.Properties[this.document.Properties.Count].Value);
        }
        void ReadPropertyWithConverter(BamlBinaryReader reader, BamlBinaryWriter writer)
        {
            short attributeIdentifier = reader.ReadInt16();
            string value = reader.ReadString();
            short converterTypeIdentifier = reader.ReadInt16();

            writer.Write(attributeIdentifier);
            writer.Write((string)this.orginaldocument.Properties[this.document.Properties.Count].Value);
            writer.Write(converterTypeIdentifier);

            Property property = new Property(PropertyType.Value);
            property.PropertyDeclaration = this.GetPropertyDeclaration(attributeIdentifier);
            property.Converter = this.GetTypeDeclaration(converterTypeIdentifier);
            property.Value = value;

            Element element = (Element)this.elementStack.Peek();
            property.DeclaringElement = element;
            this.document.Properties.Add(property);
            element.Properties.Add(property);
        }
    }
}
