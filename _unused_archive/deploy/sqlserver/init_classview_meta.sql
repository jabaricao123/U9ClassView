-- ClassView metadata schema for SqlList SQL Server database.
-- This script only creates schema objects.
-- No default business data will be inserted.

if object_id(N'dbo.db_connection_profile', N'U') is null
begin
    create table dbo.db_connection_profile (
        id bigint identity(1,1) not null primary key,
        name nvarchar(200) not null,
        server nvarchar(200) not null,
        database_name nvarchar(200) not null,
        username nvarchar(100) not null,
        password nvarchar(500) not null,
        is_default bit not null constraint df_db_connection_profile_is_default default 0,
        connection_status nvarchar(50) null,
        last_tested_at datetime2 null,
        created_at datetime2 not null constraint df_db_connection_profile_created_at default sysdatetime(),
        updated_at datetime2 not null constraint df_db_connection_profile_updated_at default sysdatetime()
    );
end
go

if object_id(N'dbo.favorite_item', N'U') is null
begin
    create table dbo.favorite_item (
        id bigint identity(1,1) not null primary key,
        item_type nvarchar(50) not null,
        item_key nvarchar(500) not null,
        title nvarchar(500) not null,
        subtitle nvarchar(500) null,
        extra_json nvarchar(max) null,
        created_at datetime2 not null constraint df_favorite_item_created_at default sysdatetime()
    );
end
go

if object_id(N'dbo.recent_view', N'U') is null
begin
    create table dbo.recent_view (
        id bigint identity(1,1) not null primary key,
        item_type nvarchar(50) not null,
        item_key nvarchar(500) not null,
        title nvarchar(500) not null,
        subtitle nvarchar(500) null,
        extra_json nvarchar(max) null,
        viewed_at datetime2 not null constraint df_recent_view_viewed_at default sysdatetime()
    );
end
go

if object_id(N'dbo.click_stat', N'U') is null
begin
    create table dbo.click_stat (
        id bigint identity(1,1) not null primary key,
        item_type nvarchar(50) not null,
        item_key nvarchar(500) not null,
        click_count int not null constraint df_click_stat_click_count default 0,
        last_clicked_at datetime2 not null constraint df_click_stat_last_clicked_at default sysdatetime()
    );
end
go

if object_id(N'dbo.item_note', N'U') is null
begin
    create table dbo.item_note (
        id bigint identity(1,1) not null primary key,
        item_type nvarchar(50) not null,
        item_key nvarchar(500) not null,
        note nvarchar(max) null,
        created_at datetime2 not null constraint df_item_note_created_at default sysdatetime(),
        updated_at datetime2 not null constraint df_item_note_updated_at default sysdatetime()
    );
end
go

if not exists (select 1 from sys.indexes where name = N'ux_favorite_item_type_key' and object_id = object_id(N'dbo.favorite_item'))
begin
    create unique index ux_favorite_item_type_key on dbo.favorite_item(item_type, item_key);
end
go

if not exists (select 1 from sys.indexes where name = N'ux_recent_view_type_key' and object_id = object_id(N'dbo.recent_view'))
begin
    create unique index ux_recent_view_type_key on dbo.recent_view(item_type, item_key);
end
go

if not exists (select 1 from sys.indexes where name = N'ux_click_stat_type_key' and object_id = object_id(N'dbo.click_stat'))
begin
    create unique index ux_click_stat_type_key on dbo.click_stat(item_type, item_key);
end
go

if not exists (select 1 from sys.indexes where name = N'ux_item_note_type_key' and object_id = object_id(N'dbo.item_note'))
begin
    create unique index ux_item_note_type_key on dbo.item_note(item_type, item_key);
end
go

if not exists (select 1 from sys.indexes where name = N'ix_recent_view_viewed_at' and object_id = object_id(N'dbo.recent_view'))
begin
    create index ix_recent_view_viewed_at on dbo.recent_view(viewed_at desc);
end
go

if not exists (select 1 from sys.indexes where name = N'ix_click_stat_rank' and object_id = object_id(N'dbo.click_stat'))
begin
    create index ix_click_stat_rank on dbo.click_stat(item_type, click_count desc, last_clicked_at desc);
end
go
