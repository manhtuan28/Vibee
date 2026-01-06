using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vibee.Migrations
{
    /// <inheritdoc />
    public partial class AddPostEmotion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Emotion",
                table: "Posts",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Emotion",
                table: "Posts");
        }
    }
}
