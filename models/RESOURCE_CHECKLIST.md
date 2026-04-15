# 3D 资源采集清单（持续导入）

目标：保证项目在 Unity 中稳定实现 `角色移动 + 地图加载`，并为后续 `物品摆放` 预留可扩展模型目录。

## P0 必需（优先补齐）

- [ ] 玩家角色主模型（Humanoid，T-Pose）
  - 推荐路径：`models/player/player_main.fbx`
- [ ] 玩家动画（至少 `idle/walk/run/jump`）
  - 推荐路径：`models/player/anims/player_idle.fbx` 等
- [ ] 树模型 3-5 款
  - 推荐路径：`models/scene/tree/tree_oak_01.fbx` 等
- [ ] 石头/岩石模型 3-5 款
  - 推荐路径：`models/scene/stone/rock_granite_01.fbx` 等
- [ ] 地形/地表相关网格至少 1 套
  - 推荐路径：`models/scene/terrain/terrain_chunk_01.fbx`
- [ ] 地标模型至少 1-3 个（门/塔/废墟/巨石等）
  - 推荐路径：`models/scene/landmark/ruin_gate_01.fbx`

## P1 建议（增强地图观感）

- [ ] 灌木/草丛 5-10 款（低模）
  - 推荐路径：`models/scene/props/bush_01.fbx`
- [ ] 地面道具 10+（木箱/桶/路牌/碎石等）
  - 推荐路径：`models/scene/props/crate_01.fbx`
- [ ] 建筑基础件（后续摆放）
  - `models/building/floor/`
  - `models/building/wall/`
  - `models/building/stairs/`
  - `models/building/pillar/`
  - `models/building/roof/`

## P2 后续（摆放玩法扩展）

- [ ] 功能摆放物（篝火/工作台/储物箱/围栏/灯）
  - `models/building/utility/`
- [ ] 互动道具（装饰/摆设）
  - `models/building/props/`
- [ ] 可采集资源节点（矿石/树桩/草药）
  - `models/scene/props/`

## 规格约束（避免导入返工）

- 坐标尺度：`1 Unity Unit = 1m`
- 玩家高度：建议 `1.7m - 1.9m`
- 格式：角色/动画优先 `FBX`，场景支持 `FBX/GLB`
- 贴图：与模型同目录存放 `PNG/TGA`，减少材质丢失
- 面数建议：可交互物件 `< 20k tris`，小道具 `< 5k tris`

## 每批导入后的固定验证

1. 执行：`Tools/Project Bootstrap/Run Full Bootstrap`
2. 打开：`Assets/Scenes/BootstrapWorld.unity`
3. Play 检查：
   - `WASD + Space` 角色移动/跳跃
   - 场景地图可见（树、石头、地标至少一个）
   - 物品摆放系统正常（放置/旋转/删除）
4. 记录失败模型：贴图缺失、比例错误、碰撞异常
