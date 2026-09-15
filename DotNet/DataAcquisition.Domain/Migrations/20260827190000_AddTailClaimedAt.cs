using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    [DbContext(typeof(DataAcquisitionDbContext))]
    [Migration("20260827190000_AddTailClaimedAt")]
    public partial class AddTailClaimedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TailClaimedAt",
                table: "DataAcquisitionLog",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TailClaimedAt",
                table: "DataAcquisitionLog");
        }
    }
}
