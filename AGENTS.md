# 项目规则

## 通用规则
1. 用户可能会开启 Unity/游戏实例预览和测试；如果请求开启实例被拒绝，允许先强制关闭占用实例，再继续编辑/编译。
2. `.\space-build\models` 用于存放游戏需要用到的模型；`.\space-build\map` 用于存放地图图层。

## oh-my-codex-compat 默认规则

对于以下任务，默认使用 `oh-my-codex-compat` workflow：

- 需求不明确的功能开发
- 中大型重构
- 多步骤调试
- 代码审查
- 需要先规划再执行的任务

执行要求：

- 优先通过仓库探索获取事实，必要时再澄清需求
- 非简单任务默认先给出简要计划，再实施
- 完成前必须做验证，并说明主要风险或剩余限制
- 保持 Desktop-first，不要假设 `omx`、`tmux`、HUD、pane orchestration、`.omx` runtime state 可用
- 若任务明确依赖 OMX runtime，需说明该能力仅支持外部 CLI fallback，不在 Codex Desktop 内直接执行

优先级：

1. 当前项目已有 AGENTS 规则优先
2. `oh-my-codex-compat` 作为补充工作流约束生效
3. OMX CLI runtime 相关能力仅作为人工触发的备用路径



# 环境:

## WSL:

```text
IP: 172.19.247.204
account: jinghongjie
password: 123456
os: ubuntu 23
```


