# Real Ruins 变更日志

## 2026-11-13（4）
- DefInjected 汉化写入（+51 键）：拾荒心情 5 阶段（10 键）、TrippingTrigger.label（"粗糙的地面"，伪装风格）、遗迹扫描 RuinsScanDef/JobDef/WorkGiverDef（14 键）、扫描遗迹任务 QuestScriptDef（27 键）。`RaidTrigger.label` 保留用户旧译"遗迹袭击标志"未动。备份：`docs/_lang_backup/definjected-20261113/`。
- 术语："智力"→"智识"（4 处，对齐原版），提交 `12a7be7`。
- 已知待办：扫描任务名规则（questNameRules）在抽象基类，需 PatchOperation 补丁（草稿在审核文件末尾）。

## 2026-11-13（3）
- 应用用户审核定稿到 Keyed（远行队/行星遗迹/遗迹任务等术语统一，约 80 条润色），同步提交 `fc71be0`。
- 术语分工定稿："原始遗迹任务"仅作任务名（事件开关、说明文案），其余场景用"遗迹任务"；`CaravanReformType` 改为"在遗迹重组远行队"。
- 新增 DefInjected 审核文件（`docs/DefInjected_审核版/`，按最终目录结构拆分，每条带英文原文注释）：拾荒心情 5 阶段、TrippingTrigger.label、遗迹扫描（RuinsScanDef/JobDef/WorkGiverDef）、扫描遗迹任务 QuestScriptDef（约 27 条随机文案）。提交 `c86d3e5`。
- 已知待办：任务名规则（questNameRules）在抽象基类里，DefInjected 无法翻译，定稿后需写 PatchOperation 补丁。

## 2026-11-13（2）
- 生成翻译审核版 `docs/RealRuins_Options_翻译审核版.xml`：335 键全量、按英文文件分节，每条中文上方以 `EN:` 注释附英文原文；`\n` 字面量与 `&lt;` 等转义保留原样，审核定稿后可直接替换 Keyed 文件。
- 备份当前翻译到 `docs/_lang_backup/RealRuins_Options.xml.20261113-full.bak`。提交 `aa71497`。

## 2026-11-13
- 汉化：`Languages/ChineseSimplified/Keyed/RealRuins_Options.xml` 补全 183 个缺失键（英文 335 键现已全覆盖），包括：
  - 整页缺失：预设（Presets）、财富与平衡（Wealth and balance）、玩法（Gameplay）、科技等级限制（TechLimit）、遗迹扫描（RuinsScan）、更新说明窗口（WhatsNew）、开局地块被占（FactionOwnedStartTile）、行星蓝图加载器大部分文案、设置导入导出。
  - 零星缺失：财富上限系列（WealthPerMap 等 8 键）、行星缓存详情（Seed/Coverage 等）、POI12–15（村庄/城堡/工坊/飞船残骸）、调试设置补充 10 键、事件开关 8 键。
- 修正 17 条过时/错译（如 DisableHostilesTT 原译"禁用敌对"、CaravanReformTooltip 乱码、ForceMultiplier/AbsoluteWealthCap 语义与新版英文不符、CostLimitTT 过时等）。
- 备份：改动前的原文件存于 `docs/_lang_backup/RealRuins_Options.xml.20261113.bak`。
- 校验：XML 可解析、335 键无重复；与英文键集逐一比对无缺失。提交 `535d36b`。
