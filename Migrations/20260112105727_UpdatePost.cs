using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vibee.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FakeLikeCount",
                table: "Posts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FakeShareCount",
                table: "Posts",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FakeLikeCount",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "FakeShareCount",
                table: "Posts");
        }
    }
}
