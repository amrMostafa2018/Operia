using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Employees.Commands.CreateEmployee;

namespace Operia.Infrastructure.Persistence;

internal sealed class EmployeeCodeGenerator(ApplicationDbContext db) : IEmployeeCodeGenerator
{
    public async Task<string> ReserveNextAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var reservedNumbers = await db.Database
            .SqlQuery<int>($"EXEC dbo.ReserveNextEmployeeNumber @TenantId={tenantId}")
            .ToListAsync(cancellationToken);

        var nextNumber = reservedNumbers.SingleOrDefault();

        if (nextNumber == 0)
        {
            throw new ConflictException(
                "Employee code range EMP-0001 through EMP-9999999 is exhausted.");
        }

        return $"EMP-{nextNumber:0000}";
    }
}
