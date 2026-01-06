using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vibee.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePostMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "Posts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Posts",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationName",
                table: "Posts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Posts",
                type: "float",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PostMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    PostId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostMedia_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PostMedia_PostId",
                table: "PostMedia",
                column: "PostId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PostMedia");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "LocationName",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Posts");
        }
    }
}
