using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Agent.Generator
{
    public class DiskImmutableCompleter : ImmutableCompleter
    {
        public DiskImmutableCompleter(string solutionPath)
            : base(Roslyn.Services.Solution.Load(solutionPath)) {}

        protected override void Update(Roslyn.Services.IDocument document, string newText)
        {
            if (document.GetText().GetText() != newText)
                File.WriteAllText(document.FilePath, newText);
        }
    }
}
