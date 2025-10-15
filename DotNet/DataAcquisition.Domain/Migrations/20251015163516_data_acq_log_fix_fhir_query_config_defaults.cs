using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class data_acq_log_fix_fhir_query_config_defaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE FhirQueryConfiguration
SET MinAcquisitionPullTime = NULL,
    MaxAcquisitionPullTime = NULL
WHERE MinAcquisitionPullTime = '00:00:00'
   OR MaxAcquisitionPullTime = '00:00:00'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE FhirQueryConfiguration SET MinAcquisitionPullTime = '00:00:00', MaxAcquisitionPullTime = '00:00:00'");
        }
    }
}
