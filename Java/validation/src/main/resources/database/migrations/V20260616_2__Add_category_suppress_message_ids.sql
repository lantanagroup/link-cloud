-- Adds the suppress_message_ids column on category for Phase 4 SUPPRESS support.
-- See VALIDATION-CATEGORIES-DESIGN.md.
--
-- suppress_message_ids: JSON array of stable HAPI I18nConstants message ID strings.
--                       Nullable; only populated for rules that drop specific messages
--                       via CategoryBackedPolicyAdvisor.isSuppressMessageId(...). A rule
--                       can carry both this column AND scope at the same time — they
--                       wire two independent advisor hooks.

if not exists (select 1 from sys.columns where name = 'suppress_message_ids'
                 and object_id = object_id('category'))
begin
    alter table category add suppress_message_ids varchar(max) null;
end;
