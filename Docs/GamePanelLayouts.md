# GamePanel 三档玩家名片

更新：2026-09-07。

三份独立 prefab 按同步状态中 IsOccupied 的人数选档，空座位和旁观者不计入。跨档重建实例，同档复用；列表仍按服务器座位索引映射。跨档保留当前行动玩家的剩余时间。

| 占座人数 | prefab（Assets/Prefabs/UI/Panels/GamePanel/） | 排列 | 比例 | 1920×1080 下的名片尺寸 | 横／纵间距 |
| --- | --- | --- | --- | --- | --- |
| 1–4 | Player4.prefab | 1 列 × 最多 4 行 | 8:3 | 480×180 | 8 / 8 |
| 5–8 | Player8.prefab | 2 列 × 最多 4 行 | 4:3 | 236×177 | 8 / 8 |
| 9–16 | Player16.prefab | 2 列 × 最多 8 行 | 8:3 | 236×88.5 | 8 / 8 |

三档使用相同宽度的玩家栏，切换时右侧牌桌无需横向跳动。PlayerListGrid 根据可用宽度及选中 prefab 的宽高比计算高度，窗口尺寸变化后仍保持指定比例。根节点不使用 AspectRatioFitter，避免与网格重复驱动尺寸。超过 16 人继续使用紧凑档，由原 ScrollView 处理溢出；没有更改房间人数限制。

## 共用状态区域

NamecardContent/StatusSlot 是倒计时与行动结果的共用区域，Time、Operation 是同位置的两个分支：

- 轮到玩家时显示金色“思考 30s”，隐藏所有旧行动；右上角留给姓名和身份标记。
- 服务器结束该玩家的行动回合后，隐藏倒计时，显示最近行动及金额。
- 没有行动记录且未轮到该玩家时，两者均不显示。
- 本地计时归零仍显示“思考 0s”，等待服务器同步后切换；不会自行显示上一轮操作。

PlayerListItem.RefreshStatus 统一处理可见性，更新 Operation 与 IsOperating 的先后顺序不会造成两者重叠。倒计时沿用已有中文 UI 字体。4 人和 16 人档使用两行信息，筹码与共用状态区位于下行；8 人档为姓名、筹码、共用状态区三行。

## Inspector 调整位置

每档可分别调整以下内容：

- 根 RectTransform：按该档要求保持 8:3 或 4:3，宽高成对调整；网格会读取该比例。
- PlayerListItem / List Spacing：名片间距；Folded Alpha 为弃牌透明度，当前设为 0.6。
- NamecardContent/PlayerInfo/PlayerName、PlayerChips：姓名、筹码的字号和位置。
- NamecardContent/Icons：庄家、盲注、断线标记。
- NamecardContent/StatusSlot：共用区域位置；内部 Time 和 Operation 分别调整倒计时与行动字号。
- NamecardSlot/NamecardArtwork：独立美术槽，由既有 NamecardAppearance 替换图片。
- GamePanel / Table Layout：玩家栏右边界、牌桌左边界及区域引用。当前单列与双列使用相同边界。

| 档位 | 姓名字号 | 筹码字号 | 行动字号 | 倒计时字号 |
| --- | --- | --- | --- | --- |
| 4 人 | 28 | 28 | 20 | 24 |
| 8 人 | 26 | 26 | 20 | 24 |
| 16 人 | 23 | 21 | 18 | 20 |

字号为 Canvas 参考分辨率下的默认值。长姓名允许省略；金额保留项目既有格式，仅在字段不足时按 prefab 下限缩小。16 人档已检查 uint 最大金额，筹码及行动金额均完整显示。

## 名片接口与美术

保留 SetNamecard(string id) 和 SetNamecard(NamecardProductSO product)。SO 入口取商品 ID 后调用既有目录和美术组件。默认／空 ID 沿用经典底色，本任务未修改共享 NamecardAppearance、名片 SO、纹理或导入规范。

美术规格沿用原图 2048×768、Unity 最大 1024×384，64 px 装饰边界和 96 px 文字安全区。NamecardContent 保持横向 4.6875%、纵向 12.5% 内缩。4 人／16 人容器与美术均为 8:3；8 人容器按要求为 4:3，素材槽的适配仍可独立编辑。

## 验证

- Unity 2022.3.52f1c1 编译成功。
- GamePanelLayoutTest + UIInteractionTest + UINavigationTest：85/85，通过；作业 1036dc4cf1b94c56b9808e8173886ea0。
- 覆盖人数边界、空座位、双向跨档、计时保留、实际网格比例，以及三档中六种行动与倒计时的互斥显示、空操作、等待服务器结束回合、行动金额更新。
- Game.unity Play Mode 查看三档 1920×1080 和 16 人 1366×768；16 人全部可见。检查长姓名、最大金额和倒计时中文显示；运行时确认倒计时能切换为加注结果。
- GamePanel 和三份新 prefab 无 Missing Script／Missing Reference，运行时场景对象扫描也未发现 Missing Script。编译重载期间曾出现无对象信息的 Missing Script 日志；保留日志后清空 Console，再验证返回标题、退出 Play Mode、重导入和资源引用，最后 error 查询为 0。
- 返回标题后名片停止行动状态；最终编辑器退出 Play Mode，Game View 恢复 1920×1080，Game.unity 没有保存修改。
- 截图采用本地构造的展示状态；未重跑真实服务器连接、入房、开局与断线全流程，未构建，也未验证其他名片素材的最终外观。

## 实际截图

[4 人](GamePanelLayouts/players-4-shared-countdown.png) · [8 人](GamePanelLayouts/players-8-shared-final.png) · [16 人](GamePanelLayouts/players-16-shared-final.png) · [16 人，1366×768](GamePanelLayouts/players-16-shared-1366.png)

![16 人默认名片](GamePanelLayouts/players-16-shared-final.png)
