using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BossFind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CandidateFactProfileRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_CandidateFacts_CandidateProfiles_ProfileId",
                table: "CandidateFacts",
                column: "ProfileId",
                principalTable: "CandidateProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CandidateFacts_CandidateProfiles_ProfileId",
                table: "CandidateFacts");
        }
    }
}
