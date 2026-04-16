if exists (select 1 from sys.foreign_keys where name = 'fk_category_rule_category_id')
    alter table category_rule drop constraint fk_category_rule_category_id;

if exists (select 1 from sys.foreign_keys where name = 'fk_result_category_category_id')
    alter table result_category drop constraint fk_result_category_category_id;

if exists (select 1 from sys.foreign_keys where name = 'fk_result_category_result_id')
    alter table result_category drop constraint fk_result_category_result_id;

drop table if exists artifact;

drop table if exists category;

drop table if exists category_rule;

drop table if exists result;

drop table if exists result_category;
