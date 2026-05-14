from __future__ import annotations

import os
import sqlite3
from datetime import datetime
from pathlib import Path
from typing import Any

import pymssql
from fastapi import FastAPI
from fastapi.encoders import jsonable_encoder
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel


APP_DIR = Path(__file__).resolve().parent
ROOT_DIR = APP_DIR.parent
FRONTEND_DIR = ROOT_DIR / "frontend"
APP_DATA_DIR = ROOT_DIR / "App_Data"
SQLITE_PATH = APP_DATA_DIR / "classview-meta.sqlite"
ENV_PATH = ROOT_DIR / ".env"


app = FastAPI(title="ClassView FastAPI")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)


class DbConnectionProfile(BaseModel):
    Id: int = 0
    Name: str = ".env"
    Server: str = ""
    DatabaseName: str = ""
    UserName: str = ""
    Password: str = ""
    IsDefault: bool = True
    HasPassword: bool = False
    KeepPassword: bool = False


class FavoriteToggleRequest(BaseModel):
    ItemType: str = ""
    ItemKey: str = ""
    Title: str = ""
    Subtitle: str = ""
    ExtraJson: str | None = None


class NoteSaveRequest(BaseModel):
    ItemType: str = ""
    ItemKey: str = ""
    Note: str = ""


class RecentRecordRequest(BaseModel):
    ItemType: str = ""
    ItemKey: str = ""
    Title: str = ""
    Subtitle: str = ""
    ExtraJson: str | None = None


class RecentClickRequest(BaseModel):
    ItemType: str = ""
    ItemKey: str = ""


def ok(data: Any = None, total: int | None = None) -> JSONResponse:
    payload: dict[str, Any] = {"success": True, "data": data}
    if total is not None:
        payload["total"] = total
    return JSONResponse(jsonable_encoder(payload))


def fail(message: str) -> JSONResponse:
    return JSONResponse({"success": False, "message": message})


def load_env() -> dict[str, str]:
    values: dict[str, str] = {}
    if not ENV_PATH.exists():
        return values

    for raw in ENV_PATH.read_text(encoding="utf-8-sig").splitlines():
        line = raw.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        values[key.strip()] = unquote(value.strip())
    return values


def save_env(values: dict[str, str]) -> None:
    lines = [f'{key}="{quote_env(value)}"' for key, value in values.items()]
    ENV_PATH.write_text("\n".join(lines) + "\n", encoding="utf-8")


def quote_env(value: str) -> str:
    return (value or "").replace("\\", "\\\\").replace('"', '\\"')


def unquote(value: str) -> str:
    if len(value) >= 2 and value[0] == '"' and value[-1] == '"':
        return value[1:-1].replace('\\"', '"').replace("\\\\", "\\")
    return value


def current_profile() -> DbConnectionProfile | None:
    values = load_env()
    server = values.get("SQL_SERVER", "")
    database = values.get("SQL_DATABASE", "")
    user = values.get("SQL_USER", "")
    password = values.get("SQL_PASSWORD", "")
    if not server and not database:
        return None
    return DbConnectionProfile(
        Server=server,
        DatabaseName=database,
        UserName=user,
        Password="",
        HasPassword=bool(password),
    )


def resolve_password(profile: DbConnectionProfile) -> str:
    if profile.KeepPassword or not profile.Password:
        return load_env().get("SQL_PASSWORD", "")
    return profile.Password


def parse_server(server: str) -> tuple[str, int | None]:
    value = (server or "").strip()
    if value.lower().startswith("tcp:"):
        value = value[4:]
    if "," in value:
        host, port = value.rsplit(",", 1)
        try:
            return host.strip(), int(port.strip())
        except ValueError:
            return value, None
    return value, None


def erp_connection(profile: DbConnectionProfile | None = None):
    profile = profile or current_profile()
    if profile is None:
        raise RuntimeError("未配置 ERP SQL Server 连接")

    host, port = parse_server(profile.Server)
    kwargs: dict[str, Any] = {
        "server": host,
        "user": profile.UserName,
        "password": resolve_password(profile),
        "database": profile.DatabaseName,
        "login_timeout": 10,
        "timeout": 60,
        "charset": "UTF-8",
    }
    if port is not None:
        kwargs["port"] = str(port)
    return pymssql.connect(**kwargs)


def erp_query(sql: str, params: tuple[Any, ...] = ()) -> list[dict[str, Any]]:
    with erp_connection() as conn:
        with conn.cursor(as_dict=True) as cursor:
            cursor.execute(sql, params)
            return [dict(row) for row in cursor.fetchall()]


def s(row: dict[str, Any], key: str) -> str:
    value = row.get(key)
    if value is None:
        return ""
    return str(value)


def parse_dt(value: Any) -> str | None:
    if value is None:
        return None
    if isinstance(value, datetime):
        return value.isoformat()
    return str(value)


def best_match(keyword: str, *values: str) -> tuple[int, int]:
    key = (keyword or "").strip()
    if not key:
        return 99, 2**31 - 1

    best_rank = 99
    best_length = 2**31 - 1
    low_key = key.lower()
    for value in values:
        text = (value or "").strip()
        if not text:
            continue
        low = text.lower()
        if low == low_key:
            rank = 0
        elif low.startswith(low_key):
            rank = 1
        elif low_key in low:
            rank = 2
        else:
            rank = 3
        if rank < best_rank or (rank == best_rank and len(text) < best_length):
            best_rank = rank
            best_length = len(text)
    return best_rank, best_length


def sqlite_conn() -> sqlite3.Connection:
    APP_DATA_DIR.mkdir(parents=True, exist_ok=True)
    conn = sqlite3.connect(SQLITE_PATH)
    conn.row_factory = sqlite3.Row
    ensure_schema(conn)
    return conn


def ensure_schema(conn: sqlite3.Connection) -> None:
    conn.executescript(
        """
        create table if not exists favorite_item (
            id integer primary key autoincrement,
            item_type text not null,
            item_key text not null,
            title text not null,
            subtitle text null,
            extra_json text null,
            created_at text not null default current_timestamp
        );
        create table if not exists recent_view (
            id integer primary key autoincrement,
            item_type text not null,
            item_key text not null,
            title text not null,
            subtitle text null,
            extra_json text null,
            viewed_at text not null default current_timestamp
        );
        create table if not exists click_stat (
            id integer primary key autoincrement,
            item_type text not null,
            item_key text not null,
            click_count integer not null default 0,
            last_clicked_at text not null default current_timestamp
        );
        create table if not exists item_note (
            id integer primary key autoincrement,
            item_type text not null,
            item_key text not null,
            note text null,
            created_at text not null default current_timestamp,
            updated_at text not null default current_timestamp
        );
        create unique index if not exists ux_favorite_item_type_key on favorite_item(item_type, item_key);
        create unique index if not exists ux_recent_view_type_key on recent_view(item_type, item_key);
        create unique index if not exists ux_click_stat_type_key on click_stat(item_type, item_key);
        create unique index if not exists ux_item_note_type_key on item_note(item_type, item_key);
        create index if not exists ix_recent_view_viewed_at on recent_view(viewed_at desc);
        """
    )
    conn.commit()


def favorited_keys(item_type: str) -> set[str]:
    with sqlite_conn() as conn:
        rows = conn.execute("select item_key from favorite_item where item_type=?", (item_type,)).fetchall()
        return {row["item_key"] for row in rows}


def click_stats(item_type: str) -> dict[str, dict[str, Any]]:
    with sqlite_conn() as conn:
        rows = conn.execute(
            "select item_key, click_count, last_clicked_at from click_stat where item_type=?",
            (item_type,),
        ).fetchall()
        return {row["item_key"]: dict(row) for row in rows}


def recent_views(item_type: str) -> dict[str, str | None]:
    with sqlite_conn() as conn:
        rows = conn.execute("select item_key, viewed_at from recent_view where item_type=?", (item_type,)).fetchall()
        return {row["item_key"]: row["viewed_at"] for row in rows}


def notes(item_type: str) -> dict[str, str]:
    with sqlite_conn() as conn:
        rows = conn.execute("select item_key, note from item_note where item_type=?", (item_type,)).fetchall()
        return {row["item_key"]: row["note"] or "" for row in rows}


@app.get("/api/health")
def health():
    return ok({"service": "backend-fastapi", "time": datetime.now().isoformat()})


@app.get("/api/config/current")
def config_current():
    return ok(current_profile())


@app.post("/api/config/test")
def config_test(profile: DbConnectionProfile):
    try:
        with erp_connection(profile):
            pass
        return JSONResponse({"success": True, "message": "连接成功"})
    except Exception as exc:
        return JSONResponse({"success": False, "message": str(exc)})


@app.post("/api/config/save")
def config_save(profile: DbConnectionProfile):
    values = load_env()
    values["SQL_SERVER"] = profile.Server
    values["SQL_DATABASE"] = profile.DatabaseName
    values["SQL_USER"] = profile.UserName
    values["SQL_PASSWORD"] = resolve_password(profile)
    save_env(values)
    return ok()


@app.get("/api/entity/search")
def entity_search(keyword: str = "", fuzzy: bool = True):
    try:
        key = (keyword or "").strip()
        where = ""
        params: tuple[Any, ...] = ()
        if key:
            if fuzzy:
                where = " and (a.Name like %s or b.DisplayName like %s) "
                params = (f"%{key}%", f"%{key}%")
            else:
                where = " and (a.Name=%s or b.DisplayName=%s) "
                params = (key, key)

        sql = """
        SELECT
            a.[FullName] as FullName,
            a.[Name] as Name,
            b.[DisplayName] as DisplayName,
            a.DefaultTableName as DefaultTableName,
            a.[ClassType] as ClassType,
            a.[ID] as ID,
            c.AssemblyName as AssemblyName
        FROM [UBF_MD_Class] as a
        left join UBF_MD_Class_trl as b on a.Local_ID=b.Local_ID
        left join UBF_MD_Component as c on a.MD_Component_ID=c.ID
        where ClassType in(1,2,3)
          and (b.sysmlflag='zh-CN' or b.sysmlflag is null)
        """ + where + " order by Name"

        rows = erp_query(sql, params)
        fav = favorited_keys("entity")
        clicks = click_stats("entity")
        recents = recent_views("entity")
        note_map = notes("entity")
        result = []
        for row in rows:
            item_key = s(row, "ID")
            click = clicks.get(item_key, {})
            match_rank, match_length = best_match(
                key,
                s(row, "DisplayName"),
                s(row, "Name"),
                s(row, "FullName"),
                s(row, "DefaultTableName"),
            )
            result.append(
                {
                    "FullName": s(row, "FullName"),
                    "Name": s(row, "Name"),
                    "DisplayName": s(row, "DisplayName"),
                    "DefaultTableName": s(row, "DefaultTableName"),
                    "ClassType": s(row, "ClassType"),
                    "ID": item_key,
                    "AssemblyName": s(row, "AssemblyName"),
                    "ItemType": "entity",
                    "ItemKey": item_key,
                    "IsFavorite": item_key in fav,
                    "Note": note_map.get(item_key, ""),
                    "ClickCount": int(click.get("click_count") or 0),
                    "LastClickedAt": click.get("last_clicked_at"),
                    "LastViewedAt": recents.get(item_key),
                    "MatchRank": match_rank,
                    "MatchLength": match_length,
                }
            )

        result.sort(
            key=lambda x: (
                x["MatchRank"],
                x["MatchLength"],
                not x["IsFavorite"],
                -x["ClickCount"],
                str(x["Name"]),
            )
        )
        return ok(result, len(result))
    except Exception as exc:
        return fail(str(exc))


@app.get("/api/entity/attributes")
def entity_attributes(id: str):
    try:
        sql = """
        SELECT
            a.[Name] as Name,
            a.DataTypeID as ID,
            b.FullName as FullName,
            a.DefaultValue as DefaultValue,
            a.IsCollection as IsCollection,
            c.DisplayName as DisplayName,
            c.[Description] as Description,
            b.ClassType as ClassType,
            a.IsKey as IsKey,
            a.IsNullable as IsNullable,
            a.IsReadOnly as IsReadOnly,
            a.IsSystem as IsSystem,
            a.IsBusinessKey as IsBusinessKey,
            a.GroupName as GroupName
        from UBF_MD_Attribute a
        inner join UBF_MD_CLASS b ON a.DataTypeID = b.ID
        left join UBF_MD_Attribute_trl as c on a.Local_ID=c.Local_ID
        where MD_Class_ID=%s
        order by a.IsSystem desc, a.GroupName asc, a.[Name] asc
        """
        rows = erp_query(sql, (id,))
        fav = favorited_keys("entity_attr")
        note_map = notes("entity_attr")
        result = []
        for row in rows:
            item_key = id + "|" + s(row, "Name")
            result.append(
                {
                    "Name": s(row, "Name"),
                    "ID": s(row, "ID"),
                    "FullName": s(row, "FullName"),
                    "DefaultValue": s(row, "DefaultValue"),
                    "IsCollection": s(row, "IsCollection"),
                    "DisplayName": s(row, "DisplayName"),
                    "Description": s(row, "Description"),
                    "ClassType": s(row, "ClassType"),
                    "IsKey": s(row, "IsKey"),
                    "IsNullable": s(row, "IsNullable"),
                    "IsReadOnly": s(row, "IsReadOnly"),
                    "IsSystem": s(row, "IsSystem"),
                    "IsBusinessKey": s(row, "IsBusinessKey"),
                    "GroupName": s(row, "GroupName"),
                    "ItemType": "entity_attr",
                    "ItemKey": item_key,
                    "IsFavorite": item_key in fav,
                    "Note": note_map.get(item_key, ""),
                }
            )
        return ok(result, len(result))
    except Exception as exc:
        return fail(str(exc))


@app.get("/api/favorite/list")
def favorite_list(itemType: str | None = None):
    with sqlite_conn() as conn:
        if itemType:
            rows = conn.execute(
                "select id, item_type, item_key, title, subtitle, extra_json, created_at from favorite_item where item_type=? order by created_at desc",
                (itemType,),
            ).fetchall()
        else:
            rows = conn.execute(
                "select id, item_type, item_key, title, subtitle, extra_json, created_at from favorite_item order by created_at desc"
            ).fetchall()
    result = [
        {
            "Id": row["id"],
            "ItemType": row["item_type"],
            "ItemKey": row["item_key"],
            "Title": row["title"],
            "Subtitle": row["subtitle"] or "",
            "ExtraJson": row["extra_json"],
            "CreatedAt": row["created_at"],
        }
        for row in rows
    ]
    return ok(result, len(result))


@app.post("/api/favorite/toggle")
def favorite_toggle(request: FavoriteToggleRequest):
    if not request.ItemType or not request.ItemKey:
        return fail("参数不能为空")
    with sqlite_conn() as conn:
        exists = conn.execute(
            "select id from favorite_item where item_type=? and item_key=? limit 1",
            (request.ItemType, request.ItemKey),
        ).fetchone()
        if exists:
            conn.execute(
                "delete from favorite_item where item_type=? and item_key=?",
                (request.ItemType, request.ItemKey),
            )
            conn.commit()
            return ok({"isFavorited": False})

        conn.execute(
            "insert into favorite_item(item_type, item_key, title, subtitle, extra_json, created_at) values(?,?,?,?,?,current_timestamp)",
            (request.ItemType, request.ItemKey, request.Title, request.Subtitle, request.ExtraJson),
        )
        conn.commit()
        return ok({"isFavorited": True})


@app.get("/api/note/list")
def note_list(itemType: str | None = None):
    with sqlite_conn() as conn:
        if itemType:
            rows = conn.execute(
                "select item_type, item_key, note, updated_at from item_note where item_type=? order by updated_at desc",
                (itemType,),
            ).fetchall()
        else:
            rows = conn.execute("select item_type, item_key, note, updated_at from item_note order by updated_at desc").fetchall()
    result = [
        {"ItemType": row["item_type"], "ItemKey": row["item_key"], "Note": row["note"] or "", "UpdatedAt": row["updated_at"]}
        for row in rows
    ]
    return ok(result, len(result))


@app.post("/api/note/save")
def note_save(request: NoteSaveRequest):
    if not request.ItemType or not request.ItemKey:
        return fail("参数不能为空")
    with sqlite_conn() as conn:
        conn.execute(
            """
            insert into item_note(item_type, item_key, note, created_at, updated_at)
            values(?,?,?,current_timestamp,current_timestamp)
            on conflict(item_type, item_key) do update set note=excluded.note, updated_at=current_timestamp
            """,
            (request.ItemType, request.ItemKey, request.Note),
        )
        conn.commit()
    return ok()


@app.get("/api/recent/list")
def recent_list(itemType: str | None = None, top: int = 100):
    limit = max(top, 1)
    with sqlite_conn() as conn:
        if itemType:
            rows = conn.execute(
                """
                select r.item_type, r.item_key, r.title, r.subtitle, r.extra_json, r.viewed_at,
                       coalesce(c.click_count, 0) as click_count, c.last_clicked_at
                from recent_view r
                left join click_stat c on r.item_type=c.item_type and r.item_key=c.item_key
                where r.item_type=?
                order by r.viewed_at desc limit ?
                """,
                (itemType, limit),
            ).fetchall()
        else:
            rows = conn.execute(
                """
                select r.item_type, r.item_key, r.title, r.subtitle, r.extra_json, r.viewed_at,
                       coalesce(c.click_count, 0) as click_count, c.last_clicked_at
                from recent_view r
                left join click_stat c on r.item_type=c.item_type and r.item_key=c.item_key
                order by r.viewed_at desc limit ?
                """,
                (limit,),
            ).fetchall()
    result = [
        {
            "ItemType": row["item_type"],
            "ItemKey": row["item_key"],
            "Title": row["title"],
            "Subtitle": row["subtitle"] or "",
            "ExtraJson": row["extra_json"],
            "ViewedAt": row["viewed_at"],
            "ClickCount": row["click_count"],
            "LastClickedAt": row["last_clicked_at"],
        }
        for row in rows
    ]
    return ok(result, len(result))


@app.post("/api/recent/record")
def recent_record(request: RecentRecordRequest):
    if not request.ItemType or not request.ItemKey:
        return fail("参数不能为空")
    with sqlite_conn() as conn:
        conn.execute(
            """
            insert into recent_view(item_type, item_key, title, subtitle, extra_json, viewed_at)
            values(?,?,?,?,?,current_timestamp)
            on conflict(item_type, item_key) do update set
              title=excluded.title,
              subtitle=excluded.subtitle,
              extra_json=excluded.extra_json,
              viewed_at=current_timestamp
            """,
            (request.ItemType, request.ItemKey, request.Title, request.Subtitle, request.ExtraJson),
        )
        conn.commit()
    return ok()


@app.post("/api/recent/click")
def recent_click(request: RecentClickRequest):
    if not request.ItemType or not request.ItemKey:
        return fail("参数不能为空")
    with sqlite_conn() as conn:
        conn.execute(
            """
            insert into click_stat(item_type, item_key, click_count, last_clicked_at)
            values(?,?,1,current_timestamp)
            on conflict(item_type, item_key) do update set
              click_count=click_count + 1,
              last_clicked_at=current_timestamp
            """,
            (request.ItemType, request.ItemKey),
        )
        conn.commit()
    return ok()


if FRONTEND_DIR.exists():
    app.mount("/frontend", StaticFiles(directory=FRONTEND_DIR, html=True), name="frontend-compat")
    app.mount("/", StaticFiles(directory=FRONTEND_DIR, html=True), name="frontend")
