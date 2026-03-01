# GravityShift 逐关人工回归脚本清单

适用范围: `Assets/Scripts` 当前实现（含最近触发器与重生稳态修复）。
目标: 逐关验证“可通过路径 + 防卡关 + 触发可靠性”，把人工回归跑全。

## 0. 执行前准备

- [ ] 使用 Unity Play 模式，确保场景由 `GameDirector` 自动启动。
- [ ] 打开 Console，`Clear On Play` 关闭，便于保留报错。
- [ ] 每次开局记录 HUD: `Need X`、`Flip CD`、`Checkpoints`。
- [ ] 基础按键确认: `WASD`、`Space`、`F`、`R`、`Esc`、`N`、`O`。
- [ ] 运行中如果出现卡死或无法推进，先按 `R` 重开本局复现一次，再判定为缺陷。

## 1. 全量执行矩阵（24 局）

说明: 每一项至少“完整通关 1 次 + 中途死亡重生 1 次”。

- [ ] L1 Easy Adventure
- [ ] L1 Normal Adventure
- [ ] L1 Hard Adventure
- [ ] L1 Easy Challenge
- [ ] L1 Normal Challenge
- [ ] L1 Hard Challenge
- [ ] L2 Easy Adventure
- [ ] L2 Normal Adventure
- [ ] L2 Hard Adventure
- [ ] L2 Easy Challenge
- [ ] L2 Normal Challenge
- [ ] L2 Hard Challenge
- [ ] L3 Easy Adventure
- [ ] L3 Normal Adventure
- [ ] L3 Hard Adventure
- [ ] L3 Easy Challenge
- [ ] L3 Normal Challenge
- [ ] L3 Hard Challenge
- [ ] L4 Easy Adventure
- [ ] L4 Normal Adventure
- [ ] L4 Hard Adventure
- [ ] L4 Easy Challenge
- [ ] L4 Normal Challenge
- [ ] L4 Hard Challenge

## 2. 通用通过判定（每局都要过）

- [ ] 没有 `Exception`、`NullReference`、`MissingReference`、断言错误。
- [ ] Console 无 `LevelBuilder ... missing wiring` 类告警（Pressure/Laser/Chain/Rhythm）。
- [ ] `F` 翻转在非 Anchor 区可用，在 Anchor 区禁用，离开后恢复。
- [ ] 至少触发 1 次死亡重生，重生后可继续推进，不会丢失关键交互。
- [ ] 任意关键门（Local Gate / Pressure Gate / Rhythm Gate / Laser Gate）不会出现“提示已开但实际阻挡”。
- [ ] 收集晶体计数始终递增，不出现重复计数或无法计数。
- [ ] 满足全局晶体需求后，Energy Gate 打开，Exit 可结算。

## 3. L1 逐项脚本（五区教学流）

### 3.1 主路径

- [ ] 区域 1: 完成基础移动、跳跃、翻转到天花板练习垫并返回地面。
- [ ] 区域 2: 收集本地区晶体并打开 `Zone2 Gate`（需求 2，实际放置 3）。
- [ ] 区域 3: 在倒置状态只能站在 `StickySurface`，踩到非粘附面会滑落。
- [ ] 区域 4: 压板门可通过箱子触发，也可玩家站压板触发（防软锁兜底）。
- [ ] 区域 5: 先开本地区门，再压板开门，穿过高低阻挡后回主路。
- [ ] 收集足够全局晶体后，最终 Energy Gate 打开并完成结算。

### 3.2 防卡关回归

- [ ] 把箱子推离可用区域后，玩家站压板仍能推进。
- [ ] 在 Sticky 区死亡重生，规则不残留，仍可正常再解。
- [ ] 在 Gravity Anchor 边缘死亡重生后，不出现永久锁翻转。

## 4. L2 逐项脚本（进阶综合流）

### 4.1 主路径

- [ ] 通过基础 Laser Cage 与浮空平台段。
- [ ] 完成链路机制 `A -> B`（B 初始锁定，A 后解锁 B）。
- [ ] 通过弹板天空段 `MegaBounce_A -> SkyRoute -> MegaBounce_B -> 落地回路`。
- [ ] 完成节奏段: 触发 A，过 Gate A，再触发 B，过 Gate B。
- [ ] 通过旋转障碍走廊（Rotor Corridor）回到主路线。
- [ ] 晶体达标后开终点门并通关。

### 4.2 防卡关回归

- [ ] 在节奏段触发区内等待（不反复进出），不会出现开关误连发。
- [ ] B 开关在锁定时进入触发区，A 解锁后无需额外异常操作即可正常触发。
- [ ] 弹板高速掠过时不会漏弹导致必经路径断档。

## 5. L3 逐项脚本（高压挑战流）

### 5.1 主路径

- [ ] 完成 L2 全流程能力点。
- [ ] 节奏段包含更高压窗口与附加移动平台，仍可完整通过。
- [ ] 旋转走廊更密集（含额外危险臂），路线可读且可稳定通过。
- [ ] 晶体达标后能在时限内到达终点。

### 5.2 防卡关回归

- [ ] 至少 2 次中途死亡后仍可完成，不出现状态错乱。
- [ ] 重生后节奏链会正确重置，不出现“门状态与 HUD 不一致”。

## 6. L4 逐项脚本（Boss 复合机制）

### 6.1 主路径

- [ ] 完成前段挑战后进入 Boss 区。
- [ ] Boss 链要求先 A 后 B，B 在 A 前必须保持锁定。
- [ ] A 成功后，B 可触发，Gate A/B 对应开启。
- [ ] Boss Anchor 区内翻转禁用，离开区后恢复。
- [ ] 完成 Boss 段后接终点门并结算。

### 6.2 防卡关回归

- [ ] Boss 区内死亡重生后，链路状态可重新执行，不会永久锁死。
- [ ] Anchor 区边缘反复进出，不出现“离开后仍锁翻转”。

## 7. 专项稳定性回归（每个大版本至少一次）

- [ ] 开关触发可靠性: `OnTriggerEnter` 丢失时，`OnTriggerStay` 能兜底成功。
- [ ] 防重复触发: 同一次进入区间不应重复激活开关/弹板。
- [ ] 锁定开关后进入触发区，解锁后不需离开区域也可触发一次。
- [ ] 链路重置后（如死亡重生），同一开关可再次正常触发。
- [ ] 压板占用体清理: 箱子/玩家异常移除后，压板状态可自动恢复。
- [ ] KillZone / Checkpoint / Crystal 在高速移动下不漏触发。
- [ ] 多重 KillZone 重叠时，单次坠落只计一次死亡并只触发一次重生。
- [ ] 坍塌平台在踩中时必定进入倒计时，不出现“站上去不坍塌”。 
- [ ] 压板门初始化时序: 关卡加载后门状态应与压板状态一致，不应因组件创建顺序误开门。
- [ ] 重力翻转姿态稳定: 翻转后角色视觉不应长时间“侧倒/趴倒”，应快速恢复直立。
- [ ] 移动平台承载清理: 死亡重生或脱离平台后，不会被平台继续“远程拖拽”。
- [ ] 在 Anchor/Sticky 区域内重生时，区域规则应立即生效，不应出现短暂失效窗口。

## 8. 单局记录模板

- [ ] 局信息: Level= , Difficulty= , Mode=
- [ ] HUD 需求: Need= , FlipCD= , Checkpoints=
- [ ] 通关结果: Pass/Fail
- [ ] 失败点位置: 
- [ ] 是否可重现: Always / Sometimes / Once
- [ ] Console 报错关键字: 
- [ ] 备注与截图编号: 
