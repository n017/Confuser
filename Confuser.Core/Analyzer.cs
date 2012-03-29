using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace Confuser.Core
{
    public abstract class Analyzer : IProgressProvider
    {
        protected Logger Logger { get; private set; }
        protected IProgresser Progresser { get; private set; }
        public abstract void Analyze(IEnumerable<AssemblyDefinition> asms);

        internal void SetLogger(Logger logger)
        {
            this.Logger = logger;
        }
        public void SetProgresser(IProgresser progresser)
        {
            this.Progresser = progresser;
        }
    }
}
