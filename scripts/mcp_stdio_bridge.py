#!/usr/bin/env python3
"""Vex MCP stdio 桥接：让标准 MCP 客户端（stdio 传输）连接 Vex 的本机 HTTP MCP 服务。

用法：
  1. 在 Vex 帮助菜单 -> MCP 设置 中启用 MCP 服务；
  2. 配置环境变量（可选，均有默认值）：
       VEX_MCP_URL   默认 http://127.0.0.1:17891/mcp/
       VEX_MCP_TOKEN Vex 生成的授权 Token（必填，否则服务端返回 401）
  3. 在 MCP 客户端中把本脚本注册为 stdio 服务器，例如（Claude Desktop / Cursor 等）：
       "command": "python",
       "args": ["<仓库>/scripts/mcp_stdio_bridge.py"],
       "env": { "VEX_MCP_TOKEN": "<你的 Token>" }

协议说明：Vex 的 MCP 端点是 MCP Streamable HTTP（官方 C# SDK，会话由服务端分配）。
本桥从 stdin 逐行读取 JSON-RPC（单请求或批量数组），转发到 Vex，再把响应按行写回
stdout；通知类消息（无 id）不产生响应。initialize 之后本桥携带服务端分配的
Mcp-Session-Id 与协商出的 MCP-Protocol-Version 继续通信。
"""
import json
import os
import sys
import urllib.error
import urllib.request

URL = os.environ.get("VEX_MCP_URL", "http://127.0.0.1:17891/mcp/")
TOKEN = os.environ.get("VEX_MCP_TOKEN", "")

SESSION = {"id": None, "protocol": None}


def post(payload):
    body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    headers = {
        "Content-Type": "application/json; charset=utf-8",
        "Accept": "application/json, text/event-stream",
        "Authorization": "Bearer " + TOKEN,
    }
    is_initialize = isinstance(payload, dict) and payload.get("method") == "initialize"
    if SESSION["id"]:
        headers["Mcp-Session-Id"] = SESSION["id"]
    if SESSION["protocol"] and not is_initialize:
        headers["MCP-Protocol-Version"] = SESSION["protocol"]
    req = urllib.request.Request(URL, data=body, headers=headers, method="POST")
    with urllib.request.urlopen(req, timeout=120) as resp:
        data = resp.read()
        session_id = resp.headers.get("Mcp-Session-Id")
        content_type = resp.headers.get("Content-Type", "")
    if session_id:
        SESSION["id"] = session_id
    if not data.strip():
        return None

    text = data.decode("utf-8")
    if "text/event-stream" in content_type:
        # Streamable HTTP 以 SSE 帧回包：event: message / data: {...}
        messages = []
        for line in text.splitlines():
            if line.startswith("data:"):
                try:
                    messages.append(json.loads(line[len("data:"):].strip()))
                except json.JSONDecodeError:
                    pass
        if not messages:
            return None
        if isinstance(payload, dict) and "id" in payload:
            for message in messages:
                if message.get("id") == payload["id"]:
                    return message
        return messages[0]

    message = json.loads(text)
    if is_initialize and isinstance(message, dict):
        result = message.get("result")
        if isinstance(result, dict) and result.get("protocolVersion"):
            SESSION["protocol"] = result["protocolVersion"]
    return message


def handle(line):
    line = line.strip()
    if not line:
        return
    try:
        payload = json.loads(line)
    except json.JSONDecodeError as exc:
        print(json.dumps({"jsonrpc": "2.0", "id": None, "error": {
            "code": -32700, "message": f"parse error: {exc}"}}), flush=True)
        return

    if isinstance(payload, list):
        responses = [r for item in payload if (r := safe_call(item)) is not None]
        if responses:
            print(json.dumps(responses, ensure_ascii=False), flush=True)
        return

    response = safe_call(payload)
    if response is not None:
        print(json.dumps(response, ensure_ascii=False), flush=True)


def safe_call(payload):
    if not isinstance(payload, dict):
        return {"jsonrpc": "2.0", "id": None, "error": {"code": -32600, "message": "invalid request"}}
    if "id" not in payload or payload.get("method", "").startswith("notifications/"):
        post_quiet(payload)  # 通知也要转发给 Vex（如 notifications/initialized）
        return None
    try:
        return post(payload)
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        return {"jsonrpc": "2.0", "id": payload.get("id"), "error": {
            "code": -32000, "message": f"HTTP {exc.code}: {detail}"}}
    except Exception as exc:  # 连接失败等
        return {"jsonrpc": "2.0", "id": payload.get("id"), "error": {
            "code": -32001, "message": f"bridge error: {exc}"}}


def post_quiet(payload):
    try:
        post(payload)
    except Exception:
        pass


def main():
    if not TOKEN:
        print(json.dumps({"jsonrpc": "2.0", "id": None, "error": {
            "code": -32002, "message": "VEX_MCP_TOKEN is not set"}}), flush=True)
    for line in sys.stdin:
        handle(line)


if __name__ == "__main__":
    main()
