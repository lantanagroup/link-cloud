using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Report.Migrations
{
    /// <inheritdoc />
    public partial class UniqueReportPopulationKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ReportStartDate and ReportEndDate are already datetimeoffset from
            // 20260330211922_DateTimeOffsetScheduleDates. The model snapshot had drifted
            // back to datetime2; correcting the snapshot must not convert those columns again.
            // quartz.QRTZ_TRIGGERS.MISFIRE_ORIG_FIRE_TIME is already on the model (August designer)
            // and no migration created it. Do not add it here.

            migrationBuilder.Sql(MergeDuplicatePopulationRowsSql);

            migrationBuilder.DropIndex(
                name: "IX_ReportPopulation_ReportScheduleId",
                table: "ReportPopulation");

            migrationBuilder.DropIndex(
                name: "IX_MeasureReportPopulation_GroupPopulationId",
                table: "MeasureReportPopulation");

            migrationBuilder.DropIndex(
                name: "IX_GroupPopulation_ReportPopulationId",
                table: "GroupPopulation");

            migrationBuilder.CreateIndex(
                name: "IX_ReportPopulation_Schedule_ReportType",
                table: "ReportPopulation",
                columns: new[] { "ReportScheduleId", "ReportType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeasureReportPopulation_Group_MeasureReport",
                table: "MeasureReportPopulation",
                columns: new[] { "GroupPopulationId", "MeasureReportId" },
                unique: true);

            // Unfiltered. A filtered index would still allow two null PopulationId rows on one parent.
            migrationBuilder.CreateIndex(
                name: "IX_GroupPopulation_Population_PopulationId",
                table: "GroupPopulation",
                columns: new[] { "ReportPopulationId", "PopulationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The data merge is not reversible. Down only restores the previous non-unique indexes.
            migrationBuilder.DropIndex(
                name: "IX_ReportPopulation_Schedule_ReportType",
                table: "ReportPopulation");

            migrationBuilder.DropIndex(
                name: "IX_MeasureReportPopulation_Group_MeasureReport",
                table: "MeasureReportPopulation");

            migrationBuilder.DropIndex(
                name: "IX_GroupPopulation_Population_PopulationId",
                table: "GroupPopulation");

            migrationBuilder.CreateIndex(
                name: "IX_ReportPopulation_ReportScheduleId",
                table: "ReportPopulation",
                column: "ReportScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_MeasureReportPopulation_GroupPopulationId",
                table: "MeasureReportPopulation",
                column: "GroupPopulationId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPopulation_ReportPopulationId",
                table: "GroupPopulation",
                column: "ReportPopulationId");
        }

        // Keep the earliest ReportPopulation (CreateDate, then Id) and re-parent its groups.
        // Keep the earliest GroupPopulation (Id) per parent and PopulationId, including nulls, and re-parent its children.
        // Keep the earliest MeasureReportPopulation (Id) when the same measure report was stored twice.
        // Recompute the total only for a group that moved, absorbed a collision, or lost a duplicate child.
        // Comparisons use the column collation so they match the unique indexes (a case-insensitive database treats "A" and "a" as the same key).
        private const string MergeDuplicatePopulationRowsSql = @"
SET NOCOUNT ON;

CREATE TABLE #PopulationLoser
(
    LoserId uniqueidentifier NOT NULL PRIMARY KEY,
    KeeperId uniqueidentifier NOT NULL
);

INSERT INTO #PopulationLoser (LoserId, KeeperId)
SELECT ranked.Id, ranked.KeeperId
FROM
(
    SELECT
        Id,
        FIRST_VALUE(Id) OVER (
            PARTITION BY ReportScheduleId, ReportType
            ORDER BY CreateDate ASC, Id ASC
        ) AS KeeperId
    FROM ReportPopulation
) AS ranked
WHERE ranked.Id <> ranked.KeeperId;

CREATE TABLE #AffectedGroup
(
    GroupId int NOT NULL PRIMARY KEY
);

INSERT INTO #AffectedGroup (GroupId)
SELECT g.Id
FROM GroupPopulation AS g
INNER JOIN #PopulationLoser AS l ON g.ReportPopulationId = l.LoserId;

UPDATE g
SET g.ReportPopulationId = l.KeeperId
FROM GroupPopulation AS g
INNER JOIN #PopulationLoser AS l ON g.ReportPopulationId = l.LoserId;

CREATE TABLE #GroupLoser
(
    LoserId int NOT NULL PRIMARY KEY,
    KeeperId int NOT NULL
);

INSERT INTO #GroupLoser (LoserId, KeeperId)
SELECT ranked.Id, ranked.KeeperId
FROM
(
    SELECT
        Id,
        FIRST_VALUE(Id) OVER (
            PARTITION BY ReportPopulationId, PopulationId
            ORDER BY Id ASC
        ) AS KeeperId
    FROM GroupPopulation
) AS ranked
WHERE ranked.Id <> ranked.KeeperId;

INSERT INTO #AffectedGroup (GroupId)
SELECT DISTINCT l.KeeperId
FROM #GroupLoser AS l
WHERE NOT EXISTS (SELECT 1 FROM #AffectedGroup AS a WHERE a.GroupId = l.KeeperId);

UPDATE c
SET c.GroupPopulationId = l.KeeperId
FROM MeasureReportPopulation AS c
INNER JOIN #GroupLoser AS l ON c.GroupPopulationId = l.LoserId;

CREATE TABLE #ChildLoser
(
    LoserId int NOT NULL PRIMARY KEY,
    GroupPopulationId int NOT NULL
);

INSERT INTO #ChildLoser (LoserId, GroupPopulationId)
SELECT ranked.Id, ranked.GroupPopulationId
FROM
(
    SELECT
        Id,
        GroupPopulationId,
        ROW_NUMBER() OVER (
            PARTITION BY GroupPopulationId, MeasureReportId
            ORDER BY Id ASC
        ) AS rn
    FROM MeasureReportPopulation
) AS ranked
WHERE ranked.rn > 1;

INSERT INTO #AffectedGroup (GroupId)
SELECT DISTINCT c.GroupPopulationId
FROM #ChildLoser AS c
WHERE NOT EXISTS (SELECT 1 FROM #AffectedGroup AS a WHERE a.GroupId = c.GroupPopulationId);

DELETE c
FROM MeasureReportPopulation AS c
INNER JOIN #ChildLoser AS l ON c.Id = l.LoserId;

DELETE g
FROM GroupPopulation AS g
INNER JOIN #GroupLoser AS l ON g.Id = l.LoserId;

DELETE p
FROM ReportPopulation AS p
INNER JOIN #PopulationLoser AS l ON p.Id = l.LoserId;

UPDATE g
SET g.TotalPopulationCount = ISNULL(sums.TotalCount, 0)
FROM GroupPopulation AS g
INNER JOIN #AffectedGroup AS a ON a.GroupId = g.Id
LEFT JOIN
(
    SELECT GroupPopulationId, SUM(PopulationCount) AS TotalCount
    FROM MeasureReportPopulation
    GROUP BY GroupPopulationId
) AS sums ON sums.GroupPopulationId = g.Id;
";
    }
}
