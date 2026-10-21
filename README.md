# 奶蛙桌面宠物

奶蛙是一个运行在 Windows 桌面上的 3D 宠物。双击 `release/奶蛙.exe` 后，它会从屏幕右下角出现。

## 交互

- 左键单击：随机播放一个动作（跳跃、招手、前倾、伸懒腰、吐舌、眨眼、转圈），动作会平滑过渡，保持几秒后再平滑回到静止状态。
- 左键拖动：拖动奶蛙到桌面任意位置。
- 右键单击或左键双击：打开控制面板。
- 控制面板：调整大小（最大为屏幕高度的一半），以及“上移 / 下移”切换奶蛙是否置顶显示。
- 静止状态：奶蛙会随机正面、背面或侧面对着你，但双眼始终看向屏幕前的你。

## 技术说明

奶蛙使用 WPF 3D 构建，模型由程序化生成的身体、头部、眼睛、瞳孔、四肢和舌头网格组成，动画采用分层变换和 smoothstep 插值，运行时不依赖云端服务。

最初计划调研并调用图片转 3D 与运动重定向工具，例如：

- [Tencent-Hunyuan/Hunyuan3D-2](https://github.com/Tencent-Hunyuan/Hunyuan3D-2)
- [microsoft/TRELLIS](https://github.com/microsoft/TRELLIS)
- [microsoft/TRELLIS.2](https://github.com/microsoft/TRELLIS.2)
- [stabilityai/stable-fast-3d](https://github.com/Stability-AI/stable-fast-3d)
- [MaxLiu22/Motion2MixamoRig](https://github.com/MaxLiu22/Motion2MixamoRig)

执行环境无法直连 GitHub、Hugging Face 和 npm registry，因此本次采用了本机可验证的 WPF 3D 程序化建模方案。图片被用于提取奶蛙配色并生成 exe 图标。

## 构建

需要 Windows 和 .NET Framework 4.x。在 PowerShell 中执行：

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' `
  /nologo /target:winexe /platform:anycpu /codepage:65001 `
  /win32icon:'build\app.ico' `
  /out:'release\奶蛙.exe' `
  /r:'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll' `
  /r:'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll' `
  /r:'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll' `
  /r:'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll' `
  src\NaiWa.cs
```

图标生成脚本为 `build/make_icon.ps1`，会从参考图片中裁出奶蛙上半身并生成 `app.ico`。
