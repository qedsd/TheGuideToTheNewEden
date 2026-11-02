# 更新发布工具（DevTools）

`make-update-package.ps1` 的 WPF 前端：不用记命令行参数，`notes` 可以直接多行编辑。

## 运行

```powershell
dotnet run --project .\TheGuideToTheNewEden\TheGuideToTheNewEden.DevTools.UpdatePublisher\TheGuideToTheNewEden.DevTools.UpdatePublisher.csproj
```

或在 Visual Studio 里把 `TheGuideToTheNewEden.DevTools.UpdatePublisher` 设为启动项目。

工具会从自身所在目录向上查找 `tools\packaging\make-update-package.ps1`；找不到时窗口顶部会提示。

## 界面

- **版本**：留空 = 读取 csproj 的 `VersionPrefix` / `VersionSuffix`；
- **Tag**：留空 = `v版本号`；
- **输出目录**：留空 = `<仓库根>\update-package`；
- **分片大小**：默认 90 MB（Gitee 单附件上限 100 MB）；
- **Gitee 地址前缀**：留空 = `https://gitee.com/<Gitee 仓库>/releases/download/<Tag>`；
- **更新内容 notes**：多行文本，生成时写入 `update.json` 的 `notes`；
- **指定 exe**：填写后自动跳过编译；
- 按钮：
  - 一键发布并生成：`dotnet publish` + 完整 exe + Gitee 分片 + `update.json`；
  - 仅生成：跳过编译，直接用已有 exe；
  - 停止：结束整个脚本进程树；
  - 打开发布目录 / 打开 update.json。

参数会保存到：

```
%LocalAppData%\TheGuideToTheNewEden\DevTools\update-publisher.json
```

## 说明

- notes 通过临时 `.md`（UTF-8 无 BOM）传给脚本的 `-NotesFile`，因此可以正常换行，不受命令行引号限制。
- 脚本输出以 UTF-8 实时显示在下方日志区。
- 本工具不替代发布：生成后的 exe/分片/`update.json` 仍需按文档上传到 GitHub / Gitee。
