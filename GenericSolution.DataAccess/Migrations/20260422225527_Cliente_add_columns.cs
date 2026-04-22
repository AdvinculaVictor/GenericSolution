using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenericSolution.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Cliente_add_columns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoPostal",
                table: "Clientes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Domicilio",
                table: "Clientes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RFC",
                table: "Clientes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodigoPostal",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Domicilio",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "RFC",
                table: "Clientes");
        }
    }
}
