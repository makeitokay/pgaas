using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClusterStatusAndConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Clusters",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ClusterConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClusterId = table.Column<int>(type: "integer", nullable: false),
                    StorageSize = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClusterConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClusterConfigurations_Clusters_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "Clusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clusters_SystemName",
                table: "Clusters",
                column: "SystemName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClusterConfigurations_ClusterId",
                table: "ClusterConfigurations",
                column: "ClusterId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClusterConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_Clusters_SystemName",
                table: "Clusters");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Clusters");
        }
    }
}
