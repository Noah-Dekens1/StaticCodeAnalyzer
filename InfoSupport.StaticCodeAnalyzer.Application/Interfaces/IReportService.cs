﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Domain;

namespace InfoSupport.StaticCodeAnalyzer.Application.Interfaces;
public interface IReportService
{
    public Task<Report?> GetReportById(Guid id, CancellationToken cancellationToken);
    public Task DeleteReportById(Guid id, CancellationToken cancellationToken);
}
