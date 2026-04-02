# Unity MCP 调试记录

记录 unity-mcp（CoplayDev/unity-mcp）与 Claude Code VSCode 扩展的连接配置过程。

## 最终可用配置

```json
// .mcp.json（放在项目根目录，如 BountyHunter/ 或 KartingMicrogame/）
{
  "mcpServers": {
    "unityMCP": {
      "type": "stdio",
      "command": "C:/Users/A/AppData/Local/Programs/Python/Python313/Scripts/uv.exe",
      "args": ["run", "--directory", "D:/Computer/unity-mcp/Server", "python", "src/main.py", "--transport", "stdio"]
    }
  }
}
```

Unity MCP 窗口设置：`Window > MCP for Unity` → Transport 选 **Stdio** → 点击 **Start Session**

---

## 问题排查流程

### 问题 1：OAuth 错误（HTTP 模式）

**现象**：`/mcp` 面板显示：
```
SDK auth failed: HTTP 502 trying to load OAuth metadata from http://localhost:8080/.well-known/oauth-authorization-server
```

**原因**：Claude Code VSCode 扩展对 `type: "http"` 的 MCP 服务器强制要求 OAuth 认证流程。FastMCP 3.x 虽然能响应 GET/POST `/mcp`（HTTP 200 OK），但不提供 OAuth metadata endpoint，导致认证失败。

**错误配置**：
```json
{
  "mcpServers": {
    "unityMCP": {
      "type": "http",
      "url": "http://localhost:8080/mcp"
    }
  }
}
```

**解决**：改用 stdio 模式（见上方配置），Claude Code 直接通过 stdin/stdout 与 Python 进程通信，绕过 OAuth。

---

### 问题 2：Server type "undefined" does not support OAuth

**现象**：改为 stdio 后仍然报：
```
Server type "undefined" does not support OAuth authentication
```

**原因**：stdio 模式需要显式声明 `"type": "stdio"`，否则 Claude Code 无法识别服务器类型。

**修复**：在 `.mcp.json` 的服务器配置中加上 `"type": "stdio"`。

---

### 问题 3：Unity instance count = 0（stdio 模式找不到 Unity）

**现象**：MCP 连接成功，但工具调用返回：
```
No Unity Editor instances found. Please ensure Unity is running with MCP for Unity bridge.
```

**原因**：stdio 模式与 HTTP 模式的连接架构不同：

| 模式 | Unity 连接方向 | Unity 端 |
|------|--------------|---------|
| HTTP | Unity → Python 服务器（WebSocket `/hub/plugin`） | 无需 TCP 监听 |
| Stdio | Python 服务器 → Unity（TCP 端口 6400） | 需要 TCP 监听器 |

Unity 的 `StdioBridgeHost.cs`（`[InitializeOnLoad]`）中：
```csharp
private static bool ShouldAutoStartBridge()
{
    bool useHttpTransport = EditorConfigurationCache.Instance.UseHttpTransport;
    return !useHttpTransport;  // HTTP 模式时 TCP bridge 不启动
}
```

之前点击过"Start Server"（HTTP 模式），导致 `UseHttpTransport = true`，TCP bridge 不会自动启动。

**修复**：
1. 在 Unity `Window > MCP for Unity` 中，将 Transport 下拉菜单从 **HTTPLocal** 改为 **Stdio**
2. 点击 **Start Session**，启动端口 6400 的 TCP 监听器
3. Python stdio 服务器重连后即可发现 Unity 实例

---

## 架构说明

```
┌─────────────────────────────────────────────────┐
│  HTTP 模式（需要 OAuth，Claude Code 不兼容）       │
│                                                   │
│  Claude Code ──HTTP──→ Python Server (8080)       │
│  Unity ──WebSocket /hub/plugin──→ Python Server   │
│                                                   │
│  Unity 需点击 "Start Server" 启动 Python 进程      │
└─────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────┐
│  Stdio 模式（Claude Code 推荐，无需 OAuth）        │
│                                                   │
│  Claude Code ──spawn──→ Python Server (stdio)     │
│  Python Server ──TCP 6400──→ Unity               │
│                                                   │
│  Unity 需将 Transport 设为 Stdio 并 Start Session │
│  Python 进程由 Claude Code 通过 uv run 自动管理   │
└─────────────────────────────────────────────────┘
```

---

## 依赖安装

unity-mcp 本地安装路径：`D:/Computer/unity-mcp/Server/`

首次运行 `uv run` 时会自动创建 `.venv` 并安装所有依赖（约 75 个包）。
如果 pip 下载超时（中国网络），配置阿里云镜像：

```toml
# C:\Users\A\AppData\Roaming\uv\uv.toml
[pip]
index-url = "https://mirrors.aliyun.com/pypi/simple/"
```

---

## 每次使用流程

1. 打开 Unity 项目（KartingMicrogame）
2. `Window > MCP for Unity` → Transport = **Stdio** → **Start Session**（显示绿点 Connected）
3. 在 VSCode 中打开 BountyHunter 或 KartingMicrogame 文件夹
4. Claude Code 对话自动加载 `.mcp.json`，stdio 服务器在后台启动
5. 输入 `/mcp` 确认 unityMCP 显示绿色已连接
