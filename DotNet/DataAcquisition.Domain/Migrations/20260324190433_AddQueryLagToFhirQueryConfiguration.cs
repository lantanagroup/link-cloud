using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddQueryLagToFhirQueryConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "QueryLag",
                table: "fhirQueryConfiguration",
                type: "time",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QueryLag",
                table: "fhirQueryConfiguration");
        }
    }
}
