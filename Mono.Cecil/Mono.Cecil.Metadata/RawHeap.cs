using System;
using System.Collections.Generic;
using System.Text;

namespace Mono.Cecil.Metadata
{
    public class RawHeap : MetadataHeap
    {
        public RawHeap(MetadataStream str) : base(str, str.Header.Name) { }

        public override void Accept(IMetadataVisitor visitor)
        {
            visitor.VisitRawHeap(this);
        }
    }
}
