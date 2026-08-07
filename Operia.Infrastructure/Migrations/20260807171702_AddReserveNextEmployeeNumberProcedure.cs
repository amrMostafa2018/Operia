using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Operia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReserveNextEmployeeNumberProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE [dbo].[ReserveNextEmployeeNumber]
                    @TenantId nvarchar(36)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;

                    BEGIN TRANSACTION;

                    IF NOT EXISTS
                    (
                        SELECT 1
                        FROM [dbo].[TenantNumberCounters] WITH (UPDLOCK, HOLDLOCK)
                        WHERE [TenantId] = @TenantId
                    )
                    BEGIN
                        INSERT INTO [dbo].[TenantNumberCounters]
                            ([TenantId], [LastEmployeeNumber], [CreatedAt])
                        VALUES
                            (@TenantId, 0, SYSUTCDATETIME());
                    END;

                    DECLARE @Reserved TABLE ([Value] int NOT NULL);

                    UPDATE [dbo].[TenantNumberCounters] WITH (UPDLOCK, ROWLOCK)
                    SET
                        [LastEmployeeNumber] = [LastEmployeeNumber] + 1,
                        [LastModifiedAt] = SYSUTCDATETIME()
                    OUTPUT INSERTED.[LastEmployeeNumber] INTO @Reserved ([Value])
                    WHERE [TenantId] = @TenantId
                        AND [LastEmployeeNumber] < 9999999;

                    IF NOT EXISTS (SELECT 1 FROM @Reserved)
                    BEGIN
                        COMMIT TRANSACTION;
                        RETURN;
                    END;

                    COMMIT TRANSACTION;

                    SELECT [Value] FROM @Reserved;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP PROCEDURE IF EXISTS [dbo].[ReserveNextEmployeeNumber];
                """);
        }
    }
}
