using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace Confuser.Core
{
    public interface IEngine
    {
        void Analysis(Logger logger, IEnumerable<AssemblyDefinition> asms);
    }
}
