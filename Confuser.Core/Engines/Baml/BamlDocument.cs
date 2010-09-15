using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

namespace Confuser.Core.Engines
{
    class TypeDeclaration
    {
        private string name;
        private string namespaceName;
        private string assembly;
        private string xmlPrefix;

        public TypeDeclaration(string name)
        {
            this.name = name;
            this.namespaceName = string.Empty;
            this.assembly = string.Empty;
        }

        public TypeDeclaration(string name, string namespaceName, string assembly)
        {
            this.name = name;
            this.namespaceName = namespaceName;
            this.assembly = assembly;
        }

        public TypeDeclaration Copy(string xmlPrefix)
        {
            TypeDeclaration copy = new TypeDeclaration(this.name, this.namespaceName, this.assembly);
            copy.xmlPrefix = xmlPrefix;
            return copy;
        }

        public string Name
        {
            get
            {
                return this.name;
            }
            set
            {
                this.name = value;
            }
        }

        public string Namespace
        {
            get
            {
                return this.namespaceName;
            }
            set
            {
                this.namespaceName = value;
            }
        }

        public string Assembly
        {
            get
            {
                return this.assembly;
            }
        }

        public string XmlPrefix
        {
            get { return this.xmlPrefix; }
        }

        public override string ToString()
        {
            if (null == this.xmlPrefix || 0 == this.xmlPrefix.Length)
                return this.Name;

            return this.xmlPrefix + ":" + this.Name;
        }
    }
    enum PropertyType
    {
        Value,
        Content,
        Declaration,
        List,
        Dictionary,
        Complex,
        Namespace
    }
    class PropertyDeclaration
    {
        private string name;
        private TypeDeclaration declaringType;

        public PropertyDeclaration(string name)
        {
            this.name = name;
            this.declaringType = null;
        }

        public PropertyDeclaration(string name, TypeDeclaration declaringType)
        {
            this.name = name;
            this.declaringType = declaringType;
        }

        public TypeDeclaration DeclaringType
        {
            get
            {
                return this.declaringType;
            }
        }

        public string Name
        {
            get
            {
                return this.name;
            }
            set
            {
                this.name = value;
            }
        }

        public override string ToString()
        {
            if ((this.DeclaringType != null) && (this.DeclaringType.Name == "XmlNamespace") && (this.DeclaringType.Namespace == null) && (this.DeclaringType.Assembly == null))
            {
                if ((this.Name == null) || (this.Name.Length == 0))
                {
                    return "xmlns";
                }

                return "xmlns:" + this.Name;
            }

            return this.Name;
        }
    }

    abstract class Connection
    {
        int cid = -1;
        public int ConnectionId { get { return cid; } set { cid = value; } }
    }
    class Property : Connection
    {
        private Element element;
        private PropertyType propertyType;
        private PropertyDeclaration propertyDeclaration;
        private TypeDeclaration converter;
        private object value;

        public Property(PropertyType propertyType)
        {
            this.propertyType = propertyType;
        }

        public Element DeclaringElement
        {
            get
            {
                return this.element;
            }
            set
            {
                this.element = value;
            }
        }

        public PropertyType PropertyType
        {
            get
            {
                return this.propertyType;
            }
        }

        public PropertyDeclaration PropertyDeclaration
        {
            get
            {
                return this.propertyDeclaration;
            }

            set
            {
                this.propertyDeclaration = value;
            }
        }

        public object Value
        {
            get
            {
                return this.value;
            }

            set
            {
                this.value = value;
            }
        }

        public TypeDeclaration Converter
        {
            get
            {
                return this.converter;
            }

            set
            {
                this.converter = value;
            }
        }

        public override string ToString()
        {
            return this.PropertyDeclaration.Name;
        }
    }
    class Element : Connection
    {
        private TypeDeclaration typeDeclaration;
        private Collection<Property> properties = new Collection<Property>();
        private List<object> arguments = new List<object>();

        public TypeDeclaration TypeDeclaration
        {
            get
            {
                return this.typeDeclaration;
            }

            set
            {
                this.typeDeclaration = value;
            }
        }

        public Collection<Property> Properties
        {
            get
            {
                return this.properties;
            }
        }

        public List<object> Arguments
        {
            get
            {
                return this.arguments;
            }
        }

        public override string ToString()
        {
            return "<" + this.TypeDeclaration.ToString() + ">";
        }
    }

    class BamlDocument
    {
        Dictionary<short, string> assemblyTable = new Dictionary<short, string>();
        Dictionary<short, string> stringTable = new Dictionary<short, string>();
        Dictionary<short, TypeDeclaration> typeTable = new Dictionary<short, TypeDeclaration>();
        Dictionary<short, PropertyDeclaration> propertyTable = new Dictionary<short, PropertyDeclaration>();
        List<object> staticResourceTable = new List<object>();
        Element element;
        List<Element> elements = new List<Element>();
        List<Property> properties = new List<Property>();

        public IDictionary<short, string> AssemblyTable { get { return assemblyTable; } }
        public IDictionary<short, string> StringTable { get { return stringTable; } }
        public IDictionary<short, TypeDeclaration> TypeTable { get { return typeTable; } }
        public IDictionary<short, PropertyDeclaration> PropertyTable { get { return propertyTable; } }
        public IList<object> StaticResourceTable { get { return staticResourceTable; } }
        public Element RootElement { get { return element; } internal set { element = value; } }
        public IList<Element> Elements { get { return elements; } }
        public IList<Property> Properties { get { return properties; } }

        bool loadAsync;
        int maxAsyncRecords;
        bool debugBaml;
        public bool LoadAsync { get { return loadAsync; } set { loadAsync = value; } }
        public int MaxAsyncRecords { get { return maxAsyncRecords; } set { maxAsyncRecords = value; } }
        public bool DebugBaml { get { return debugBaml; } set { debugBaml = value; } }

        internal BamlDocument() { }
    }
}
