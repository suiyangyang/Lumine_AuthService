# Knowledge Workspace Service 接入与 MCP 权限设计

> 状态：接入设计
> 更新时间：2026-08-05
> 适用范围：Lumine AuthService 与 `knowledge-workspace-service` 的集成边界、MCP 工具权限映射和后续实现约定

本文档总结 `E:\Work\Code\Tools\knowledge-workspace-service` 当前实现，并给出将其纳入 Lumine 认证与权限体系时的建议。

当前 Lumine 仓库尚未实现对该服务的 Token 透传、MCP 代理或权限校验接管。`knowledge.instruments.view` 与 `knowledge.instruments.manage` 已加入 Lumine 权限种子，用于承接新增的仪器 MCP 接口；`knowledge.view`、`knowledge.manage`、`knowledge.maintenance` 仍属于整体 KWS 接入目标，尚未对 KWS 生效。

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

当前 MCP 工具共 21 个。建议在 Lumine 中采用 `module.action` 风格，使用一个知识库模块和五个权限点：

| 建议权限 | 权限含义 | 适用工具 |
| --- | --- | --- |
| `knowledge.view` | 读取和检查知识库，不改变正式知识或运行时索引 | `list_directory`、`read_file`、`search_files`、`search_knowledge`、`get_file_metadata`、`get_maintenance_status`、`check_knowledge`；`sync_knowledge(mode="dry_run")` |
| `knowledge.manage` | 修改正式知识，或管理一次知识变更事务 | `create_file`、`write_file`、`patch_file`、`move_file`、`delete_file`；`begin_transaction`、`get_transaction`、`preview_transaction`、`commit_transaction`、`rollback_transaction` |
| `knowledge.maintenance` | 应用目录与索引对账，或重建运行时索引 | `sync_knowledge(mode="apply")`、`rebuild_runtime_index` |
| `knowledge.instruments.view` | 查询知识库仪器索引，不修改仪器元数据 | `query_instruments_mcp` |
| `knowledge.instruments.manage` | 更新仪器 occurrence、JSON 注册表和 `all_indexes.sqlite` | `update_instrument_metadata_mcp` |

权限语义建议如下：

- `knowledge.manage` 的有效授权应同时包含 `knowledge.view`，便于编辑者先检索、读取和校验再修改。
- `knowledge.maintenance` 不应自动等同于普通编辑权限。`sync_knowledge(mode="apply")` 会把绕过 MCP 的磁盘变更纳入正式索引和 Manifest，`rebuild_runtime_index` 会重建运行时索引，应只授予受信任的维护账号。
- `knowledge.instruments.manage` 应同时分配 `knowledge.instruments.view`；仪器元数据更新会修改派生 JSON 索引和 `all_indexes.sqlite`，但不等同于普通知识文件写入。
- `sync_knowledge` 是同一个工具的分级操作：`dry_run` 只需要 `knowledge.view`，`apply` 需要 `knowledge.maintenance`。
- MCP 握手、`tools/list` 和传输层认证属于服务接入控制，不应另造一个业务权限；业务权限在工具调用时检查。

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

## 4. KWS 当前认证能力和集成限制

KWS 当前使用两枚环境变量 Token：

- `MCP_READER_TOKEN`：reader Token；可访问只读能力，写入和事务操作拒绝，`sync_knowledge` 仅允许 dry-run。
- `MCP_EDITOR_TOKEN`：editor Token；继承 reader 能力，并允许文件写入、事务操作、同步 apply 和索引重建。

KWS 会从 Token 派生脱敏的 `token_id`，事务所有权和审计以该 Token ID 为准；请求参数中的 `actor` 只是非权威元数据，不能伪造调用者身份。事务只能由创建它的同一 Token 继续预览、提交或回滚。

需要注意当前源码的认证接线状态：`BearerTokenAuthenticator.verify_token` 已实现，工具层也有 editor/reader 的角色判断；但当前 `FastMCP(...)` 创建代码没有启用 SDK 的传输层认证 provider，`main.py` 的 `/mcp` 挂载仅负责延迟转发。因此，仓库 README 和安装校验中期望的“无 Token 返回 401”，不能仅凭当前源码视为已完成的端到端保证。正式接入 Lumine 前，必须先补齐并验证 MCP HTTP 入口的 Bearer 校验；否则普通只读工具可能绕过统一认证入口。

因此，当前 KWS 不能直接接收 Lumine 签发的普通 OIDC JWT：当前实现是对两枚固定 Token 做比对，并不是按 Lumine 的 `sub`、角色或 `permission` claim 进行授权。当前 Lumine 也尚未实现 `client_credentials` grant，不能把它当成已经存在的机器到机器授权流程。

## 5. 推荐集成方式

### 5.1 长期方案：KWS 接入 Lumine JWT

推荐让 KWS 增加 Lumine OIDC/JWKS 验证适配层，或在 `/mcp/` 前增加受 Lumine 保护的 MCP 网关：

1. 校验 Lumine JWT 的 issuer、签名、有效期和 `sub`。
2. 从 `permissions` claim 或 Lumine 的实时权限检查接口得到 `knowledge.*` 权限。
3. 按工具和参数模式执行上表权限映射。
4. 将 Lumine 的用户 ID 作为可信审计主体传给 KWS；不能继续依赖客户端可填写的 `actor` 字段。
5. 事务所有权应绑定到用户/客户端身份，而不是所有用户共用一个 editor Token。

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

1. `knowledge.instruments.view` 与 `knowledge.instruments.manage` 已加入 `AuthSeedOptions` 和 `SeedData:Permissions`；后续整体 KWS 接入时，再新增 `knowledge.view`、`knowledge.manage`、`knowledge.maintenance`，并为目标角色分配权限。
2. 在 KWS JWT 适配层或 MCP 网关中实现工具级权限检查；不要只在 AuthPortal 隐藏入口。
3. 为 Lumine 用户、OIDC 客户端或服务账号定义调用身份和事务所有权规则。
4. 将 KWS 接入说明、权限清单和必要的部署密钥配置同步到 `docs/integrations`、`docs/permissions` 和部署文档。
5. 补充 reader/editor、五种 `knowledge.*` 权限、仪器查询/更新、dry-run/apply 和跨身份事务操作的集成测试。

当前项目已经登记两项仪器权限，但在 KWS JWT 适配层或 MCP 网关完成前，不应宣称 Lumine 已经保护 KWS MCP 接口；整体 KWS 的三项通用权限也仍未生效。
