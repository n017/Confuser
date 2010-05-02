using System;
using System.Collections.Generic;
using System.Text;

namespace Mono.Cecil.Cil
{
    public class UnmanagedMethodBody : MethodBody
    {
        ScopeCollection m_scopes;
        MethodDefinition m_method;

        public UnmanagedMethodBody(MethodDefinition meth)
		{
			m_method = meth;
		}

        byte[] c;
        public byte[] Codes { get { return c; } set { c = value; } }

        internal override MethodBody Clone(MethodDefinition parent, ImportContext context)
        {
            UnmanagedMethodBody ret = new UnmanagedMethodBody(parent);
            ret.c = this.c;
            return ret;
        }

        public override ScopeCollection Scopes
        {
            get
            {
                if (m_scopes == null)
                    m_scopes = new ScopeCollection(this);
                return m_scopes;
            }
        }

        public override void Accept(ICodeVisitor visitor)
        {
            visitor.VisitMethodBody(this);
            visitor.TerminateMethodBody(this);
        }

		public MethodDefinition Method {
			get { return m_method; }
		}
    }
}
