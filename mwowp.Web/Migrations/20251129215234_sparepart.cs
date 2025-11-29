using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace mwowp.Web.Migrations
{
    /// <inheritdoc />
    public partial class sparepart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "WorkOrderSpareParts",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "WorkOrderSpareParts");
        }
    }
}
