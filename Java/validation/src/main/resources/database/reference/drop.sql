alter table category_rule drop constraint if exists fk_category_rule_category_id;
alter table result_category drop constraint if exists fk_result_category_category_id;
alter table result_category drop constraint if exists fk_result_category_result_id;
drop table if exists artifact;
drop table if exists category;
drop table if exists category_rule;
drop table if exists result;
drop table if exists result_category;
