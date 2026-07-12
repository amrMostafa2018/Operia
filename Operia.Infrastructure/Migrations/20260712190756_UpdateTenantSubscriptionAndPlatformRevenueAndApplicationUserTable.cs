using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Operia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTenantSubscriptionAndPlatformRevenueAndApplicationUserTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlatformRevenues_TenantSubscriptions_SubscriptionId",
                table: "PlatformRevenues");

            migrationBuilder.DropIndex(
                name: "IX_PlatformRevenues_SubscriptionId",
                table: "PlatformRevenues");

            migrationBuilder.DropColumn(
                name: "ScreenShotUrl",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "SubscriptionId",
                table: "PlatformRevenues");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "RegistrationRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ScreenShotUrl",
                table: "PlatformRevenues",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PlatformRevenues",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FullName",
                table: "RegistrationRequests");

            migrationBuilder.DropColumn(
                name: "ScreenShotUrl",
                table: "PlatformRevenues");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PlatformRevenues");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<string>(
                name: "ScreenShotUrl",
                table: "TenantSubscriptions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionId",
                table: "PlatformRevenues",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformRevenues_SubscriptionId",
                table: "PlatformRevenues",
                column: "SubscriptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlatformRevenues_TenantSubscriptions_SubscriptionId",
                table: "PlatformRevenues",
                column: "SubscriptionId",
                principalTable: "TenantSubscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
