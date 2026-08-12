if not exists (select 1 from sys.columns where object_id = object_id('category') and name = 'submit')
begin
    alter table category add submit bit not null constraint df_category_submit default (1);
end;

if not exists (select 1 from sys.columns where object_id = object_id('category') and name = 'review')
begin
    alter table category add review bit not null constraint df_category_review default (1);
end;
