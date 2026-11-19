-- Undo V20261109__Add_category_paging_index.sql; retain the result-first indexes.
IF OBJECT_ID('result_category', 'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'ix_result_category_category_id_result_id'
                 AND object_id = OBJECT_ID('result_category'))
BEGIN
    DROP INDEX ix_result_category_category_id_result_id ON result_category;
END;
