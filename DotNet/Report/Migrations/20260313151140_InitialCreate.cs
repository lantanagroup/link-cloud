using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Report.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "quartz");

            migrationBuilder.CreateTable(
                name: "QRTZ_CALENDARS",
                schema: "quartz",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CALENDAR_NAME = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CALENDAR = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QRTZ_CALENDARS", x => new { x.SCHED_NAME, x.CALENDAR_NAME });
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_FIRED_TRIGGERS",
                schema: "quartz",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ENTRY_ID = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    TRIGGER_NAME = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TRIGGER_GROUP = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    INSTANCE_NAME = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FIRED_TIME = table.Column<long>(type: "bigint", nullable: false),
                    SCHED_TIME = table.Column<long>(type: "bigint", nullable: false),
                    PRIORITY = table.Column<int>(type: "int", nullable: false),
                    STATE = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    JOB_NAME = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    JOB_GROUP = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IS_NONCONCURRENT = table.Column<bool>(type: "bit", nullable: false),
                    REQUESTS_RECOVERY = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QRTZ_FIRED_TRIGGERS", x => new { x.SCHED_NAME, x.ENTRY_ID });
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_JOB_DETAILS",
                schema: "quartz",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    JOB_NAME = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    JOB_GROUP = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DESCRIPTION = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    JOB_CLASS_NAME = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    IS_DURABLE = table.Column<bool>(type: "bit", nullable: false),
                    IS_NONCONCURRENT = table.Column<bool>(type: "bit", nullable: false),
                    IS_UPDATE_DATA = table.Column<bool>(type: "bit", nullable: false),
                    REQUESTS_RECOVERY = table.Column<bool>(type: "bit", nullable: false),
                    JOB_DATA = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QRTZ_JOB_DETAILS", x => new { x.SCHED_NAME, x.JOB_NAME, x.JOB_GROUP });
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_LOCKS",
                schema: "quartz",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    LOCK_NAME = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QRTZ_LOCKS", x => new { x.SCHED_NAME, x.LOCK_NAME });
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_PAUSED_TRIGGER_GRPS",
                schema: "quartz",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TRIGGER_GROUP = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QRTZ_PAUSED_TRIGGER_GRPS", x => new { x.SCHED_NAME, x.TRIGGER_GROUP });
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_SCHEDULER_STATE",
                schema: "quartz",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    INSTANCE_NAME = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LAST_CHECKIN_TIME = table.Column<long>(type: "bigint", nullable: false),
                    CHECKIN_INTERVAL = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QRTZ_SCHEDULER_STATE", x => new { x.SCHED_NAME, x.INSTANCE_NAME });
                });

            migrationBuilder.CreateTable(
                name: "ReportSchedule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FacilityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReportStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReportEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmitReportDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EnableSubmission = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EndOfReportPeriodJobHasRun = table.Column<bool>(type: "bit", nullable: false),
                    AdHocType = table.Column<int>(type: "int", nullable: true),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    PayloadRootUri = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ReportSc__3214EC079272F612", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_TRIGGERS",
                schema: "quartz",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TRIGGER_NAME = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TRIGGER_GROUP = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    JOB_NAME = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    JOB_GROUP = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DESCRIPTION = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    NEXT_FIRE_TIME = table.Column<long>(type: "bigint", nullable: true),
                    PREV_FIRE_TIME = table.Column<long>(type: "bigint", nullable: true),
                    PRIORITY = table.Column<int>(type: "int", nullable: true),
                    TRIGGER_STATE = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TRIGGER_TYPE = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    START_TIME = table.Column<long>(type: "bigint", nullable: false),
                    END_TIME = table.Column<long>(type: "bigint", nullable: true),
                    CALENDAR_NAME = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MISFIRE_INSTR = table.Column<short>(type: "smallint", nullable: true),
                    JOB_DATA = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QRTZ_TRIGGERS", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                    table.ForeignKey(
                        name: "FK_QRTZ_TRIGGERS_QRTZ_JOB_DETAILS_SCHED_NAME_JOB_NAME_JOB_GROUP",
                        columns: x => new { x.SCHED_NAME, x.JOB_NAME, x.JOB_GROUP },
                        principalSchema: "quartz",
                        principalTable: "QRTZ_JOB_DETAILS",
                        principalColumns: new[] { "SCHED_NAME", "JOB_NAME", "JOB_GROUP" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReportEntry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FacilityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReportScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReportingStatus = table.Column<int>(type: "int", nullable: false),
                    SubmissionStatus = table.Column<int>(type: "int", nullable: true),
                    SubmitReportDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AggregateReportUri = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    AggregateReportBlobName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ReportEn__3214EC0798E7BCE5", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportEntries_ReportSchedules",
                        column: x => x.ReportScheduleId,
                        principalTable: "ReportSchedule",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReportPopulation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FacilityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReportScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Measure = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReportType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ReportPo__3214EC07B5921BDB", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportPopulations_ReportSchedules",
                        column: x => x.ReportScheduleId,
                        principalTable: "ReportSchedule",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReportResource",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FacilityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReportScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MeasureReportId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ReportRe__3214EC076E900CEA", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportResources_ReportSchedules",
                        column: x => x.ReportScheduleId,
                        principalTable: "ReportSchedule",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ScheduleReportType",
                columns: table => new
                {
                    ReportScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleReportType", x => new { x.ReportScheduleId, x.ReportType });
                    table.ForeignKey(
                        name: "FK_ReportScheduleReportTypes_ReportSchedules",
                        column: x => x.ReportScheduleId,
                        principalTable: "ReportSchedule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_BLOB_TRIGGERS",
                schema: "quartz",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TRIGGER_NAME = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TRIGGER_GROUP = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    BLOB_DATA = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QRTZ_BLOB_TRIGGERS", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                    table.ForeignKey(
                        name: "FK_QRTZ_BLOB_TRIGGERS_QRTZ_TRIGGERS_SCHED_NAME_TRIGGER_NAME_TRIGGER_GROUP",
                        columns: x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP },
                        principalSchema: "quartz",
                        principalTable: "QRTZ_TRIGGERS",
                        principalColumns: new[] { "SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_CRON_TRIGGERS",
                schema: "quartz",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TRIGGER_NAME = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TRIGGER_GROUP = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CRON_EXPRESSION = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TIME_ZONE_ID = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QRTZ_CRON_TRIGGERS", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                    table.ForeignKey(
                        name: "FK_QRTZ_CRON_TRIGGERS_QRTZ_TRIGGERS_SCHED_NAME_TRIGGER_NAME_TRIGGER_GROUP",
                        columns: x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP },
                        principalSchema: "quartz",
                        principalTable: "QRTZ_TRIGGERS",
                        principalColumns: new[] { "SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_SIMPLE_TRIGGERS",
                schema: "quartz",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TRIGGER_NAME = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TRIGGER_GROUP = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    REPEAT_COUNT = table.Column<long>(type: "bigint", nullable: false),
                    REPEAT_INTERVAL = table.Column<long>(type: "bigint", nullable: false),
                    TIMES_TRIGGERED = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QRTZ_SIMPLE_TRIGGERS", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                    table.ForeignKey(
                        name: "FK_QRTZ_SIMPLE_TRIGGERS_QRTZ_TRIGGERS_SCHED_NAME_TRIGGER_NAME_TRIGGER_GROUP",
                        columns: x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP },
                        principalSchema: "quartz",
                        principalTable: "QRTZ_TRIGGERS",
                        principalColumns: new[] { "SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_SIMPROP_TRIGGERS",
                schema: "quartz",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TRIGGER_NAME = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TRIGGER_GROUP = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    STR_PROP_1 = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    STR_PROP_2 = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    STR_PROP_3 = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    INT_PROP_1 = table.Column<int>(type: "int", nullable: true),
                    INT_PROP_2 = table.Column<int>(type: "int", nullable: true),
                    LONG_PROP_1 = table.Column<long>(type: "bigint", nullable: true),
                    LONG_PROP_2 = table.Column<long>(type: "bigint", nullable: true),
                    DEC_PROP_1 = table.Column<decimal>(type: "numeric(13,4)", nullable: true),
                    DEC_PROP_2 = table.Column<decimal>(type: "numeric(13,4)", nullable: true),
                    BOOL_PROP_1 = table.Column<bool>(type: "bit", nullable: true),
                    BOOL_PROP_2 = table.Column<bool>(type: "bit", nullable: true),
                    TIME_ZONE_ID = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QRTZ_SIMPROP_TRIGGERS", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                    table.ForeignKey(
                        name: "FK_QRTZ_SIMPROP_TRIGGERS_QRTZ_TRIGGERS_SCHED_NAME_TRIGGER_NAME_TRIGGER_GROUP",
                        columns: x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP },
                        principalSchema: "quartz",
                        principalTable: "QRTZ_TRIGGERS",
                        principalColumns: new[] { "SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EntryMeasureReport",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeasureReportId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReportType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MeasureReportUri = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    MeasureReportFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ReportEn__3214EC07C971CE5C", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportEntryMeasureReports_ReportEntries",
                        column: x => x.ReportEntryId,
                        principalTable: "ReportEntry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupPopulation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportPopulationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PopulationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PopulationCodeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalPopulationCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__GroupPop__3214EC0792FCF5E1", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupPopulations_ReportPopulations",
                        column: x => x.ReportPopulationId,
                        principalTable: "ReportPopulation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResourceCount",
                columns: table => new
                {
                    MeasureReportId = table.Column<int>(type: "int", nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResourceCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceCount", x => new { x.MeasureReportId, x.ResourceType });
                    table.ForeignKey(
                        name: "FK_ReportEntryMeasureReportResourceCounts_MeasureReport",
                        column: x => x.MeasureReportId,
                        principalTable: "EntryMeasureReport",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeasureReportPopulation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupPopulationId = table.Column<int>(type: "int", nullable: false),
                    MeasureReportId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PopulationCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__MeasureR__3214EC07051D5E54", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeasureReportPopulations_GroupPopulations",
                        column: x => x.GroupPopulationId,
                        principalTable: "GroupPopulation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntryMeasureReport_ReportEntryId",
                table: "EntryMeasureReport",
                column: "ReportEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPopulation_ReportPopulationId",
                table: "GroupPopulation",
                column: "ReportPopulationId");

            migrationBuilder.CreateIndex(
                name: "IX_MeasureReportPopulation_GroupPopulationId",
                table: "MeasureReportPopulation",
                column: "GroupPopulationId");

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_JOB_GROUP",
                schema: "quartz",
                table: "QRTZ_FIRED_TRIGGERS",
                column: "JOB_GROUP");

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_JOB_NAME",
                schema: "quartz",
                table: "QRTZ_FIRED_TRIGGERS",
                column: "JOB_NAME");

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_JOB_REQ_RECOVERY",
                schema: "quartz",
                table: "QRTZ_FIRED_TRIGGERS",
                column: "REQUESTS_RECOVERY");

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_TRIG_GROUP",
                schema: "quartz",
                table: "QRTZ_FIRED_TRIGGERS",
                column: "TRIGGER_GROUP");

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_TRIG_INST_NAME",
                schema: "quartz",
                table: "QRTZ_FIRED_TRIGGERS",
                column: "INSTANCE_NAME");

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_TRIG_NAME",
                schema: "quartz",
                table: "QRTZ_FIRED_TRIGGERS",
                column: "TRIGGER_NAME");

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_TRIG_NM_GP",
                schema: "quartz",
                table: "QRTZ_FIRED_TRIGGERS",
                columns: new[] { "SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_J_REQ_RECOVERY",
                schema: "quartz",
                table: "QRTZ_JOB_DETAILS",
                column: "REQUESTS_RECOVERY");

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_NEXT_FIRE_TIME",
                schema: "quartz",
                table: "QRTZ_TRIGGERS",
                column: "NEXT_FIRE_TIME");

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_NFT_ST",
                schema: "quartz",
                table: "QRTZ_TRIGGERS",
                columns: new[] { "NEXT_FIRE_TIME", "TRIGGER_STATE" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_STATE",
                schema: "quartz",
                table: "QRTZ_TRIGGERS",
                column: "TRIGGER_STATE");

            migrationBuilder.CreateIndex(
                name: "IX_QRTZ_TRIGGERS_SCHED_NAME_JOB_NAME_JOB_GROUP",
                schema: "quartz",
                table: "QRTZ_TRIGGERS",
                columns: new[] { "SCHED_NAME", "JOB_NAME", "JOB_GROUP" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportEntries_CreateDate",
                table: "ReportEntry",
                column: "CreateDate",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ReportEntries_Facility_Patient",
                table: "ReportEntry",
                columns: new[] { "FacilityId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportEntries_Facility_Schedule_Patient",
                table: "ReportEntry",
                columns: new[] { "FacilityId", "ReportScheduleId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportEntries_Reporting_Submission",
                table: "ReportEntry",
                columns: new[] { "ReportingStatus", "SubmissionStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportEntries_ReportScheduleId",
                table: "ReportEntry",
                column: "ReportScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportEntries_Schedule_Status",
                table: "ReportEntry",
                columns: new[] { "ReportScheduleId", "ReportingStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportPopulation_ReportScheduleId",
                table: "ReportPopulation",
                column: "ReportScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportPopulations_Facility_Schedule",
                table: "ReportPopulation",
                columns: new[] { "FacilityId", "ReportScheduleId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportPopulations_FacilityId",
                table: "ReportPopulation",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportResource_ReportScheduleId",
                table: "ReportResource",
                column: "ReportScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportResources_Facility_Patient",
                table: "ReportResource",
                columns: new[] { "FacilityId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportResources_Facility_Resource",
                table: "ReportResource",
                columns: new[] { "FacilityId", "ResourceType", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportResources_ResourceType",
                table: "ReportResource",
                column: "ResourceType");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_CreateDate",
                table: "ReportSchedule",
                column: "CreateDate",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_Facility_Period",
                table: "ReportSchedule",
                columns: new[] { "FacilityId", "ReportStartDate", "ReportEndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_FacilityId",
                table: "ReportSchedule",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_FacilityId_Id",
                table: "ReportSchedule",
                columns: new[] { "FacilityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_Status",
                table: "ReportSchedule",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeasureReportPopulation");

            migrationBuilder.DropTable(
                name: "QRTZ_BLOB_TRIGGERS",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "QRTZ_CALENDARS",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "QRTZ_CRON_TRIGGERS",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "QRTZ_FIRED_TRIGGERS",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "QRTZ_LOCKS",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "QRTZ_PAUSED_TRIGGER_GRPS",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "QRTZ_SCHEDULER_STATE",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "QRTZ_SIMPLE_TRIGGERS",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "QRTZ_SIMPROP_TRIGGERS",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "ReportResource");

            migrationBuilder.DropTable(
                name: "ResourceCount");

            migrationBuilder.DropTable(
                name: "ScheduleReportType");

            migrationBuilder.DropTable(
                name: "GroupPopulation");

            migrationBuilder.DropTable(
                name: "QRTZ_TRIGGERS",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "EntryMeasureReport");

            migrationBuilder.DropTable(
                name: "ReportPopulation");

            migrationBuilder.DropTable(
                name: "QRTZ_JOB_DETAILS",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "ReportEntry");

            migrationBuilder.DropTable(
                name: "ReportSchedule");
        }
    }
}
