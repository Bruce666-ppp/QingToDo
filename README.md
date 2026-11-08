# QingToDo · 轻待办

简约的 Windows 桌面待办小窗：三条事项、手动排序、柔和提醒、本地保存。

**[项目网站与下载](https://bruce666-ppp.github.io/QingToDo/)** · **[使用指南](https://bruce666-ppp.github.io/QingToDo/guide/)**

## 使用

从项目网站下载一键安装包，解压并双击“一键安装.exe”。安装器内置完整程序，会创建桌面入口并打开应用；若旧版正在运行，请先从托盘右键完全退出，再重试安装。旧版待办会在本机迁移，已有安装数据优先保留。

- 可选时间、提前提醒分钟数和提醒方式。
- 拖动点阵手柄调序，浮起、让位、落位动画提供反馈。
- 小窗显示前三条未完成事项，完成后自动补位。
- 最小化或收至托盘继续提醒，轻闪三次、无声音。
- 可设置开机自动打开、置顶和桌面快捷方式。

需要 Windows 10 / 11 及系统的 .NET Framework 4.x。完全退出、关机或休眠期间不能实时提醒，重新运行或唤醒后补提醒一次。

## 项目结构

```text
app/源码/                 C# / WPF 完整源码、测试、图标生成器、安装器
app/使用说明.md           桌面程序使用指南
site/                     GitHub Pages 静态网站与下载文件
scripts/build-windows.ps1  构建程序、检查并生成安装包
scripts/check-site.mjs     页面、路径、资源及下载文件检查
scripts/check-repository.mjs 仓库文件与敏感信息检查
.github/workflows/        Windows 构建及 GitHub Pages 发布流程
```

## 本地构建与预览

在 Windows PowerShell 中执行（无需 npm 依赖）：

```powershell
powershell -File scripts/build-windows.ps1
node scripts/check-repository.mjs
node scripts/check-site.mjs
node scripts/serve.mjs
```

打开终端打印的本地网址。预览服务器同时支持 `/` 和 `/QingToDo/`，可以验证项目子路径下的首页、指南直接打开及刷新。

构建会执行 47 项应用检查和 16 项安装逻辑检查。生成物暂存在 `.build/`，最新的 EXE 与 ZIP 复制到 `site/downloads/`。

## GitHub Pages 与以后更新

仓库唯一发布目标为 `https://github.com/Bruce666-ppp/QingToDo.git`，默认分支 `main`。

首次发布在仓库 **Settings → Pages → Source** 选择 **GitHub Actions**。之后每次提交到 `main`，Actions 会在 Windows 构建并检查应用与安装器，再在 Linux 发布静态网站。

以后在这个文件夹修改应用源码或 `site/` 页面；如修改了应用，先运行上面的构建命令更新下载文件，再执行：

```powershell
git remote -v
git add app site scripts .github docs README.md .gitignore .gitattributes
git commit -m "Describe the update"
git push origin main
```

等待仓库 Actions 中的 **Build and publish QingToDo** 成功，网站和下载链接就会更新。第一次在新设备推送时，按 GitHub 提示登录。不要提交 `data`、`.env`、密钥、依赖目录或个人待办。

网站为静态项目介绍与下载页，不在网页中收集待办。反馈通过本仓库 Issues 提交。
