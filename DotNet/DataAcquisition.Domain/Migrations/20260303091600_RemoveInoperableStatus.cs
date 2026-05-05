using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.DataAcquisition.Domain.Migrations
{
    public partial class RemoveInoperableStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [DataAcquisitionLogs] SET [Status] = 'Completed' WHERE [Status] = 'Inoperable'");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Note: We cannot easily distinguish which 'Completed' logs were originally 'Inoperable'
            // but for downgrade purposes we can just leave them as 'Completed' or if really needed 
            // we could have added a note to the log during Up migration.
        }
    }
}
