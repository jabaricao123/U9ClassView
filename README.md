# ClassView Jabaricao

ClassView 是一个 ERP/U9 元数据查询工具。\
前端使用重构版 `frontend/index.html`，后端重构为fastapi

- 脱离 IIS 架构，舍弃传统部署依赖
- 
## 主要功能

- 实体查询：按名称、命名空间、表名检索实体
- Form 查询：按页面名、URI、类名检索 Form
- View 查询：按 View 名称、Form、类名检索
- 参照查询：按参照名称、引用实体检索
- BP 查询：按 BP 显示名/全名检索
- 实体详情：查看字段、类型、默认值、分组等
- 类型跳转：字段类型以 `UFIDA` 开头时可点击跳转到对应实体
- 字段置顶：勾选后即时置顶，并按实体持久化
- 备注：支持行内备注保存
- 收藏与最近浏览：支持收藏、最近访问记录
- SQL 生成：在实体详情页快速生成查询 SQL

## 下一版本更新规划

- 完善 SQL 测试功能，已置顶字段自动生成 SQL，摒弃原有冗余沉淀字段，提升 AI Agent 识别效率
- 测试 SQL 语句自动加注释功能，导出的 SQL 可直接交给 AI，一键生成接口 API 和 MCP 服务
- 完善字段备注、数据表备注体系，助力 AI 快速解析业务、自动生成标准 SQL

## 存储说明

- ERP 业务数据：读取 SQL Server（通过 `.env` 配置）
- 本地工具数据：使用 SQLite（自动创建在 `App_Data/classview-meta.sqlite`）
  - 收藏
  - 备注
  - 最近浏览
  - 点击统计

## 快速开始

1. 复制环境变量模板：
   - 将 `.env.example` 复制为 `.env`
2. 配置 ERP SQL Server 连接：
   - `SQL_SERVER`
   - `SQL_DATABASE`
   - `SQL_USER`
   - `SQL_PASSWORD`
3. 启动 FastAPI 后端：

```powershell
cd backend-fastapi
python -m pip install -r requirements.txt
python -m uvicorn main:app --host 127.0.0.1 --port 8000
```

4. 打开前端页面：
   - `http://127.0.0.1:8000/`


## FastAPI 后端

新后端位于 `backend-fastapi/`，使用 FastAPI + `pymssql` 读取 ERP SQL Server，使用 SQLite 保存本地工具数据。

已迁移接口：

- `GET /api/entity/search`
- `GET /api/entity/attributes`
- `GET /api/favorite/list`
- `POST /api/favorite/toggle`
- `GET /api/note/list`
- `POST /api/note/save`
- `GET /api/recent/list`
- `POST /api/recent/record`
- `POST /api/recent/click`
- `GET /api/config/current`
- `POST /api/config/test`
- `POST /api/config/save`

启动方式：

```powershell
cd backend-fastapi
python -m pip install -r requirements.txt
python -m uvicorn main:app --host 127.0.0.1 --port 8000
```

默认地址：

```text
http://127.0.0.1:8000/
```

新后端会直接托管现有 `frontend/`，因此访问 `http://127.0.0.1:8000/` 即可进入重构版页面。

## 使用说明

1. 进入左侧任一查询页（实体/Form/View/参照/BP）
2. 输入关键词并搜索
3. 点击结果进入详情页
4. 在详情页可：
   - 勾选字段置顶
   - 编辑备注
   - 收藏实体
   - 生成 SQL
5. 在“最近浏览”中可回看最近访问项

## 目录结构

- `frontend/`：重构版前端
- `backend-fastapi/`：FastAPI 后端
- `Api/`：Web API 控制器
- `App_Code/`：运行时编译代码（与 `Api/` 同步）
- `App_Data/`：本地 SQLite 与日志
- `Bin/`：运行依赖（含 `System.Data.SQLite`）

## 注意事项

- `.env` 包含敏感信息，默认已在 `.gitignore` 忽略
- 上传 GitHub 前请确认：
  - 不提交真实账号密码
  - 不提交 `App_Data/*.sqlite` 本地数据文件
