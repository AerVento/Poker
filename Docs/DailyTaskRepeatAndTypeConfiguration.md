# 可重复任务、类型参数和奖励对象设计

更新：2026-09-07。状态：实现规范；本文对重复完成、领奖身份、类型配置的描述优先于之前的单次实例模型。重复轮次行为已确认：先领才开下一轮；全部轮次完成或到期释放随机名额。

关联：[总任务书](DailyTaskRefactorSpecification.md)、[三表与刷新](DailyTaskPoolAndServerConfiguration.md)、[验收清单](DailyTaskCombinationTestCases.md)。

## 1. 用户要表达的三个维度

- 单轮目标：例如 1 手葫芦，或 10 手两对。不是整个实例的总目标。
- maxCompletions：一个任务实例在自己的有效期内最多完成/领几轮奖励，正整数。示例每日奖励为 3，就是一天可领三次；多日任务为 3，就是整个多日有效期共三次，不是每天三次。
- 单轮奖励：每一轮发同一份实例快照奖励；总上限为单轮奖励乘 maxCompletions，运算须检查溢出。

固定表依旧每天创建新实例，因此下一业务日次数重置；随机多日实例不会因每天补位而重置轮数或延长截止日。固定/随机不应成为任务类的继承层次，来源和生命周期由池服务处理。

已计入永不因牌桌取消回退的规则继续有效；当前池/当前时间判断继续有效。完成一轮后的下一轮进度显示重置不等于撤销已经完成的轮次，UI 需要显式展示当前轮次。

## 2. 三个状态数与两个进度条

实例状态至少有：
- ObjectiveProgress[]：当前轮各子目标进度。
- CompletedCount：已经达标的轮数。
- ClaimedCount：已经发奖的轮数。
- MaxCompletions、ExpiresAt、配置版本和单轮奖励快照。
- 累计任务 PendingClaims = CompletedCount - ClaimedCount。

用户已确认：先领奖才开启下一轮，未领奖期间不累计。累计任务因此满足 0 <= ClaimedCount <= CompletedCount <= MaxCompletions，且 PendingClaims 只能为 0 或 1；不保存未来轮次的待用进度。第二条进度条是 CompletedCount/MaxCompletions，领奖不让它下降。

每轮葫芦 1 手，共 3 轮：

| 状态 | 本轮进度 | 完成次数 | 待领取 |
| --- | --- | --- | --- |
| 刚分配 | 0/1 | 0/3 | 0 |
| 第一手葫芦计入 | 1/1，等待领取 | 1/3 | 1 |
| 未领奖又出现葫芦 | 仍为 1/1 | 仍为 1/3 | 仍为 1 |
| 领第一轮，实例仍有效 | 第二轮 0/1 | 1/3 | 0 |
| 新一手葫芦计入 | 1/1，等待领取 | 2/3 | 1 |
| 领第二轮后再完成新一手 | 1/1，全部轮次完成 | 3/3 | 1 |
| 领完第三轮 | 1/1，全部已领取 | 3/3 | 0 |

等待领取时保持本轮满条，成功领取后才重置下一轮；已完成次数不会回退。全部轮次完成后不重置第一条为 0/1，不再开启第四轮。

DirectClaim 没有事件累计目标。领取成功同时增加 CompletedCount 与 ClaimedCount；Available 只表示当前可申请领取，不预生成永久奖励。每日奖励的三次成功领取依次为 1/3、2/3、3/3。直接领取类只需次数条，若 UI 统一两条，则第一条显示“当前可领取/条件未满足”，不可伪造历史累计。

## 3. 已确认的重复轮次与名额规则

1. 先领取本轮奖励才开启下一轮；等待领取期间不累计、不缓存、不追溯补记。任务消费者仍记录事件消费结果，以免重放时污染新轮。
2. 随机实例只占一个名额，不按剩余轮数占多个；完成第一/第二轮仍占位，全部轮次完成或到期才释放。最后一轮已经达标但尚未领取，也释放名额；待领卡独立保留。
3. 随机 DirectClaim 没有预先完成事实，故每次成功领取才完成一轮；最后一次成功领取或到期释放名额。
4. 单次领取完成后若还有次数且尚未到期，原实例开启下一轮，保留同一个 TaskInstanceId、原期限及次数上限。这不是再次抽任务，不触发每日补位。
5. 到期时有一轮已达标未领，该轮奖励按累计目标规则保留；尚未完成或尚未开启的轮次全部失效。到期后领掉旧轮不能再开下一轮。
6. DirectClaim 剩余未领次数到期作废，不因 Available 曾为 true 转成永久待领；当日限制与多次领取并不冲突。

固定每日新实例重置最多轮数，旧日已赚的累计奖励保持原卡；随机有效期内跨日的实例不重置。每日随机补位仍按原规则一天一次，不能通过领奖开启下一轮触发抽签。

## 4. type + params 配置契约

采用用户提出的 type 标识和 params 数组。加载时一次校验、转换为强类型对象；运行时不再把玩家、奖励数、事件混进同一个 object[]。schemaVersion=2 表示已更换原 kind/objectives/reward 的外部格式；旧示例按新格式同步重写，尚未实现自动迁移。

所有顶层任务 params[0] 是 RewardValue：
- 整数，例如 1000：纯筹码奖励的简写。
- 对象，例如 { "chip": 1000, "items": [] }：完整奖励。
- 加载器将两种表示统一编译为 Reward，运行时只有一种表示；不做字符串转整数等隐式宽松转换。

注册类型与参数表：

| type / C# 类 | params 按顺序 | 语义 |
| --- | --- | --- |
| DirectClaim | [reward] | 每轮直接领取，仍检查次数、有效期和基础领取资格 |
| DirectClaimWhenLesserThan | [reward, threshold] | 领取当刻筹码严格 < threshold |
| DirectClaimWhenAtMost | [reward, threshold] | 领取当刻筹码 <= threshold，保留现有东山再起口径 |
| PlayHands | [reward, handsPerCompletion] | 每轮正常完成 X 手，Fold 参与手在正常结算时也算，取消不产生成功结算 |
| FormPattern | [reward, pattern, handsPerCompletion] | 每轮 X 手各曾形成指定牌型；通知到达即计数，无需获胜 |
| WinHands | [reward, winsPerCompletion] | 每轮正常结算获胜 X 手，任意奖池/平分算获胜 |
| WinWithPattern | [reward, pattern, winsPerCompletion] | 每轮 X 手各曾形成指定牌型且最后获胜；不要求最终牌型相同 |
| CountHands | [reward, handsPerCompletion, conditions] | 高级同手 AND 模板，支持同手葫芦与四条等组合 |
| AllObjectives | [reward, objectives] | 每轮多个独立子目标全部达标，领奖后一起重置 |

CountHands 内部组合 IHandCondition 和 CountDistinctHandsObjective，不为“两对1手”“葫芦3手”各写一个新类。AllObjectives 的子目标类型注册表与顶层任务类型表分开，子目标没有独立奖励和重复次数。

条件同样为 type + params，例如 PatternObserved:["FullHouse"]、Won:[]、HandSettled:[]。参与由手牌目标强制检查；纯牌型在牌型通知计入，含 Won/HandSettled 的计划等成功结算。不给配置者另选互相矛盾的代码事件时机。CountHands 空条件不自动解释为无条件成功，要求显式配 HandSettled 或牌型等支持的条件。

参数数组为存储格式，编辑器根据类型元数据展示“每轮奖励”“每轮手数”“牌型”“可完成次数”等有名称/类型/范围的字段，并生成预览；用户无需记 objs[2] 的含义。服务端与编辑器共用参数校验器。错误示例应报告 TaskDefinitions.json、任务 ID、params[1]、预期正整数和实际错误类型。

未知类名报错，不通过 Type.GetType 反射实例化任意 C# 类型；注册表将稳定短名称映射为已编译工厂。新增参数值或牌型不改代码，新增任务语义需要实现并注册类。

### 4.1 三个重点任务

每日奖励一天领取三次：

```json
{
  "id": "daily_supply",
  "name": "每日奖励",
  "type": "DirectClaim",
  "params": [
    1000
  ],
  "maxCompletions": 3
}
```

东山再起保持当前项目原先 <=1000 口径，用 DirectClaimWhenAtMost；不是将用户示例的 < 偷换为 <=。若产品希望严格小于，换成 DirectClaimWhenLesserThan，其余参数不变：

```json
{
  "id": "comeback",
  "name": "东山再起",
  "type": "DirectClaimWhenAtMost",
  "params": [
    2000,
    1000
  ],
  "maxCompletions": 1
}
```

每轮葫芦一手，共完成三轮，每轮奖励 500：

```json
{
  "id": "full_house_1_repeat_3",
  "name": "葫芦三次",
  "type": "FormPattern",
  "params": [
    500,
    "FullHouse",
    1
  ],
  "maxCompletions": 3
}
```

它至少需要三手不同的牌，不能用同一手翻牌葫芦、转牌仍葫芦、河牌仍葫芦完成三轮。每次条件成立只评估一次旧轮状态，不把同一个事件递归投入刚重置的新轮。

### 4.2 两个独立子目标仍可配置

```json
{
  "id": "play_10_and_two_pair_once",
  "name": "游玩十手并组成一次两对",
  "type": "AllObjectives",
  "params": [
    1500,
    [
      {
        "id": "played_hands",
        "type": "CountDistinctHands",
        "params": [
          10,
          [
            {
              "type": "HandSettled",
              "params": []
            }
          ]
        ]
      },
      {
        "id": "two_pair_hands",
        "type": "CountDistinctHands",
        "params": [
          1,
          [
            {
              "type": "PatternObserved",
              "params": [
                "TwoPair"
              ]
            }
          ]
        ]
      }
    ]
  ],
  "maxCompletions": 1
}
```

每轮需正常玩 10 手，并且有 1 手曾组成两对。若重复次数大于 1，每轮全部子目标完成且领取成功后才一起开始新轮，旧轮已消费事实不再使用，不将先完成子目标的额外事件积攒给未来轮次。第一版不增加跨轮溢出库存，编辑器明确预览此建议口径。

### 4.3 剩余具体类型配置

以下定义可按需加入总表，再由一张池表引用；示例仅展示类型契约，不自动加入玩家任务池。

```json
[
  {
    "id": "play_10",
    "name": "游玩十手",
    "type": "PlayHands",
    "params": [
      500,
      10
    ],
    "maxCompletions": 1
  },
  {
    "id": "win_3",
    "name": "获胜三手",
    "type": "WinHands",
    "params": [
      800,
      3
    ],
    "maxCompletions": 1
  },
  {
    "id": "win_full_house_2",
    "name": "曾有葫芦并获胜两手",
    "type": "WinWithPattern",
    "params": [
      1200,
      "FullHouse",
      2
    ],
    "maxCompletions": 1
  },
  {
    "id": "full_house_and_quads",
    "name": "同一手曾有葫芦和四条",
    "type": "CountHands",
    "params": [
      1600,
      1,
      [
        {
          "type": "PatternObserved",
          "params": [
            "FullHouse"
          ]
        },
        {
          "type": "PatternObserved",
          "params": [
            "FourOfAKind"
          ]
        }
      ]
    ],
    "maxCompletions": 1
  },
  {
    "id": "strict_comeback",
    "name": "严格低于门槛的救济",
    "type": "DirectClaimWhenLesserThan",
    "params": [
      2000,
      1000
    ],
    "maxCompletions": 3
  },
  {
    "id": "play_10_and_two_pair_once",
    "name": "游玩十手并组成一次两对",
    "type": "AllObjectives",
    "params": [
      1500,
      [
        {
          "id": "played_hands",
          "type": "CountDistinctHands",
          "params": [
            10,
            [
              {
                "type": "HandSettled",
                "params": []
              }
            ]
          ]
        },
        {
          "id": "two_pair_hands",
          "type": "CountDistinctHands",
          "params": [
            1,
            [
              {
                "type": "PatternObserved",
                "params": [
                  "TwoPair"
                ]
              }
            ]
          ]
        }
      ]
    ],
    "maxCompletions": 1
  }
]
```

## 5. C# OOP 建议

用户提出的 Available/ClaimReward 表达“能否领取”和“本次给什么”。还需事件评估入口，否则难以表达 10 手牌的累积。建议使用 GameTask 名称，避免与 System.Threading.Tasks.Task 冲突；所有任务仍放在 Game.Tasks 领域命名空间。

### 5.1 具体任务类应完整覆盖的构造方式

以下是 C# 9 设计草图，不是已接入 Unity 的实现；TaskContext/TaskState/TaskEvent/ProgressChange、ObjectivePlan、ObjectiveEvaluator 和条件类由实施阶段补齐。每个类的参数与第 4 节注册表一一对应；轮次、去重、持久化、实际发奖不在每个类里复制。

```csharp
public abstract class GameTask
{
    public abstract ProgressChange Evaluate(TaskEvent fact, TaskState state);
    public abstract bool Available(TaskContext context, TaskState state);
    public abstract Reward GetReward(TaskContext context);
}

public abstract class ConfiguredTask : GameTask
{
    private readonly Reward _reward;

    protected ConfiguredTask(Reward reward)
    {
        _reward = reward;
    }

    public override Reward GetReward(TaskContext context)
    {
        return _reward;
    }
}

public class DirectClaim : ConfiguredTask
{
    public DirectClaim(Reward reward) : base(reward)
    {
    }

    public override ProgressChange Evaluate(TaskEvent fact, TaskState state)
    {
        return ProgressChange.None;
    }

    public override bool Available(TaskContext context, TaskState state)
    {
        return context.IsWithinValidity &&
            state.ClaimedCount < state.MaxCompletions;
    }
}

public sealed class DirectClaimWhenLesserThan : DirectClaim
{
    private readonly uint _threshold;

    public DirectClaimWhenLesserThan(Reward reward, uint threshold)
        : base(reward)
    {
        _threshold = threshold;
    }

    public override bool Available(TaskContext context, TaskState state)
    {
        return base.Available(context, state) &&
            context.PlayerChip < _threshold;
    }
}

public sealed class DirectClaimWhenAtMost : DirectClaim
{
    private readonly uint _threshold;

    public DirectClaimWhenAtMost(Reward reward, uint threshold)
        : base(reward)
    {
        _threshold = threshold;
    }

    public override bool Available(TaskContext context, TaskState state)
    {
        return base.Available(context, state) &&
            context.PlayerChip <= _threshold;
    }
}

public abstract class ObjectiveTask : ConfiguredTask
{
    private readonly ObjectivePlan _plan;

    protected ObjectiveTask(Reward reward, ObjectivePlan plan) : base(reward)
    {
        _plan = plan;
    }

    public override ProgressChange Evaluate(TaskEvent fact, TaskState state)
    {
        // 消费服务先检查当前有效期；这里拒绝等待领奖或次数已耗尽。
        return state.CanAccumulate
            ? ObjectiveEvaluator.Evaluate(_plan, fact, state)
            : ProgressChange.None;
    }

    public override bool Available(TaskContext context, TaskState state)
    {
        // 已赚取的本轮奖励到期后仍可领；实际基础资格在领奖服务重查。
        return state.CompletedCount > state.ClaimedCount;
    }
}

public class CountHands : ObjectiveTask
{
    public CountHands(Reward reward, int target, params IHandCondition[] conditions)
        : base(reward, ObjectivePlan.CountDistinctHands(target, conditions))
    {
    }
}

public sealed class PlayHands : CountHands
{
    public PlayHands(Reward reward, int target)
        : base(reward, target, new HandSettledCondition())
    {
    }
}

public sealed class FormPattern : CountHands
{
    public FormPattern(Reward reward, TaskPattern pattern, int target)
        : base(reward, target, new PatternObservedCondition(pattern))
    {
    }
}

public sealed class WinHands : CountHands
{
    public WinHands(Reward reward, int target)
        : base(reward, target, new WonCondition(), new HandSettledCondition())
    {
    }
}

public sealed class WinWithPattern : CountHands
{
    public WinWithPattern(Reward reward, TaskPattern pattern, int target)
        : base(reward, target, new PatternObservedCondition(pattern),
            new WonCondition(), new HandSettledCondition())
    {
    }
}

public sealed class AllObjectives : ObjectiveTask
{
    public AllObjectives(Reward reward, params ObjectivePlan[] objectives)
        : base(reward, ObjectivePlan.All(objectives))
    {
    }
}
```

ObjectivePlan.CountDistinctHands 自动加真实参与检查，按条件派生事件时机并拒绝非法空条件；ObjectiveEvaluator 只提出进度变更，不自己提交事务、查询牌桌或写奖励。单手去重和跨轮禁止复用由统一任务消费者执行；TaskState.CanAccumulate 是“轮数未耗尽且无待领取轮次”，有效期由消费者按当前服务器时间检查。

AllObjectives 的子目标编译入口使用 CountDistinctHands + 目标数/条件，不嵌套奖励或 maxCompletions，也不开放任意层级 All/OR/NOT；代码工厂仍须限制第一版合法结构。

### 5.2 Available 与 ClaimReward 的边界

Available 是只读的领域领取资格，不预留次数、不加进度、不发钱；Evaluate 负责事件匹配。DirectClaim 的类规则不能让过期或次数耗尽的实例领取。玩家余额来自服务端 context，不来自配置或客户端传入的 objs。

真正的 ClaimReward 位于 TaskClaimService：按账号、实例、指定领取轮次和幂等请求，在事务内重查基础资格 + Available，读取 GetReward，原子修改领取次数、筹码、物品、账本，并在有效且还有轮数时初始化下一轮。超时重试仍针对同一轮，不领下一轮。

GameTask 及条件对象是不可变规则，可共享；TaskInstance 保存各玩家独立状态。type/params 只存在于加载器/编辑器，解析一次生成强类型字段；不用 object[] 承载每次运行时玩家数据。MaxCompletions、期限及每轮奖励均在分配实例时快照。

## 6. Reward 与加法

Reward 包含 Chip 和 Items。现有仓库 ItemInventory / ItemDefinitionSO 使用 string 物品 ID、uint 数量，建议沿用，不另引入 int ID 映射；筹码最终也需要与现有账户 uint 范围兼容。配置中负奖励直接拒绝，不依靠无符号转换绕过。

建议 readonly struct Reward + 不可变/防御性复制的 ItemReward 列表。struct 内放可变 List 仍会共享引用，不能以为 struct 已经保证独立。JSON 使用具名 itemId/count DTO，不依赖 ValueTuple 字段序列化。

Reward.operator + 的契约：
- 筹码相加，同 itemId 数量合并，不同 ID 保留。
- checked 检查筹码和物品数量溢出；失败不产生部分结果。
- 不修改两个输入对象的物品列表；加法产生独立结果，空奖励为单位元。
- 不在加法中修改玩家背包、检查 Available 或执行 SQL。
- 一个轮次的物品和筹码要么全部发放，要么全部不发；背包/物品不存在/数量范围出错，不能已经扣了领取次数却没到账。

完整奖励形状示例（这里只展示 DTO，物品 ID 必须替换成服务器物品目录中实际存在的 ID）：

```json
{
  "chip": 500,
  "items": [
    { "itemId": "example_item_id", "count": 2 }
  ]
}
```

将它放入 params[0] 即可；Reward 相加用于合并多项奖励，不代表默认一次领取所有剩余次数。第一版建议每个领取请求明确一轮；如果以后做“一键领取”，低余额型福利必须逐轮重查余额，不能一次 Available=true 后直接乘 3。

### 6.1 Reward 加法草图

下面复用仓库现有不可变 Game.ItemStack（ItemId/Quantity）；JSON 的 itemId/count DTO 在加载器转换，不直接序列化 tuple。代码需要 System、System.Collections.Generic、System.Linq；仍属于设计示例，未加入运行时代码。

```csharp
public readonly struct Reward
{
    public uint Chip { get; }
    public IReadOnlyList<Game.ItemStack> Items { get; }

    public Reward(uint chip, IEnumerable<Game.ItemStack> items = null)
    {
        Chip = chip;
        var snapshot = (items ?? Array.Empty<Game.ItemStack>()).ToArray();
        if (snapshot.Any(item => item == null || item.Quantity == 0))
        {
            throw new ArgumentException("Reward items must have positive quantities.");
        }
        Items = Array.AsReadOnly(snapshot);
    }

    public static Reward operator +(Reward left, Reward right)
    {
        var chip = checked(left.Chip + right.Chip);
        var counts = new Dictionary<string, uint>(StringComparer.Ordinal);
        var items = (left.Items ?? Array.Empty<Game.ItemStack>())
            .Concat(right.Items ?? Array.Empty<Game.ItemStack>());
        foreach (var item in items)
        {
            counts.TryGetValue(item.ItemId, out var previous);
            counts[item.ItemId] = checked(previous + item.Quantity);
        }

        return new Reward(chip, counts.Select(pair =>
            new Game.ItemStack(pair.Key, pair.Value)));
    }
}
```

构造时复制集合，Items 对外只读，ItemStack 本身不可变；默认 struct 的空 Items 在加法中作为空集合处理。物品 ID 是否存在、是否允许发放、最终余额/背包是否溢出由加载/发奖边界校验，不由加法执行外部查询。合并和持久化是不同职责，不能先给筹码再尝试给物品。

## 7. 重复任务特别容易漏掉的实现边界

- 去重范围不能只增加 RoundIndex 后就允许同一手重复使用。保留实例/目标/HandId 的消费标记跨轮存在；同一手可以推进不同任务，但不能完成当前实例的多个重复轮次。
- 建议在跨轮点记录已完成轮次涉及的 HandId；前轮使用过的手不再次投入新轮的其他子目标，防止同手不同事件（牌型/结算）跨轮复用。
- Claim 唯一键由原实例一次升级为 (AccountId, TaskInstanceId, CompletionOrdinal)。请求明确要领第几轮；重复/双击/提交后超时不能偷偷领下一轮。
- 两个并发事件把本轮推进到上限时，必须串行/事务去重：只完成一轮，另一个事件如在 WaitingForClaim 期间处理则忽略计数并保存消费结果，不偷跑下一轮。
- 完成次数和领奖次数一起检查，最多 MaxCompletions 次；客户端修改次数没有权限。
- DirectClaimWhenLesserThan 每一次领取都重新读取余额。余额 900、阈值 1000、每次给 2000，即使上限 3，领一次后余额 2900，第二次不满足；以后余额降下来且尚未到期，可再使用剩余次数。
- 已完成部分轮次但实例到期：保留已赚奖励，不能将整个实例连带所有待领奖删除。随机同定义新实例可与旧待领奖卡共存。
- 多轮期间修改配置、登录、跨日、重启不重置当前轮或已领次数；固定任务的下一业务日新实例按规则重置，不与旧奖励混用。
- 多子目标的早达标进度、当前轮归零动画、全部完成末轮满条与每实例至多一轮待领奖均需 UI 用例。
- 物品目录目前有独立变化；本次只设计 Reward 接口，不修改该模块或凭空声明已支持物品发奖。

## 8. 验收与文档范围

新增用例覆盖：固定三领；随机有效期共三轮；两条进度条、等待领奖不计入；跨轮同手去重；多子目标重置；到期部分已完成奖励；每轮幂等；余额逐次判断；params 类型与数量错误；物品合并/溢出/原子发放；重启与版本快照。

两项重复轮次产品选择已确认：未领奖不累计，全部轮次完成或到期释放名额。旧清单未标明重复次数的 C/G/E/R/P 场景默认 MaxCompletions=1；本轮追加 M 编号专门覆盖多轮。所有文档与代码片段都是设计，不代表已实现或测试通过。
