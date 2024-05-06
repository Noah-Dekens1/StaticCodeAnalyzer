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

    public async Task DeleteReportById(Guid id, CancellationToken cancellationToken)
    {
        var report = await _context.Reports.FindAsync(id);

        if (report is null)
            return;

        _context.Reports.Remove(report);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Report?> GetReportById(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Reports
            .Where(r => r.Id == id)
            .Include(r => r.ProjectFiles)
            .ThenInclude(f => f.Issues)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
