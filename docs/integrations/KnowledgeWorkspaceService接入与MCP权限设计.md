# Knowledge Workspace Service 接入与 MCP 权限设计

> 状态：已实现 KWS 鉴权适配，客户端接入待确认
> 更新时间：2026-08-05
> 适用范围：Lumine AuthService 与 `knowledge-workspace-service` 的集成边界、MCP 工具权限映射和后续实现约定

本文档总结 `E:\Work\Code\Tools\knowledge-workspace-service` 当前实现，并给出将其纳入 Lumine 认证与权限体系时的建议。

当前 Lumine 仓库负责 API-KEY 管理、换取 `labchrom_knowledge` audience 的 JWT 以及权限种子；KWS 已实现 API-KEY→JWT 换取、JWKS/JWT 校验和 MCP 工具权限检查。Codex 的具体 MCP 客户端配置仍单独确认，本文不固化客户端配置细节。

## 1. 服务定位

`Knowledge Workspace Service` 是一个独立的 Python 3.12 Docker 服务，将 `Knowledge/` 目录作为知识正文事实源，通过 Streamable HTTP MCP 提供受控的知识库浏览、检索、修改、事务提交和运行时索引维护能力。

主要端点：

| 端点 | 用途 | 认证建议 |
| --- | --- | --- |
| `/health` | 存活检查 | 可公开给容器编排或内网探针 |
| `/ready` | 就绪、索引和恢复状态检查 | 由运维网络访问；不作为业务权限判断 |
| `/mcp/` | MCP 握手和工具调用 | 接入前必须由传输层强制 Bearer Token |

服务本身负责路径安全、内容校验、Manifest、全文索引、事务 overlay、发布、审计和故障恢复；Lumine 应负责调用者身份、角色和业务权限来源。

## 2. MCP 工具和建议权限

当前 MCP 工具共 21 个。KWS 接收 Lumine API-KEY 后向 `/api/auth/api-key/token` 换取 JWT，再按 JWT 的 `permission` / `permissions` claims 执行以下权限检查：

| 建议权限 | 权限含义 | 适用工具 |
| --- | --- | --- |
| `knowledge.manufacturer.view` | 显式选择厂家后，只查询通用知识、该厂家页面和该厂家绑定的仪器；当前不绑定 Lumine 用户 | `read_me`、`list_manufacturers_mcp`、`query_manufacturer_knowledge_mcp`、`read_manufacturer_document_mcp` |
| `knowledge.view` | 开发者读取和检查全知识库，不改变正式知识或运行时索引；KWS 内部自动包含厂家范围查询 | `browse_knowledge`、`read_document`、`get_document_relations`、`search_knowledge`、`get_maintenance_status`、`check_knowledge`、`validate_document`；`sync_knowledge(mode="dry_run")` |
| `knowledge.manage` | 修改正式知识，或管理一次知识变更事务；自动包含 `knowledge.view` | `create_file`、`write_file`、`patch_file`、`move_file`、`delete_file`；`begin_transaction`、`get_transaction`、`preview_transaction`、`commit_transaction`、`rollback_transaction` |
| `knowledge.maintenance` | 应用目录与索引对账，或重建运行时索引；自动包含 `knowledge.view` | `sync_knowledge(mode="apply")`、`rebuild_runtime_index` |
| `knowledge.instruments.view` | 开发者查询全局仪器索引、class、setup 和 evidence，不修改仪器元数据；KWS 内部自动包含厂家范围查询 | `query_instruments_mcp` |
| `knowledge.instruments.manage` | 更新仪器 occurrence、JSON 注册表和 `all_indexes.sqlite`；自动包含仪器查询权限 | `update_instrument_metadata_mcp` |

权限语义建议如下：

- `knowledge.manage` 的有效授权应同时包含 `knowledge.view`，便于编辑者先检索、读取和校验再修改。
- `knowledge.maintenance` 不应自动等同于普通编辑权限。`sync_knowledge(mode="apply")` 会把绕过 MCP 的磁盘变更纳入正式索引和 Manifest，`rebuild_runtime_index` 会重建运行时索引，应只授予受信任的维护账号。
- `knowledge.instruments.manage` 应同时分配 `knowledge.instruments.view`；仪器元数据更新会修改派生 JSON 索引和 `all_indexes.sqlite`，但不等同于普通知识文件写入。
- `sync_knowledge` 是同一个工具的分级操作：`dry_run` 只需要 `knowledge.view`，`apply` 需要 `knowledge.maintenance`。
- MCP 握手、`tools/list` 和传输层认证属于服务接入控制，不应另造一个业务权限；业务权限在工具调用时检查。
- 当前厂家查询阶段不实现 Lumine 用户—厂家分配。部署端 `KWS_MCP_API_KEY` 只授予 KWS 本地 `knowledge.manufacturer.view`；它不能调用开发者全库 `search_knowledge`、`read_document` 或目录浏览。现有 Lumine `knowledge.view` / `knowledge.instruments.view` 在 KWS 内部自动获得厂家范围查询能力，因此本阶段不新增厂家用户或厂家角色种子。

## 3. Lumine 角色建议

不要把 KWS 的 `reader`、`editor` 直接设计成 Lumine 的全局角色。它们只是当前 KWS 的两档本地 Token 能力。接入 Lumine 后，建议按权限组合角色：

| 角色示例 | 权限 | 适用场景 |
| --- | --- | --- |
| `KnowledgeReader` | `knowledge.view` | 问答、检索、浏览和只读诊断 |
| `KnowledgeEditor` | `knowledge.view`、`knowledge.manage` | 受控创建、修改和事务提交 |
| `KnowledgeMaintainer` | `knowledge.view`、`knowledge.manage`、`knowledge.maintenance` | 知识维护及索引/Manifest 运维 |
| `InstrumentReader` | `knowledge.instruments.view` | 查询仪器元数据 |
| `InstrumentMaintainer` | `knowledge.instruments.view`、`knowledge.instruments.manage` | 更新仪器元数据和派生索引 |

这些角色名称只是建议。当前项目的实际角色由 `AuthSeedOptions` 和数据库种子决定，接入实现时应根据部署组织的角色体系选择是否新增。

厂家终端用户角色不在本阶段范围内；在用户—厂家映射落地前，不创建仅凭调用方参数就代表厂家身份的 Lumine 角色。

## 4. KWS 当前认证能力和集成限制

KWS 当前使用两枚环境变量 Token：

- `MCP_READER_TOKEN`：reader Token；可访问只读能力，写入和事务操作拒绝，`sync_knowledge` 仅允许 dry-run。
- `MCP_EDITOR_TOKEN`：editor Token；继承 reader 能力，并允许文件写入、事务操作、同步 apply 和索引重建。

KWS 会从 Token 派生脱敏的 `token_id`，事务所有权和审计以该 Token ID 为准；请求参数中的 `actor` 只是非权威元数据，不能伪造调用者身份。事务只能由创建它的同一 Token 继续预览、提交或回滚。

KWS 直接接收 `Bearer sk-...` API-KEY，调用 Lumine 的 `/api/auth/api-key/token` 换取 access token，并通过配置的 JWKS 本地校验签名、issuer、audience、有效期、subject 和 `token_use=access_token`。API-KEY 只在请求处理和短期内存缓存中存在，不写入日志、审计、事务或运行时文件。旧 reader/editor Token 仍作为兼容入口。

## 5. 推荐集成方式

### 5.1 长期方案：KWS 接入 Lumine JWT

KWS 当前已实现 Lumine API-KEY/JWKS 适配层：

1. 校验 Lumine JWT 的 issuer、签名、audience、有效期、`sub` 和 access-token 标识。
2. 从 `permission` / `permissions` claims 得到 `knowledge.*` 权限。
3. 在 MCP 统一工具包装层按工具和参数模式执行权限映射。
4. 事务所有权绑定到 API-KEY 派生的脱敏身份；请求中的 `actor` 仍只是非权威元数据。
5. API-KEY 默认每 5 分钟重新向 AuthService 换取一次 JWT，以便较快感知 API-KEY 撤销；若要求即时撤销，可增加实时权限检查。

这种方式才能保留 Lumine 的用户级撤权、角色变更和审计关联能力。

### 5.2 过渡方案：独立 KWS Token

在 KWS 尚未支持 Lumine JWT 前，可以用独立 Token 运行：

- 只读 Agent 使用 `MCP_READER_TOKEN`。
- 明确承担知识维护的受信任 Agent 使用 `MCP_EDITOR_TOKEN`。
- Token 只放在部署环境密钥中，不写入仓库、客户端配置文件或文档。
- 不要把 Lumine 用户 JWT 直接当作 KWS Token，也不要把 `actor` 参数当作权限依据。
- 需要接受一个限制：KWS 审计只能区分 reader/editor Token，不能可靠还原每个 Lumine 用户。

过渡方案适合单一受信任 Agent 或内网部署，不适合作为面向多用户的最终权限方案。

## 6. 接入实施时的同步项

当开始实际接入时，应按以下顺序修改：

1. `knowledge.view`、`knowledge.manage`、`knowledge.maintenance`、`knowledge.instruments.view` 与 `knowledge.instruments.manage` 已加入 `AuthSeedOptions` 和 `SeedData:Permissions`；管理员种子角色会自动获得全部已配置权限，其他角色需显式分配。
2. 在 KWS JWT 适配层或 MCP 网关中实现工具级权限检查；不要只在 AuthPortal 隐藏入口。
3. 为 Lumine 用户、OIDC 客户端或服务账号定义调用身份和事务所有权规则。
4. 将 KWS 接入说明、权限清单和必要的部署密钥配置同步到 `docs/integrations`、`docs/permissions` 和部署文档。
5. 补充 reader/editor、五种 `knowledge.*` 权限、仪器查询/更新、dry-run/apply 和跨身份事务操作的集成测试。

当前 KWS 已具备 Lumine API-KEY→JWT→MCP 权限检查链；Codex/QwenPaw 具体客户端如何注入 API-KEY，另行根据客户端能力确定。
