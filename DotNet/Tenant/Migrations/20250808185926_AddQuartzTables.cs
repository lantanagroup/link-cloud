using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddQuartzTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QRTZ_CALENDARS",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    CALENDAR_NAME = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false),
                    CALENDAR = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__QRTZ_CAL__DEBD34E0A2E90BF2", x => new { x.SCHED_NAME, x.CALENDAR_NAME });
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_FIRED_TRIGGERS",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    ENTRY_ID = table.Column<string>(type: "varchar(140)", unicode: false, maxLength: 140, nullable: false),
                    TRIGGER_NAME = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false),
                    TRIGGER_GROUP = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false),
                    INSTANCE_NAME = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false),
                    FIRED_TIME = table.Column<long>(type: "bigint", nullable: false),
                    SCHED_TIME = table.Column<long>(type: "bigint", nullable: false),
                    PRIORITY = table.Column<int>(type: "int", nullable: false),
                    STATE = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    JOB_NAME = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: true),
                    JOB_GROUP = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: true),
                    IS_NONCONCURRENT = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: true),
                    REQUESTS_RECOVERY = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__QRTZ_FIR__7793D06D16D46819", x => new { x.SCHED_NAME, x.ENTRY_ID });
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_JOB_DETAILS",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    JOB_NAME = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false),
                    JOB_GROUP = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false),
                    DESCRIPTION = table.Column<string>(type: "varchar(250)", unicode: false, maxLength: 250, nullable: true),
                    JOB_CLASS_NAME = table.Column<string>(type: "varchar(250)", unicode: false, maxLength: 250, nullable: false),
                    IS_DURABLE = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: false),
                    IS_NONCONCURRENT = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: false),
                    IS_UPDATE_DATA = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: false),
                    REQUESTS_RECOVERY = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: false),
                    JOB_DATA = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__QRTZ_JOB__E0CAAB8A9E94B203", x => new { x.SCHED_NAME, x.JOB_NAME, x.JOB_GROUP });
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_LOCKS",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    LOCK_NAME = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__QRTZ_LOC__7D2E9A03EE778BAD", x => new { x.SCHED_NAME, x.LOCK_NAME });
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_PAUSED_TRIGGER_GRPS",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    TRIGGER_GROUP = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__QRTZ_PAU__696155E957B72BF7", x => new { x.SCHED_NAME, x.TRIGGER_GROUP });
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_SCHEDULER_STATE",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    INSTANCE_NAME = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false),
                    LAST_CHECKIN_TIME = table.Column<long>(type: "bigint", nullable: false),
                    CHECKIN_INTERVAL = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__QRTZ_SCH__C8C3A19EA72285F1", x => new { x.SCHED_NAME, x.INSTANCE_NAME });
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_TRIGGERS",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    TRIGGER_NAME = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false),
                    TRIGGER_GROUP = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false),
                    JOB_NAME = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false),
                    JOB_GROUP = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false),
                    DESCRIPTION = table.Column<string>(type: "varchar(250)", unicode: false, maxLength: 250, nullable: true),
                    NEXT_FIRE_TIME = table.Column<long>(type: "bigint", nullable: true),
                    PREV_FIRE_TIME = table.Column<long>(type: "bigint", nullable: true),
                    PRIORITY = table.Column<int>(type: "int", nullable: true),
                    TRIGGER_STATE = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    TRIGGER_TYPE = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false),
                    START_TIME = table.Column<long>(type: "bigint", nullable: false),
                    END_TIME = table.Column<long>(type: "bigint", nullable: true),
                    CALENDAR_NAME = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: true),
                    MISFIRE_INSTR = table.Column<short>(type: "smallint", nullable: true),
                    JOB_DATA = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__QRTZ_TRI__20F4F101F92883D9", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                    table.ForeignKey(
                        name: "FK__QRTZ_TRIGGERS__4BAC3F29",
                        columns: x => new { x.SCHED_NAME, x.JOB_NAME, x.JOB_GROUP },
                        principalTable: "QRTZ_JOB_DETAILS",
                        principalColumns: new[] { "SCHED_NAME", "JOB_NAME", "JOB_GROUP" });
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_BLOB_TRIGGERS",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    TRIGGER_NAME = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false),
                    TRIGGER_GROUP = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false),
                    BLOB_DATA = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__QRTZ_BLO__20F4F101AC3CBB8A", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                    table.ForeignKey(
                        name: "FK__QRTZ_BLOB_TRIGGE__571DF1D5",
                        columns: x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP },
                        principalTable: "QRTZ_TRIGGERS",
                        principalColumns: new[] { "SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP" });
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_CRON_TRIGGERS",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    TRIGGER_NAME = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false),
                    TRIGGER_GROUP = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false),
                    CRON_EXPRESSION = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    TIME_ZONE_ID = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__QRTZ_CRO__20F4F101BAADD159", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                    table.ForeignKey(
                        name: "FK__QRTZ_CRON_TRIGGE__5165187F",
                        columns: x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP },
                        principalTable: "QRTZ_TRIGGERS",
                        principalColumns: new[] { "SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP" });
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_SIMPLE_TRIGGERS",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    TRIGGER_NAME = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false),
                    TRIGGER_GROUP = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false),
                    REPEAT_COUNT = table.Column<long>(type: "bigint", nullable: false),
                    REPEAT_INTERVAL = table.Column<long>(type: "bigint", nullable: false),
                    TIMES_TRIGGERED = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__QRTZ_SIM__20F4F101329F0516", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                    table.ForeignKey(
                        name: "FK__QRTZ_SIMPLE_TRIG__4E88ABD4",
                        columns: x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP },
                        principalTable: "QRTZ_TRIGGERS",
                        principalColumns: new[] { "SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP" });
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_SIMPROP_TRIGGERS",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    TRIGGER_NAME = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false),
                    TRIGGER_GROUP = table.Column<string>(type: "varchar(190)", unicode: false, maxLength: 190, nullable: false),
                    STR_PROP_1 = table.Column<string>(type: "varchar(512)", unicode: false, maxLength: 512, nullable: true),
                    STR_PROP_2 = table.Column<string>(type: "varchar(512)", unicode: false, maxLength: 512, nullable: true),
                    STR_PROP_3 = table.Column<string>(type: "varchar(512)", unicode: false, maxLength: 512, nullable: true),
                    INT_PROP_1 = table.Column<int>(type: "int", nullable: true),
                    INT_PROP_2 = table.Column<int>(type: "int", nullable: true),
                    LONG_PROP_1 = table.Column<long>(type: "bigint", nullable: true),
                    LONG_PROP_2 = table.Column<long>(type: "bigint", nullable: true),
                    DEC_PROP_1 = table.Column<decimal>(type: "numeric(13,4)", nullable: true),
                    DEC_PROP_2 = table.Column<decimal>(type: "numeric(13,4)", nullable: true),
                    BOOL_PROP_1 = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: true),
                    BOOL_PROP_2 = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__QRTZ_SIM__20F4F101A9A8C10F", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                    table.ForeignKey(
                        name: "FK__QRTZ_SIMPROP_TRI__5441852A",
                        columns: x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP },
                        principalTable: "QRTZ_TRIGGERS",
                        principalColumns: new[] { "SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP" });
                });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_INST_JOB_REQ_RCVRY",
                table: "QRTZ_FIRED_TRIGGERS",
                columns: new[] { "SCHED_NAME", "INSTANCE_NAME", "REQUESTS_RECOVERY" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_J_G",
                table: "QRTZ_FIRED_TRIGGERS",
                columns: new[] { "SCHED_NAME", "JOB_NAME", "JOB_GROUP" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_JG",
                table: "QRTZ_FIRED_TRIGGERS",
                columns: new[] { "SCHED_NAME", "JOB_GROUP" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_T_G",
                table: "QRTZ_FIRED_TRIGGERS",
                columns: new[] { "SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_TG",
                table: "QRTZ_FIRED_TRIGGERS",
                columns: new[] { "SCHED_NAME", "TRIGGER_GROUP" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_TRIG_INST_NAME",
                table: "QRTZ_FIRED_TRIGGERS",
                columns: new[] { "SCHED_NAME", "INSTANCE_NAME" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_J_GRP",
                table: "QRTZ_JOB_DETAILS",
                columns: new[] { "SCHED_NAME", "JOB_GROUP" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_J_REQ_RECOVERY",
                table: "QRTZ_JOB_DETAILS",
                columns: new[] { "SCHED_NAME", "REQUESTS_RECOVERY" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_C",
                table: "QRTZ_TRIGGERS",
                columns: new[] { "SCHED_NAME", "CALENDAR_NAME" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_G",
                table: "QRTZ_TRIGGERS",
                columns: new[] { "SCHED_NAME", "TRIGGER_GROUP" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_J",
                table: "QRTZ_TRIGGERS",
                columns: new[] { "SCHED_NAME", "JOB_NAME", "JOB_GROUP" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_JG",
                table: "QRTZ_TRIGGERS",
                columns: new[] { "SCHED_NAME", "JOB_GROUP" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_N_G_STATE",
                table: "QRTZ_TRIGGERS",
                columns: new[] { "SCHED_NAME", "TRIGGER_GROUP", "TRIGGER_STATE" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_N_STATE",
                table: "QRTZ_TRIGGERS",
                columns: new[] { "SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP", "TRIGGER_STATE" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_NEXT_FIRE_TIME",
                table: "QRTZ_TRIGGERS",
                columns: new[] { "SCHED_NAME", "NEXT_FIRE_TIME" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_NFT_MISFIRE",
                table: "QRTZ_TRIGGERS",
                columns: new[] { "SCHED_NAME", "MISFIRE_INSTR", "NEXT_FIRE_TIME" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_NFT_ST",
                table: "QRTZ_TRIGGERS",
                columns: new[] { "SCHED_NAME", "TRIGGER_STATE", "NEXT_FIRE_TIME" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_NFT_ST_MISFIRE",
                table: "QRTZ_TRIGGERS",
                columns: new[] { "SCHED_NAME", "MISFIRE_INSTR", "NEXT_FIRE_TIME", "TRIGGER_STATE" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_NFT_ST_MISFIRE_GRP",
                table: "QRTZ_TRIGGERS",
                columns: new[] { "SCHED_NAME", "MISFIRE_INSTR", "NEXT_FIRE_TIME", "TRIGGER_GROUP", "TRIGGER_STATE" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_STATE",
                table: "QRTZ_TRIGGERS",
                columns: new[] { "SCHED_NAME", "TRIGGER_STATE" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QRTZ_BLOB_TRIGGERS");

            migrationBuilder.DropTable(
                name: "QRTZ_CALENDARS");

            migrationBuilder.DropTable(
                name: "QRTZ_CRON_TRIGGERS");

            migrationBuilder.DropTable(
                name: "QRTZ_FIRED_TRIGGERS");

            migrationBuilder.DropTable(
                name: "QRTZ_LOCKS");

            migrationBuilder.DropTable(
                name: "QRTZ_PAUSED_TRIGGER_GRPS");

            migrationBuilder.DropTable(
                name: "QRTZ_SCHEDULER_STATE");

            migrationBuilder.DropTable(
                name: "QRTZ_SIMPLE_TRIGGERS");

            migrationBuilder.DropTable(
                name: "QRTZ_SIMPROP_TRIGGERS");

            migrationBuilder.DropTable(
                name: "QRTZ_TRIGGERS");

            migrationBuilder.DropTable(
                name: "QRTZ_JOB_DETAILS");
        }
    }
}
