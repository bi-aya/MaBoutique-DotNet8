using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maboutique.Migrations
{
    /// <inheritdoc />
    public partial class Addquantite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Quantité",
                table: "Produit",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Quantité",
                table: "Produit");
        }
    }
}
