-- Adds suppress_path_patterns column on category for Phase 4 SUPPRESS path-narrowing.
-- See VALIDATION-CATEGORIES-DESIGN.md.
--
-- suppress_path_patterns: JSON array of regex patterns matched against the path argument of
--                         HAPI's isSuppressMessageId(path, messageId) hook. When non-empty, the
--                         rule's suppress_message_ids only fire on paths matching at least one
--                         pattern. When null/empty, the rule fires on any path (the default
--                         Phase 4 behaviour that unknown_code_system relies on).

if not exists (select 1 from sys.columns where name = 'suppress_path_patterns'
                 and object_id = object_id('category'))
begin
    alter table category add suppress_path_patterns varchar(max) null;
end;
