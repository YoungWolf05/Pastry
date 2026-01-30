using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PastryManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFileAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    S3Key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileAttachments_Users_UploadedBy",
                        column: x => x.UploadedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileAttachments_EntityType_EntityId",
                table: "FileAttachments",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_FileAttachments_S3Key",
                table: "FileAttachments",
                column: "S3Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileAttachments_UploadedBy",
                table: "FileAttachments",
                column: "UploadedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileAttachments");
        }
    }
}
