using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.Interfaces;
using InfoSupport.StaticCodeAnalyzer.Domain;
using InfoSupport.StaticCodeAnalyzer.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;

namespace InfoSupport.StaticCodeAnalyzer.Application.Services;
public class ReportService(ApplicationDbContext context) : IReportService
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Report?> GetReportById(Guid id)
    {
        return await _context.Reports
            .Where(r => r.Id == id)
            .Include(r => r.ProjectFiles)
            .ThenInclude(f => f.Issues)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync();
    }
}
