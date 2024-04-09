﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Utils;
public class AstTraverser
{
    public virtual void Visit(AstNode node)
    {
        foreach (var child in node.Children)
        {
            Visit(child);
        }
    }
}