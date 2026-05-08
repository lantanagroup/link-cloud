alter table category_rule
    drop constraint fk_category_rule_category_id;

alter table result_category
    drop constraint fk_result_category_category_id;

alter table result_category
    drop constraint fk_result_category_result_id;

drop table artifact;

drop table category;

drop table category_rule;

drop table result;

drop table result_category;
