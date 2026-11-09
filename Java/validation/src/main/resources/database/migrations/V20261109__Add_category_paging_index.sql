-- Allow category page and COUNT queries to seek category membership before joining report findings.
-- Keep the result-first indexes for patient replacement, uncategorized lookup and CSV export.
IF OBJECT_ID('result_category', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = 'ix_result_category_category_id_result_id'
                     AND object_id = OBJECT_ID('result_category'))
BEGIN
    CREATE INDEX ix_result_category_category_id_result_id ON result_category (category_id, result_id);
END;

-- Rollback: DROP INDEX IF EXISTS ix_result_category_category_id_result_id ON result_category;
