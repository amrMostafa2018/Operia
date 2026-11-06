using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Operia.Infrastructure.Persistence;

#nullable disable

namespace Operia.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261106121000_AddInstallmentDueDates")]
public partial class AddInstallmentDueDates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>("DueOn", "CustomerPackageInstallments", "date",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("DueOn", "CustomerPackageInstallments");
    }
}
