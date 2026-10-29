# Area Grid 当前工程说明

## 玩法与场景

- 游戏名称：Area Grid；公开开发者名称：lxlxlx。
- 共 32 关，采用双数字配对规则：每个矩形恰好包含两个数字，两数之和等于矩形面积；填满棋盘即可通关。
- 前三关为不限时教学关，提供手指演示和数字强调；跳过演示仍不限时。
- 第 4–32 关按难度设置 90、120、150、180 秒目标时间。时间耗尽显示 Overtime，可继续框选、撤销和解题，不判失败。
- 每关有一次 10 秒冻结，仅在目标时间尚未耗尽时可用；暂停时冻结也停止计时。
- 目标时间内通关显示 Quick finish，超时通关显示 Puzzle complete。
- 发布场景为 Assets/Scenes/Release.unity。UI、格子、矩形和动画数字已预放到场景层级；横竖屏布局仍会自动调整。
- 当前场景容量为 6×6、最多 8 对。超过容量时需增加预放对象。

## 打开和构建

- Unity 版本：2022.3.54f1c1。
- 在 Unity Hub 中打开本目录 D:\game-tool\shudu，再打开 Release 场景。
- 构建菜单：Area Grid → Build WebGL release。构建使用已经保存的 Release 场景，不会重新生成 UI。
- 正式 WebGL 输出到本工程 Builds/FirstRelease/webgl-release。旧 Builds 内容不会因工程源码同步而自动重建，发布前请重新构建。
- 命令行入口为 FirstReleaseBuild.Production、Development 或 Both；AREA_GRID_RELEASE_ROOT 可覆盖输出目录。
- WebGL 使用 Brotli 压缩。服务器须为 .br 返回 Content-Encoding: br，为 .wasm.br 返回 Content-Type: application/wasm；不能双击 index.html 运行。
- Assets/Editor/ReleaseSceneSetup.cs 中的 Create 和 Prepare 属于手动场景制作工具，日常构建不需要执行。

## 存档与资源

- PlayerPrefs 键：AreaGrid.Pairs.TwelveLevels.Save.v1，并保留 .backup 备份；键名沿用旧版以保持兼容。
- 保存关卡、矩形、剩余时间、冻结状态、教学与静音状态；超时存档可恢复后继续解题。
- 存档随浏览器站点数据保存，不支持跨设备同步；清理站点数据会清除进度。
- 字体及 Kenney 音效授权说明在 Assets/StreamingAssets。
- Development 构建包含 QA 调试入口，正式构建不包含；本次同步不执行上传或平台提交。

## 同步记录

2026-10-29：从已验证的工作副本同步源码、场景、素材和项目设置。未复制 Library、Temp、UserSettings、测试目录或旧构建产物。
