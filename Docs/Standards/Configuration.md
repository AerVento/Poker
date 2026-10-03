# 配置文件规范

`Configs/` 下所有 JSON 配置都必须遵守本规范。新增配置或修改格式时，同时更新对应的数据类型和文档。

## 字段名

- 字段名全部用小写英文单词，单词之间用短横线连接，例如 `item-id`、`max-quantity`、`connect-timeout-seconds`。
- 字段值不受此限制。直接对应 C# 枚举的值保留枚举名，例如 `"kind": "Dollars"`、`"type": "WinWithPattern"`、`"FullHouse"`。ID 按各自的规则书写，已发放或已分配的 ID 不能为了统一格式而修改。
- 读取配置的 `ConfigManager` 统一使用短横线命名策略：数据类型的成员照常用大驼峰命名，不需要逐个写 `JsonProperty("名字")`。

## 必填与可选

- 字段原则上必填。缺省时会被当成一个能实际生效的值（如 `0`、`1`、`true`、`false`、空字符串）的字段，必须必填，并在文件里写出来，包括空值，如 `"artwork": ""`。
- 只有缺省时是 `null`，且代码专门处理了 `null` 的字段才可以可选。目前的可选字段：
  - 每日任务的 `description`：不写时由任务规则生成说明。
  - 奖励条目的 `item-id`：只有 `"kind": "Item"` 的条目填写。
- 新增可选字段前必须先征得项目负责人同意，并加进上面的清单。
- 数据类型中，必填字段标 `[JsonProperty(Required = Required.Always)]`；可选字段的默认值为 `null`，并标 `NullValueHandling = NullValueHandling.Ignore`。

## 结构

- 只有一个列表的文件，根节点直接是数组，不再套一层只有一个字段的对象。例如 `Items.json`、`Shop.json`、`StarterGrants.json`、`TaskDefinitions.json`、`FixedTasks.json`。
- 奖励统一写成条目数组：`[{ "kind": "Dollars", "quantity": 1000 }, { "kind": "Item", "item-id": "rotten-egg", "quantity": 1 }]`。
- 有顺序要求的列表直接按数组顺序生效，不另设排序字段。
- 配置不写格式版本号或修订号。服务器只在启动时加载并校验一次全部配置，运行期间不重新读取磁盘；修改后重启服务器即生效。
- 引用配置条目的持久化数据（如已分配的每日任务）只保存条目 ID，名称、规则、奖励和顺序始终以当前配置为准。

## 校验

- 结构校验由 `ConfigManager` 完成：重复字段、未知字段、类型不符、数值溢出和缺少必填字段都会报错，报错使用文件中的字段名。
- 业务校验在加载之后进行，见 [Deployment](../Operations/Deployment.md#配置类型与加载)。服务器启动时报告全部问题后再退出，不会退回默认配置。
