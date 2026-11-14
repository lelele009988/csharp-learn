# Git 入门笔记

> 2026-11-14 第一次把代码推上 GitHub 时整理的

---

## 一、Git 是什么

**带快照的时光机。**

```
PLC 程序改坏了 → 从备份恢复
上位机代码改坏了 → Git 让你回到任何一个存档点
```

比"复制一份改名"强在：

| 复制文件 | Git |
|---|---|
| `Program_v2.cs`、`Program_最终版.cs` | 一个文件，历史在版本库里 |
| 不知道改了什么 | `git diff` 精确到行 |
| 占空间 | 只存差异，几百 KB |

---

## 二、日常四步（背下来就够用）

```powershell
cd C:\Users\admin\Documents\deepseek-harness\default-workspace\csharp-learn

git status              # ① 我改了什么？
git add -A              # ② 加入待提交
git commit -m "说明"    # ③ 存一个档
git push                # ④ 推到 GitHub
```

### 每步在干什么

| 命令 | 干什么 | 工程类比 |
|---|---|---|
| `git status` | 看哪些文件变了 | 看 HMI 上的报警列表 |
| `git diff` | 看具体哪几行变了 | 看报警的详细信息 |
| `git add` | 挑出这次要存档的改动 | 勾选要记录的趋势曲线 |
| `git commit` | 本地存一个快照 | 按下"保存配方" |
| `git push` | 上传到云端 | 把配方同步到服务器 |

---

## 三、怎么读 `git diff`

```diff
- 环境：.NET SDK 10.0.401 …VS 需要修复
+ 环境：.NET SDK 10.0.403 …VS 2022 17.14（已重装）
```

| 符号 | 含义 |
|---|---|
| `-` 减号 | 删掉的行（旧内容） |
| `+` 加号 | 新增的行（新内容） |
| `@@ -2,7 +2,8 @@` | 旧文件第 2 行起 7 行，新文件第 2 行起 8 行 |

> **这是排错利器。** 现场调试比"我昨天改了什么来着"强一万倍。

---

## 四、在 VS 2022 里怎么用

```
1. 打开 csharp-learn 里的任意 .csproj
2. 看底部状态栏 —— 应该显示 "main"
3. 改了代码后 → 右侧「Git 更改」窗口
4. 写提交信息 → 点「全部提交」
5. 右上角「推送」
```

---

## 五、几个关键概念

| 概念 | 说明 |
|---|---|
| **提交（commit）** | 一个快照，有唯一编号（如 `4003c1f`） |
| **分支（branch）** | 一条独立的开发线，默认叫 `main` |
| **远程（remote）** | 云端仓库，这里叫 `origin` |
| **.gitignore** | 告诉 Git"这些文件不要管"（bin、obj 等） |

---

## 六、出事怎么救

```powershell
git log --oneline              # 看所有存档点
git diff                       # 看改了什么但还没提交
git restore 文件名              # 撤销未提交的改动
git checkout 提交号 -- 文件名   # 从某个存档点恢复单个文件
```

---

## 七、这个仓库

```
远程：  git@github.com:lelele009988/csharp-learn.git
协议：  SSH（走 443 端口，绕开网络干扰）
分支：  main
```
