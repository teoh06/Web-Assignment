using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemRatingsAndComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MenuItemComments",
                columns: table => new
                {
                    CommentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberEmail = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    MenuItemId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CommentedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemComments", x => x.CommentId);
                    table.ForeignKey(
                        name: "FK_MenuItemComments_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "MenuItemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuItemComments_Users_MemberEmail",
                        column: x => x.MemberEmail,
                        principalTable: "Users",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MenuItemRatings",
                columns: table => new
                {
                    RatingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Value = table.Column<int>(type: "int", nullable: false),
                    MemberEmail = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    MenuItemId = table.Column<int>(type: "int", nullable: false),
                    RatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemRatings", x => x.RatingId);
                    table.ForeignKey(
                        name: "FK_MenuItemRatings_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "MenuItemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuItemRatings_Users_MemberEmail",
                        column: x => x.MemberEmail,
                        principalTable: "Users",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemComments_MemberEmail",
                table: "MenuItemComments",
                column: "MemberEmail");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemComments_MenuItemId",
                table: "MenuItemComments",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemRatings_MemberEmail",
                table: "MenuItemRatings",
                column: "MemberEmail");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemRatings_MenuItemId",
                table: "MenuItemRatings",
                column: "MenuItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuItemComments");

            migrationBuilder.DropTable(
                name: "MenuItemRatings");
        }
    }
}
