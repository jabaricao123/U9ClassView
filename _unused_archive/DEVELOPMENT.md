# ClassView 开发文档

## 一、项目概述

ClassView 是面向 U9/ERP 元数据的查询工具，提供实体、Form、View、参照、BP、OQL 等多维元数据检索能力，并支持收藏、最近浏览、点击统计、备注等个人化功能。

技术栈：

| 层 | 技术 |
|---|---|
| 后端 | ASP.NET Web Site (.NET Framework 4.8) + Web API 2 |
| 前端 | 原生 HTML/CSS/JS（无框架，Hash 路由） |
| ERP 数据库 | SQL Server（只读） |
| 元数据库 | SqlList 独立 SQL Server（读写） |
| 核心依赖 | U9.Subsidiary.Lib / UBF 框架 DLL 集 / Newtonsoft.Json |

## 二、目录结构

```
D:\classview
├─ App_Code\                        Web Site 运行时编译代码
│  ├─ Api\Controllers\              ★ 运行中的 Web API 控制器（主编辑位置）
│  │  ├─ BaseApiController.cs       基类：连接管理、查询执行、收藏/点击/最近/备注读取
│  │  ├─ EntityController.cs        实体搜索 & 字段查询
│  │  ├─ FormController.cs          Form 搜索
│  │  ├─ ViewController.cs          View 搜索 & View 字段查询
│  │  ├─ ReferenceController.cs     参照搜索
│  │  ├─ BPController.cs            BP 搜索
│  │  ├─ OqlController.cs           OQL 解析 & SQL 执行
│  │  ├─ ConfigController.cs        ERP 连接配置 读取/测试/保存
│  │  ├─ FavoriteController.cs      收藏列表 & 切换
│  │  ├─ RecentController.cs        最近浏览记录 & 点击计数
│  │  ├─ NoteController.cs          备注 读取/保存
│  │  └─ HealthController.cs        健康检查
│  ├─ CustomWebGlobal.cs            Application 入口，注册 Web API 路由/过滤器/JSON
│  ├─ ApiLogFilter.cs               API 请求日志过滤器（计时 + 状态码）
│  └─ FileLogger.cs                 文件日志工具（敏感信息掩码）
│
├─ App_Start\
│  └─ WebApiConfig.cs               Web API 配置（CORS / 路由 / JSON），当前主逻辑在 CustomWebGlobal
│
├─ Api\Controllers\                 历史/备份控制器目录（⚠️ 非主编辑位置，易造成误改）
│
├─ frontend\                        新前端（原生 JS，4 个文件）
│  ├─ index.html                    入口 HTML：顶栏 / 收藏栏 / 标签栏 / 侧边栏 / 主内容
│  ├─ core.js                       核心：API client / State / Router / Tabs / Bookmarks / 工具函数
│  ├─ pages.js                      页面渲染：搜索页 / 详情页 / OQL / 配置 / 最近浏览
│  └─ style.css                     样式：深色/浅色主题 CSS 变量 + 全部组件样式
│
├─ deploy\
│  ├─ iis\deploy-classview.ps1      IIS 一键部署脚本
│  └─ sqlserver\init_classview_meta.sql  SqlList 元数据库建表脚本
│
├─ App_Data\Logs\                   运行日志目录
├─ Bin\                             .NET 依赖程序集（UBF / Newtonsoft.Json / WebApi 等）
├─ App_Themes\                      WebForms 主题
├─ Detial\                          旧项目资源
├─ *.aspx / *.aspx.cs               旧 WebForms 页面（回退入口，暂保留）
├─ Global.asax                      → Inherits="CustomWebGlobal"
├─ Web.Config                       IIS / ASP.NET / 数据库连接配置
├─ packages.config                  NuGet 依赖定义
└─ packages\                        NuGet 包目录
```

## 三、应用启动流程

```
Global.asax  →  CustomWebGlobal  →  U9.Subsidiary.Lib.WebGlobal.Application_Start
                              └→  EnsureApiConfigured()
                                  ├─ config.MapHttpAttributeRoutes()
                                  ├─ config.Filters.Add(ApiLogFilter)
                                  ├─ config.Routes.MapHttpRoute("api/{controller}/{action}/{id}")
                                  ├─ 移除 XML Formatter
                                  └─ JSON ReferenceLoopHandling.Ignore
```

关键点：
- `CustomWebGlobal` 继承 `U9.Subsidiary.Lib.WebGlobal`，先执行基类启动
- API 配置使用双重检查锁（`_apiConfigured` + `_apiConfigLock`），确保只初始化一次
- 路由模板：`api/{controller}/{action}/{id}`

## 四、后端架构

### 4.1 分层

```
Controller 层  →  BaseApiController  →  ERP SQL Server（只读查询）
                                    →  SqlList SQL Server（元数据读写）
                                    →  FileLogger
```

### 4.2 BaseApiController 核心能力

| 方法 | 用途 |
|---|---|
| `ErpConnectionString` | 获取 ERP 连接串，优先级：SqlList 默认配置 → Web.Config → Session/Application |
| `MetaConnectionString` | 获取 SqlList 连接串，来源 Web.Config `MetaConnection` |
| `ExecuteErpQuery(sql)` | 执行 ERP 只读查询，返回 DataTable |
| `ExecuteMetaQuery(sql, params)` | 执行 SqlList 查询，返回 DataTable |
| `ExecuteMetaScalar(sql, params)` | 执行 SqlList 标量查询 |
| `ExecuteMetaNonQuery(sql, params)` | 执行 SqlList 写操作 |
| `TestErpConnection(connStr)` | 测试 ERP 连接是否可达 |
| `GetFavoritedKeys(itemType)` | 获取某类型所有已收藏 key |
| `GetClickStats(itemType)` | 获取某类型点击统计 |
| `GetRecentViews(itemType)` | 获取某类型最近浏览时间 |
| `GetNotes(itemType)` | 获取某类型备注 |
| `RecordRecentView(...)` | Upsert 最近浏览记录 |
| `IncrementClick(itemType, key)` | Upsert 点击计数 +1 |
| `GetMatchRank(keyword, values...)` | 关键词匹配排序：精确=0, 前缀=1, 包含=2, 不含=3 |

### 4.3 ERP 连接优先级

```
1. SqlList db_connection_profile 中 is_default=1 的配置
2. HttpContext.Current.Session["Connection"] (ConnectionModel)
3. HttpContext.Current.Application["Connection"] (ConnectionModel)
4. Web.Config ConnectionString
```

### 4.4 排序规则

搜索结果统一排序：
1. `MatchRank` 升序（精确匹配优先）
2. `MatchLength` 升序（短文本优先）
3. `IsFavorite` 降序（收藏优先）
4. `ClickCount` 降序
5. `LastViewedAt` 降序
6. 名称升序

## 五、API 接口清单

### 5.1 健康检查

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/health/check` | 检测 ERP 和 SqlList 连接状态 |

返回：`{ success, data: { erp: bool, meta: bool, serverTime } }`

### 5.2 数据库配置

| 方法 | 路径 | 请求体 | 说明 |
|---|---|---|---|
| GET | `/api/config/current` | — | 获取当前默认连接配置 |
| POST | `/api/config/test` | `DbConnectionProfile` | 测试 ERP 连接 |
| POST | `/api/config/save` | `DbConnectionProfile` | 保存连接配置到 SqlList |

`DbConnectionProfile`：`{ Id, Name, Server, DatabaseName, UserName, Password, IsDefault }`

### 5.3 实体

| 方法 | 路径 | 参数 | 说明 |
|---|---|---|---|
| GET | `/api/entity/search` | `keyword`, `fuzzy` | 搜索实体，返回带收藏/点击/最近/备注的结果 |
| GET | `/api/entity/attributes` | `id` | 查询实体字段，返回带收藏/备注的字段列表 |

搜索结果字段：`FullName, Name, DisplayName, DefaultTableName, ClassType, ID, AssemblyName, ItemType, ItemKey, IsFavorite, Note, ClickCount, LastClickedAt, LastViewedAt, MatchRank, MatchLength`

字段结果字段：`Name, ID, FullName, DefaultValue, IsCollection, DisplayName, Description, ClassType, IsKey, IsNullable, IsReadOnly, IsSystem, IsBusinessKey, GroupName, ItemType(entity_attr), ItemKey, IsFavorite, Note`

### 5.4 Form

| 方法 | 路径 | 参数 | 说明 |
|---|---|---|---|
| GET | `/api/form/search` | `keyword`, `fuzzy` | 搜索 Form |

结果字段：`AssemblyName, ClassName, FormID, Name, Url, Application, MenuName, ItemType, ItemKey, IsFavorite, Note, ClickCount, LastClickedAt, LastViewedAt, MatchRank, MatchLength`

### 5.5 View

| 方法 | 路径 | 参数 | 说明 |
|---|---|---|---|
| GET | `/api/view/search` | `keyword`, `fuzzy` | 搜索 View |
| GET | `/api/view/fields` | `className`, `viewName` | 查询 View 字段 |

搜索结果字段：`ViewName, ViewDisplayName, UIForm, FormDisplayName, ClassName, AssemblyName, UIModel, FilterOriginalOPath, Uri, Width, Height, ItemType, ItemKey, IsFavorite, Note, ClickCount, LastClickedAt, LastViewedAt, MatchRank, MatchLength`

字段结果字段：`Name, ToolTips, DataType, DefaultValue, GroupName, ClassName, ViewName, ItemType(view_field), ItemKey, IsFavorite, Note`

### 5.6 参照

| 方法 | 路径 | 参数 | 说明 |
|---|---|---|---|
| GET | `/api/reference/search` | `keyword`, `fuzzy` | 搜索参照 |

结果字段：`Assembly, FormId, FormName, Filter, ClassName, Url, DisplayName, RefEntityName, ItemType, ItemKey, IsFavorite, Note, ClickCount, LastClickedAt, LastViewedAt, MatchRank, MatchLength`

### 5.7 BP

| 方法 | 路径 | 参数 | 说明 |
|---|---|---|---|
| GET | `/api/bp/search` | `keyword`, `fuzzy` | 搜索 BP |

结果字段：`DisplayName, FullName, AssemblyName, Kind, ComponentDisplayName, ItemType, ItemKey, IsFavorite, Note, ClickCount, LastClickedAt, LastViewedAt, MatchRank, MatchLength`

### 5.8 OQL

| 方法 | 路径 | 请求体 | 说明 |
|---|---|---|---|
| POST | `/api/oql/parse` | `{ Oql }` | OQL 解析为 SQL（使用 UBF EntityViewQuery） |
| POST | `/api/oql/execute` | `{ Sql }` | 执行 SQL 返回动态列结果 |

返回：`{ success, data: [...], total, columns: [...] }`

### 5.9 收藏

| 方法 | 路径 | 参数/请求体 | 说明 |
|---|---|---|---|
| GET | `/api/favorite/list` | `itemType`(可选) | 收藏列表 |
| POST | `/api/favorite/toggle` | `FavoriteToggleRequest` | 切换收藏（有则删，无则加） |

`FavoriteToggleRequest`：`{ ItemType, ItemKey, Title, Subtitle, ExtraJson }`

### 5.10 最近浏览 & 点击

| 方法 | 路径 | 参数/请求体 | 说明 |
|---|---|---|---|
| GET | `/api/recent/list` | `itemType`(可选), `top`(默认100) | 最近浏览列表（关连 click_stat） |
| POST | `/api/recent/record` | `RecentRecordRequest` | 记录最近浏览（upsert） |
| POST | `/api/recent/click` | `RecentClickRequest` | 点击计数 +1（upsert） |

`RecentRecordRequest`：`{ ItemType, ItemKey, Title, Subtitle, ExtraJson }`
`RecentClickRequest`：`{ ItemType, ItemKey }`

### 5.11 备注

| 方法 | 路径 | 参数/请求体 | 说明 |
|---|---|---|---|
| GET | `/api/note/list` | `itemType`(可选) | 备注列表 |
| POST | `/api/note/save` | `NoteSaveRequest` | 保存备注（upsert） |

`NoteSaveRequest`：`{ ItemType, ItemKey, Note }`

### 5.12 统一响应格式

```json
{
  "success": true,
  "data": ...,
  "total": 123
}
```

失败时：
```json
{
  "success": false,
  "message": "错误信息"
}
```

## 六、数据库架构

### 6.1 ERP SQL Server（只读）

主要查询的 ERP 表：

| 表名 | 用途 |
|---|---|
| `UBF_MD_Class` | 实体/类元数据 |
| `UBF_MD_Class_Trl` | 类多语言信息（DisplayName） |
| `UBF_MD_Attribute` | 实体字段 |
| `UBF_MD_Attribute_Trl` | 字段多语言信息 |
| `UBF_MD_Component` | 组件（含 AssemblyName, Kind） |
| `UBF_MD_Component_Trl` | 组件多语言信息 |
| `UBF_MD_UIForm` | UI Form |
| `UBF_MD_UIForm_Trl` | Form 多语言 |
| `UBF_MD_UIView` | UI View |
| `UBF_MD_UIView_Trl` | View 多语言 |
| `UBF_MD_UIModel` | UI Model |
| `UBF_MD_UIField` | UI 字段 |
| `UBF_MD_UIReference` | 参照定义 |
| `UBF_MD_UIRComponent` | 参照组件 |
| `UBF_MD_UIRComponent_Trl` | 参照组件多语言 |
| `UBF_Assemble_Part` | 装配部件 |
| `UBF_Assemble_ColumnPart` | 列部件 |
| `UBF_Assemble_PageColumn` | 页面列 |
| `UBF_Assemble_Page` | 页面 |
| `UBF_Assemble_Menu` | 菜单 |
| `aspnet_Parts` | Parts（参照关联） |

约束：**只读查询，不建表、不写入、不改结构**

### 6.2 SqlList 元数据库（读写）

初始化脚本：`deploy/sqlserver/init_classview_meta.sql`

| 表名 | 用途 | 关键字段 |
|---|---|---|
| `db_connection_profile` | ERP 连接配置 | `id, name, server, database_name, username, password, is_default` |
| `favorite_item` | 收藏项 | `item_type, item_key, title, subtitle, extra_json` |
| `recent_view` | 最近浏览 | `item_type, item_key, title, subtitle, extra_json, viewed_at` |
| `click_stat` | 点击统计 | `item_type, item_key, click_count, last_clicked_at` |
| `item_note` | 备注 | `item_type, item_key, note` |

唯一索引：
- `ux_favorite_item_type_key` → `(item_type, item_key)`
- `ux_recent_view_type_key` → `(item_type, item_key)`
- `ux_click_stat_type_key` → `(item_type, item_key)`
- `ux_item_note_type_key` → `(item_type, item_key)`

排序索引：
- `ix_recent_view_viewed_at` → `(viewed_at desc)`
- `ix_click_stat_rank` → `(item_type, click_count desc, last_clicked_at desc)`

## 七、前端架构

### 7.1 文件结构

| 文件 | 职责 |
|---|---|
| `index.html` | 页面骨架：顶栏、收藏栏、标签栏、侧边栏、主内容区、Toast |
| `core.js` | API 客户端、全局状态、Hash 路由、标签页管理、收藏栏、连接状态、备注保存、工具函数 |
| `pages.js` | 各页面渲染逻辑：搜索页、实体详情、View 详情、字典详情、OQL、最近浏览、数据库设置 |
| `style.css` | 深色/浅色主题 CSS 变量 + 全部组件样式 |

### 7.2 API 客户端

```javascript
API_BASE 解析优先级：
1. window.CLASSVIEW_API_BASE
2. ?apiBase= 查询参数
3. localStorage('cv.apiBase')
4. 自动推断：file:// 协议 → http://localhost/api，否则 → /api

api.get(url)   → fetch(buildApiUrl(url)).json()
api.post(url, body) → fetch(buildApiUrl(url), { method:'POST', JSON body }).json()
```

### 7.3 状态管理

```javascript
state = {
  tabs: [],          // sessionStorage('cv.tabs')
  activeTab: '',     // sessionStorage('cv.activeTab')
  favorites: [],     // 从 /api/favorite/list 加载
  connInfo: null,    // 从 /api/config/current 加载
  sidebarCollapsed: false,
  theme: localStorage('cv.theme')  // 'dark' | 'light'
}

详情缓存：sessionStorage('cv.detail.{itemType}:{itemKey}')
搜索缓存：sessionStorage('cv.kw.{itemType}'), sessionStorage('cv.fz.{itemType}')
```

### 7.4 路由

使用 Hash 路由（`#/route`）：

| 路由 | 页面 | 说明 |
|---|---|---|
| `entity` | 搜索页 | 实体查询 |
| `form` | 搜索页 | Form 查询 |
| `view` | 搜索页 | View 查询 |
| `reference` | 搜索页 | 参照查询 |
| `bp` | 搜索页 | BP 查询 |
| `oql` | OQL 工具 | OQL 输入/解析/执行 |
| `recent` | 最近浏览 | 按类型筛选 |
| `db-config` | 数据库设置 | 连接配置/测试/保存 |
| `detail/entity/{id}` | 实体详情 | 字段表 + 收藏 + 置顶 + SQL 生成 |
| `detail/view?className=&viewName=&key=` | View 详情 | View 字段表 |
| `detail/item/{itemType}/{itemKey}` | 字典详情 | Form/Ref/BP 通用详情 |

### 7.5 布局层级

```
┌──────────────────────────────────────────────────┐
│ Topbar (48px)   [☰ ClassView]    [主题] [DB状态] │
├──────────────────────────────────────────────────┤
│ Bookmark Bar (32px)  [收藏1] [收藏2] ...          │
├──────────────────────────────────────────────────┤
│ Tab Bar (36px)  [实体查询✕] [详情✕] ...           │
├──────────┬───────────────────────────────────────┤
│ Sidebar  │ Main Content                          │
│ (200px)  │                                       │
│          │                                       │
│ 实体查询  │                                       │
│ Form查询 │                                       │
│ View查询 │                                       │
│ 参照查询  │                                       │
│ BP查询   │                                       │
│ OQL工具  │                                       │
│ 最近浏览  │                                       │
│ 数据库设置│                                       │
└──────────┴───────────────────────────────────────┘
```

### 7.6 通用搜索页流程

```
进入页面 → 加载缓存的 keyword/fuzzy → 自动执行搜索
搜索无关键词 → 过滤 ClickCount>0 或 IsFavorite → 都没有则取前50条
渲染表格 → 绑定收藏星标/链接点击/备注失焦保存
```

### 7.7 实体详情特有功能

- **置顶字段**：勾选字段 → 点击置顶 → 该字段排在表头
- **SQL 生成**：根据实体表名，自动找修改日期/创建日期/审核日期字段，生成 `select top 100 * from [表名] order by [日期字段] desc`，结果在右侧抽屉展示
- **类型跳转**：字段 FullName 以 `ufida` 开头（不区分大小写）→ 点击后搜索对应实体 → 跳转详情

### 7.8 点击计数规则

- ✅ 进入实体详情页 → `POST /api/recent/click` + `POST /api/recent/record`
- ✅ 详情内类型跳转到另一实体 → 目标实体点击 +1
- ❌ 搜索页点击收藏 → 不增加点击
- ❌ 搜索页编辑备注 → 不增加点击
- ❌ 搜索页普通点击链接 → 前端不调用 click 接口（但会跳转详情页，详情页自身会调用）

## 八、日志架构

| 组件 | 位置 |
|---|---|
| `FileLogger` | `App_Code/FileLogger.cs` |
| `ApiLogFilter` | `App_Code/ApiLogFilter.cs` |
| 日志目录 | `App_Data/Logs/` |
| 文件命名 | `classview-yyyyMMdd.log` |

日志内容：时间戳 + 级别 + 消息 + HTTP 方法 + URL + IP + 异常堆栈

敏感信息处理：`FileLogger.MaskSensitive()` 将连接串中 `Password=xxx` / `Pwd=xxx` 替换为 `Password=***`

## 九、Web.Config 关键配置

```xml
<connectionStrings>
  <add name="ConnectionString" connectionString="...ERP库..." />
  <add name="MetaConnection" connectionString="...SqlList库..." providerName="System.Data.SqlClient" />
</connectionStrings>

<system.web>
  <compilation debug="true" targetFramework="4.8">
    <!-- Web API / Newtonsoft.Json 程序集注册 -->
  </compilation>
  <httpRuntime targetFramework="4.8" />
</system.web>

<system.webServer>
  <modules runAllManagedModulesForAllRequests="true">
    <!-- URL 路由模块 -->
  </modules>
  <handlers>
    <!-- 无扩展名 URL 处理 + .aspx 处理 -->
  </handlers>
  <validation validateIntegratedModeConfiguration="false" />
</system.webServer>

<runtime>
  <!-- Newtonsoft.Json / System.Web.Http / System.Net.Http.Formatting 程序集重定向 -->
</runtime>
```

## 十、部署

### 10.1 一键部署

```powershell
# 以管理员身份运行（需 Windows PowerShell 5.1）
.\deploy\iis\deploy-classview.ps1
```

脚本执行顺序：
1. 写入 `Web.Config` 连接字符串
2. 还原 NuGet 包 → 拷贝 DLL 到 `Bin/`
3. 创建 IIS 应用池（.NET 4.0 集成模式）
4. 创建 IIS 站点（默认端口 8999）
5. 设置 `IIS_IUSRS` 读取权限 + `App_Data` 写入权限
6. 执行 SqlList 建库 + 建表脚本
7. `iisreset`

### 10.2 手动部署要点

- 应用池：`.NET CLR v4.0`，集成模式
- 端口：8999（可改脚本变量 `$BACKEND_PORT`）
- 日志目录需写入权限：`App_Data\Logs`
- 连接字符串需在 `Web.Config` 中正确配置

### 10.3 健康检查

```
http://localhost:8999/api/health/check
```

返回 ERP 和 SqlList 连接状态。

## 十一、Bin 依赖清单

| DLL | 说明 |
|---|---|
| `U9.Subsidiary.Lib.dll` | U9 基础库（WebGlobal, ConnectionModel） |
| `UBF.System.dll` | UBF 系统 |
| `UFSoft.UBF.Business.dll` | UBF 业务层 |
| `UFSoft.UBF.MD.dll` | UBF 元数据 |
| `UFSoft.UBF.View.Query.dll` | UBF 视图查询（OQL 解析） |
| `UFSoft.UBF.Util.Data*.dll` | UBF 数据工具 |
| `Newtonsoft.Json.dll` (v13) | JSON 序列化 |
| `System.Web.Http.dll` (v5.2.9) | Web API 2 |
| `System.Web.Http.WebHost.dll` | Web API WebHost |
| `System.Net.Http.Formatting.dll` | HTTP 格式化 |
| `System.Web.Cors.dll` | CORS 支持 |

## 十二、数据模型（DTO）

### 请求模型

```csharp
// ConfigController
public class DbConnectionProfile {
    int Id; string Name, Server, DatabaseName, UserName, Password; bool IsDefault;
}

// FavoriteController
public class FavoriteToggleRequest {
    string ItemType, ItemKey, Title, Subtitle, ExtraJson;
}

// RecentController
public class RecentRecordRequest { string ItemType, ItemKey, Title, Subtitle, ExtraJson; }
public class RecentClickRequest { string ItemType, ItemKey; }

// NoteController
public class NoteSaveRequest { string ItemType, ItemKey, Note; }

// OqlController
public class OqlParseRequest { string Oql; }
public class OqlExecuteRequest { string Sql; }
```

### ItemKey 规则

| ItemType | ItemKey 格式 | 示例 |
|---|---|---|
| `entity` | 实体 ID | `"12345"` |
| `form` | Form UID | `"{guid}"` |
| `view` | `ClassName\|ViewName` | `"UFIDA.U9.SM.SO.SOHead\|SOHeadView"` |
| `reference` | FormId | `"{guid}"` |
| `bp` | FullName | `"UFIDA.U9.SM.SO.SOHeadBP"` |
| `entity_attr` | `实体ID\|字段名` | `"12345\|Name"` |
| `view_field` | `ClassName\|ViewName\|字段名` | `"Cls\|View\|Field"` |

## 十三、旧 WebForms 页面

保留的旧页面（暂不删除，作为回退入口）：

| 页面 | 功能 |
|---|---|
| `Default.aspx` | 默认首页 |
| `DataConfig.aspx` | 数据库配置 |
| `QueryField.aspx` | 字段查询 |
| `QueryForm.aspx` | Form 查询 |
| `QueryView.aspx` | View 查询 |
| `QueryRef.aspx` | 参照查询 |
| `QueryBP.aspx` | BP 查询 |
| `OqlAndSql.aspx` | OQL 工具 |

## 十四、已知风险与注意事项

1. **控制器目录重复**：`App_Code\Api\Controllers` 和 `Api\Controllers` 存在同名控制器，主编辑位置应为前者
2. **SQL 拼接**：ERP 查询使用 `EscapeLike()` 防简单注入，但非参数化查询，长期应迁移为参数化
3. **OQL 执行风险**：`/api/oql/execute` 可执行任意 SQL，当前不做安全专项改造
4. **Web Site 项目**：运行时编译，文件放错目录可能不参与编译
5. **CORS 全开**：`WebApiConfig` 中 `EnableCorsAttribute("*", "*", "*")`，生产环境需收紧
6. **连接串密码明文**：SqlList 中 `db_connection_profile.password` 为明文存储

## 十五、开发调试指南

### 本地调试后端

1. 用 IIS 部署（或 IIS Express）
2. 确保 `Web.Config` 中 `ConnectionString` 和 `MetaConnection` 正确
3. 确保 SqlList 数据库已初始化（运行 `init_classview_meta.sql`）
4. 确保 `Bin/` 下所有依赖 DLL 存在
5. 访问 `http://localhost:8999/api/health/check` 验证

### 本地调试前端

1. 前端是纯静态文件，可直接用任意 HTTP 服务器托管 `frontend/` 目录
2. 需通过 `?apiBase=http://localhost:8999/api` 指定后端地址
3. 或设置 `window.CLASSVIEW_API_BASE` 全局变量
4. 后端已开启 CORS，跨域请求可正常工作

### 常用调试 URL

```
http://localhost:8999/api/health/check
http://localhost:8999/api/config/current
http://localhost:8999/api/entity/search?keyword=&fuzzy=true
http://localhost:8999/api/entity/attributes?id=xxx
http://localhost:8999/api/favorite/list
http://localhost:8999/api/recent/list?top=50
```

### 日志排查

```
App_Data/Logs/classview-yyyyMMdd.log
```

日志中搜索 `API error` 或 `failed` 定位问题。
