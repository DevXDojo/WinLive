<div align="center">
  <img src="imgs/icon-large.png" alt="WinLive logo" style="height: 80px;"/>
  
  WinLive 是一个 Windows 本地图片库，支持 iPhone/iPad 导出的 HEIC+MOV Live Photos。

  <p>
   <a href="README.md">English</a> | <strong>简体中文</strong>
  </p>

  [![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)](https://github.com/DevXDojo/WinLive/releases) [![License](https://img.shields.io/badge/license-GPLv3-green.svg)](LICENSE)
</div>

## 🚀 快速开始

[![下载链接](imgs/badge-cn.svg)](https://apps.microsoft.com/detail/9NXZW4PLMG80)

![og](imgs/og.png)

## 🛠️ 开发指南

安装 .NET 8 SDK 和 Windows App SDK 工作负载，然后运行：

```powershell
dotnet restore WinLive.sln
dotnet test WinLive.sln
dotnet run --project .\src\WinLive\WinLive.csproj
```

开发命令会直接运行一个未打包的自包含可执行程序，不会安装任何内容。仅在需要生成 MSIX 时运行

```powershell
dotnet build .\src\WinLive\WinLive.csproj -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true
```

经过检查的开发流程会在构建完成后，使用本地自签名证书对生成的安装包进行签名。在安装该安装包之前，需要先将 `WinLive-Development.cer` 安装到 `Cert:\CurrentUser\TrustedPeople\` 中。

## 🤝 贡献

我们欢迎贡献！详情请参阅我们的[贡献指南](CONTRIBUTING.md)。

<details>

<summary>点击展开贡献指南</summary>

<div markdown="1">

在贡献之前：

1. 阅读[行为准则](CODE_OF_CONDUCT.md)
2. 检查现有 issue 或创建一个新 issue
3. Fork 仓库并创建功能分支
4. 进行更改并添加测试
5. 提交 Pull Request

</div>

</details>

## 🔒 安全

如果您发现安全漏洞，请遵循我们的[安全策略](SECURITY.md)。

## 📝 许可证

本项目采用 GPL-3.0 许可证 - 详情请参阅 [LICENSE](LICENSE) 文件。

WinLive 的发行版本包含来自 [FFmpeg](https://ffmpeg.org/) 项目的动态链接库。当前使用的 `FFmpeg.LGPL` 包声明这些库采用 LGPL-3.0-or-later 许可证；FFmpeg 及其库的版权仍归各自的版权持有人所有。详情请参阅 [FFmpeg 许可与法律事项](https://ffmpeg.org/legal.html)。

## 📮 联系与支持

- **Issues**: [GitHub Issues](https://github.com/DevXDojo/WinLive/issues)
- **讨论**: [GitHub Discussions](https://github.com/DevXDojo/WinLive/discussions)
- **仓库**: [github.com/DevXDojo/WinLive](https://github.com/DevXDojo/WinLive)

<div align="center">
  <img src="imgs/sponsor.png" alt="Sponsor WinLive"/>
  <p>Made with ❤️ by the WinLive Team</p>
</div>
