# SuperDog4.1 生产接口

本文是既有 AuthServer 内生产接口的当前契约。API 前缀为 `/api/dongle-licenses`，客户端标识精确为 `superdog41`，显示名为 `SuperDog4.1`。

## 登录与每批权限复核

`POST /api/auth/login` 使用 JSON：

```json
{"userName":"<账号>","password":"<密码>","clientId":"superdog41","scope":"openid profile roles permissions"}
```

这是直接账号密码登录，非 PKCE；返回既有 `access_token`、`expires_in`、`user.id/userName/roles/permissions`，不增加刷新令牌。客户端每批烧录开始调用 `GET /api/auth/me/permissions`，检查 `isActive`、`id` 与原 session subject 一致，替换权限快照；此操作不刷新 access token。该接口会实时读取数据库，账号停用返回 401；客户端遇到 401 须重新登录，不能继续使用旧权限快照。

生产接口仅接受有效 Bearer、`aud=superdog41`、启用账号及数据库中实时授予的对应权限。沿用既有 Admin 角色种子规则：配置权限补入 Admin；普通账号仅按其角色获权，不因客户端名称或账号名称跳过权限检查。

## 厂家 KEY

| POST 路径 | 请求体 | 权限 | 成功响应 |
| --- | --- | --- | --- |
| `/manufacturer-keys/ensure` | `{ "manufacturerId": 4 }` | `superdog.keys.manage` | `{ "manufacturerId": 4, "keyId": "<UUID>", "version": 1 }` |
| `/manufacturer-keys/retrieve` | 同上 | `superdog.keys.view` | `{ "manufacturerId": 4, "keyId": "<UUID>", "version": 1, "plainText": "<KEY>" }` |
| `/manufacturer-keys/copy` | 同上 | 同时需要 `superdog.keys.view`、`superdog.keys.copy` | 与 retrieve 相同 |

ensure 为管理端可调用的创建/确认接口，只返回标识和版本，不返回明文；按 `(vendorId, manufacturerId)` 唯一创建，重复请求返回同一行。retrieve 在 KEY 缺失且账号同时具备 `superdog.keys.view` 和 `dongle.activation.manage` 时，在同一事务中创建共享 KEY、记录创建/查看审计并返回；额外创建权限实时查询数据库角色，不信任 token 权限快照。只有 view 时可读取既有 KEY，缺失返回404；copy 永不创建。停用行返回404且不会被自动替换，厂家 KEY 重用不消费。`manufacturerId=1` 是清单中的独立厂家范围，不是自动通配符。

vendorId 是硬件厂商批次，与业务厂家编号分开；本版从服务配置取得，不接受客户端覆盖。当前生产工具的 vendorId 为34936，部署须配置 `SuperDog:VendorId="34936"`，对应受信公钥条目的 `VendorId` 也使用字符串 `"34936"`。`ProductionUpload` 没有 vendor 字段，全部材料固定绑定当前服务实例的 vendor 信任范围，切换硬件厂商批次须另行配置实例。厂家 KEY 为密码学安全随机 32 字节的十六进制文本，AES-256-GCM 加密存入 MySQL。加密附加数据绑定 vendor、厂家编号、KeyId、版本；独立用途派生 HMAC 用于未来索引，不存 KEY 明文。所有生产响应禁止缓存。查看、复制、创建确认记录审计，审计不包含 KEY、密文或完整生产证明。部署入口必须使用 HTTPS，反向代理不得记录请求/响应体。

账号厂家授权的最小实现为：有相应角色权限即可访问当前 vendor 下全部配置的生产厂家。按账号限制厂家集合的授权范围待后续接入；当前 OAuth scope 也不代表厂家范围。

## 逐记录批量上传

`POST /api/dongle-licenses/production-records/batch`，需要 `superdog.records.upload`。本服务统一使用该路径；其他设计稿中的 `/registrations/batch` 应统一至此，服务没有该别名。

```json
{
  "records": [{
    "operationId": "<原OperationId>",
    "batchId": "<原BatchId>",
    "hardwareKeyId": 123,
    "form": {
      "fields": { "infoVersion": 1, "serialNumber": "0000000042", "manufacturerId": 4,
        "edition": 0, "applicationType": 0, "clientType": 0, "maxInstrumentSets": 3 },
      "operatorPinyin": "TEST"
    },
    "readOnlyBase64": "<112字节RO的Base64>",
    "readWriteBase64": "<112字节RW的Base64>",
    "payloadDigest": "<header16与有效XML字节的SHA256十六进制>",
    "keyId": "<厂家KEY的UUID>", "keyVersion": 1,
    "writtenBySubject": "<原烧录账号UUID>", "writtenByUserName": "<原烧录人>",
    "writtenAt": "2026-10-30T00:00:00Z",
    "productionRevision": 1
  }]
}
```

DTO 与 `SuperDog41.Core` 的 `ProductionUpload`、`UploadResult`、`ManufacturerKey` 同形，JSON 使用 camelCase，枚举使用数字：Edition=Pro/Gpc/Server 对应 0/1/2，ApplicationType=LC/GC/Flash 对应 0/1/2，ClientType=PC/Pad 对应 0/1。硬件 ID 使用 UInt64 JSON 数字，客户端不能经 JavaScript 浮点数中转而丢失精度。

`form.operatorPinyin` 必须为1—100个 ASCII 大写字母 `A–Z`；服务端直接拒绝中文、小写、数字、全角字母、空格和换行，不自动 trim 或转换大小写。

`productionRevision` 是同一硬件的本地业务生产修订，取值为1—2147483647，与狗内 `InfoVersion`、KEY 版本及电脑绑定代次分别管理。客户端在成功提交生产台账的同一事务中，将原 `DogRecord.Revision + 1` 固化进加密上传材料；首次生产为1，重写递增，补传保留原值。服务端拒绝缺失、零值和负值，不按 `writtenAt`、上传时间或到达次序判断生产先后。该字段属于获权生产端提交的登记元数据，不包含在狗内 SD41 签名中。

每批 1—100 条，HTTP 请求体最大 2 MiB；无效 JSON/批次格式返回 HTTP 400。生产接口按已认证账号限每分钟60个请求，超限返回429及 `Retry-After: 60`；限流为单服务实例内计数，多副本入口须另配全局限流。合法批次 HTTP 200，返回：

```json
{"results":[
  {"operationId":"op-1","success":true,"entitlementId":"<UUID>","error":null},
  {"operationId":"op-2","success":false,"entitlementId":null,"error":"409 operation_conflict"}
]}
```

条目错误保持 `UploadResult` 原契约，不新增 status 字段：

| error | 意义 |
| --- | --- |
| `400 invalid_material` | 签名、硬件 ID、七字段、编码、摘要或元数据无效，包括缺失或非正数 productionRevision |
| `400 invalid_operator_pinyin` | 烧录人拼音不是1—100个大写英文字母 |
| `400 manufacturer_key_mismatch` | 厂家 KEY 不存在、停用或 KeyId/版本不匹配 |
| `409 operation_conflict` | 同 vendor、OperationId 已上传不同有效材料 |
| `409 production_revision_conflict` | 同一硬件的该生产修订已由另一操作登记 |
| `409 production_revision_unavailable` | 既有资格缺少可信生产修订，须先受控核实原台账 |
| `503 configuration_unavailable` | vendor/受信签名公钥配置不可用 |
| `503 persistence_failed` | 本条持久化失败 |

客户端可对白名单中的固定错误码映射具体中文提示；未知错误显示通用提示，不将任意服务响应文本、DTO或生产材料直接写入日志/界面。字段和错误码仍使用原 `UploadResult.error`，不新增 DTO 字段。

每条独立事务保存原始生产材料、材料摘要、entitlement 及成功审计；一条失败不回滚已提交条目。同 `(vendorId, OperationId)` 同材料返回原 entitlement，异料拒绝；材料包括全部 ProductionUpload 字段，Base64、摘要大小写及时间偏移先规范化。生产签名字节也是原材料的一部分，重试必须保留原 RO/RW，不能重新签名。重新烧录须使用新 OperationId。

服务根据已验证的 RO 硬件 ID 与七字段确定来源及厂家，不信任表单覆盖值；公钥只按受控 `(vendorId, signingKeyId)` 配置选取，不接收请求公钥。协议固定为 SD41/v1、签名编号 1、P-256/SHA-256/P1363，校验完整 RO/RW 长度、补零、规范 UTF-8 七属性 XML、签名、硬件绑定及摘要。服务持有生产公钥，不持有生产签名私钥；验签证明材料由受信密钥签发，不能证明 USB 狗当前在线。

`writtenBySubject`/`writtenByUserName` 是原烧录来源声明，不作为本次上传的认证依据；允许获权人员上传已有生产记录。上传者 subject、服务接收时间单独保存于登记和审计，不能由请求覆盖。

资格按 `(vendorId, activationSourceHardwareKeyId)` 唯一；重写继续使用原 entitlement，不修改绑定状态/代次。新硬件可先收到大于1的生产修订；只有更高修订可以更新资格中的厂家及摘要。较低修订首次补传时保留独立登记历史并返回成功，但不覆盖当前资格；同修订换 OperationId 返回冲突。同 OperationId 原材料重放始终返回原结果，不回退当前资格。登记按 `(entitlementId, productionRevision)` 唯一，资格的当前生产修订作为 EF 并发令牌；并发更新失败会重新读取、比较后有限重试，不能用旧快照覆盖新修订。成功审计通过 `current_revision` / `historical_revision` 区分当前更新与历史补传。

本版提供生产登记和 KEY 接口；激活、电脑绑定、注销、迁移及许可证签发由后续业务接入。

## 权限与配置

种子权限为 `superdog.read`、`superdog.write`、`superdog.rewrite`、`superdog.records.view`、`superdog.records.manage`、`superdog.records.export`、`superdog.records.upload`、`superdog.audit.view`、`superdog.audit.export`、`superdog.settings.manage`、`dongle.activation.manage`、`superdog.keys.view`、`superdog.keys.copy`，另有管理员创建 KEY 使用的 `superdog.keys.manage`。权限存在不代表本服务已经提供所有相关业务接口。

`SuperDog` Options：

| 配置 | 用途 |
| --- | --- |
| `VendorId` | 当前部署所接受的硬件厂商批次，必填，最长64字符 |
| `ManufacturerIds` | 受控厂家编号集合，默认现有清单1—32 |
| `EncryptionKeyBase64` | 独立32字节 AES 主密钥的 Base64，仅经环境/秘密管理注入 |
| `TrustedSigningKeys` | 数组，每项 `VendorId`、`SigningKeyId`、`PublicKeyPem`；后者接受 P-256 SubjectPublicKeyInfo 公钥的 PEM 或纯 Base64 DER SPKI，可直接粘贴 WPF 导出字符串；不接受私钥、非P-256密钥或DER尾随数据 |

环境键如 `SuperDog__VendorId`、`SuperDog__EncryptionKeyBase64`、`SuperDog__TrustedSigningKeys__0__PublicKeyPem`。仓库默认留空 vendor/主密钥/公钥；生产操作缺配置即拒绝，不生成默认主密钥。跨容器重启必须保留同一 AES 主密钥并与数据库共同备份；更换密钥必须先设计重加密迁移，不能直接覆盖配置，否则现有 KEY 无法解密。不得启用 EF sensitive data logging 或代理请求体日志。

## 数据库与验证

使用现有 MySQL/EF Core，migration `20261030062108_AddSuperDogProduction` 新增 `ManufacturerKeys`、`DongleEntitlements`、`ProductionRegistrations` 及唯一约束，不改既有认证表数据。启动沿既有流程执行 migration 和 seed。部署前应在隔离 MySQL 中检查迁移、逐条回滚、跨实例并发重放及同硬件竞争；本次离线验证不连接数据库。

migration `20261030065727_TrackSuperDogProductionRevision` 增加资格当前生产修订、登记修订及每资格修订唯一约束。无法确定修订的存量登记保留 null，存量资格保留0并停止接受新的生产更新；恢复须核对原生产台账和材料后受控补齐可信修订，不能通过时间排序或统一填1来猜测。

`AuthDbContextDesignFactory` 为 EF 设计时提供纯模型入口，避免生成迁移时启动 WebHost、读取 `.env` 或运行数据库初始化。

离线测试入口（依赖已还原时）：

```powershell
dotnet build Lumine.AuthServer/Lumine.AuthServer.csproj --no-restore -p:SkipBrowserBuild=true
dotnet run --project tests/SuperDogProduction.Tests --no-restore -p:SkipBrowserBuild=true
dotnet ef migrations has-pending-model-changes --project Lumine.AuthServer --no-build
```

离线断言覆盖独立签名材料、RO/RW逐字节篡改、七字段差异、非规范 XML、公钥信任边界、AES-GCM附加数据及密文篡改、DTO序列化、权限标注、seed 和 migration 约束；生产修订覆盖缺失/非正数拒绝、材料幂等摘要包含修订、反向时钟和乱序补传不覆盖新资格、同修订不修改资格、资格身份及绑定保留、并发令牌和修订唯一索引。它们不代表已经完成真实 HTTP/MySQL 并发联调。
