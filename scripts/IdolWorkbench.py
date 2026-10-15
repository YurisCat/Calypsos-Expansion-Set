"""Local, file-backed idol planning workbench. Uses only Python's standard library."""

from __future__ import annotations

import argparse
import hashlib
import http.client
import json
import os
from pathlib import Path
import re
import secrets
import threading
import webbrowser
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit


DEFAULT_FOLDER = "人类工作间/接收文件/雕像需求对齐_20261013_v1"
MAX_BODY = 1024 * 1024


def stamp():
    return datetime.now(timezone.utc).isoformat()


def digest(value):
    return hashlib.sha256(value).hexdigest()


def encoded(value):
    return json.dumps(value, ensure_ascii=False, indent=2).encode("utf-8")


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def uncomment(text):
    return re.sub(r"/\*.*?\*/|//[^\n]*", "", text, flags=re.S)


class Workbench:
    def __init__(self, root, folder):
        self.root = root.resolve(strict=True)
        self.folder = folder.resolve(strict=True)
        if not self.folder.is_relative_to(self.root):
            raise ValueError("工作台目录必须在项目中。")
        self.data_file = self.folder / "需求数据.json"
        self.progress_file = self.folder / "需求进度.json"
        self.lock = threading.RLock()
        self.token = secrets.token_urlsafe(32)
        self.project_id = digest(str(self.root).encode("utf-8"))[:16]
        self.cached = None
        self.cached_signature = None

    def data(self):
        raw = self.data_file.read_bytes()
        data = json.loads(raw.decode("utf-8-sig"))
        if data.get("schema") != "ces-idol-planning-v1" or not isinstance(data.get("items"), list):
            raise ValueError("需求数据.json 的 schema 或 items 无效。")
        ids = set()
        for item in data["items"]:
            if type(item.get("id")) is not int or item["id"] in ids:
                raise ValueError("雕像编号无效或重复。")
            ids.add(item["id"])
            labels = set()
            for question in item["questions"]:
                if question["label"] in labels or not isinstance(question["options"], list):
                    raise ValueError("问题标签重复或选项无效。")
                labels.add(question["label"])
        return data, digest(raw)

    def progress(self):
        if not self.progress_file.exists():
            return {"schema": "ces-idol-progress-v2", "updatedAt": None, "decisions": []}, "empty"
        raw = self.progress_file.read_bytes()
        data = json.loads(raw.decode("utf-8-sig"))
        if data.get("schema") != "ces-idol-progress-v2":
            raise ValueError("需求进度.json 格式无效；保留原文件，请先修复或导入备份。")
        self.validate_progress(data, allow_historical=True)
        return data, digest(raw)

    def validate_progress(self, progress, allow_historical=False):
        if not isinstance(progress, dict) or progress.get("schema") != "ces-idol-progress-v2":
            raise ValueError("不支持的进度格式。")
        decisions = progress.get("decisions")
        if not isinstance(decisions, list) or len(decisions) > 512:
            raise ValueError("进度条目无效或超过上限。")
        current, _ = self.data()
        current_ids = {item["id"] for item in current["items"]}
        ids = set()
        for decision in decisions:
            if not isinstance(decision, dict):
                raise ValueError("进度条目必须是对象。")
            item_id = decision.get("id")
            if type(item_id) is not int or item_id in ids:
                raise ValueError("进度编号无效或重复。")
            if not allow_historical and item_id not in current_ids:
                raise ValueError("存在未知雕像编号。")
            ids.add(item_id)
            answers = decision.get("answers")
            if not isinstance(answers, dict) or len(answers) > 128:
                raise ValueError("答案必须按问题标签保存。")
            for label, value in answers.items():
                if not isinstance(label, str) or len(label) > 200 or not isinstance(value, str) or len(value) > 2000:
                    raise ValueError("答案格式或长度无效。")
            if not isinstance(decision.get("notes"), str) or len(decision["notes"]) > 10000:
                raise ValueError("备注格式无效或超过 10000 字。")
            if type(decision.get("reviewed")) is not bool or type(decision.get("firstBatch")) is not bool:
                raise ValueError("确认与优先级必须是布尔值。")

    def source_paths(self):
        return [
            self.root / "CalypsoExpansionSetPlugin.cs",
            self.root / "TODO.md",
            self.root / "docs/ARCHITECTURE.md",
            self.root / "CHANGELOG.md",
            self.root / "DEVELOPMENT.md",
            *sorted((self.root / "Idols").glob("*.cs")),
            *sorted((self.root / "Assets/StickyNote/Previews").glob("*.png")),
        ]

    def signature(self):
        paths = [self.data_file, self.progress_file, *self.source_paths()]
        return tuple((str(path), path.stat().st_mtime_ns, path.stat().st_size) if path.exists()
                     else (str(path), None, None) for path in paths)

    def sources(self):
        plugin = uncomment((self.root / "CalypsoExpansionSetPlugin.cs").read_text(encoding="utf-8-sig"))
        version_match = re.search(r'PluginVersion\s*=\s*"([^"]+)"', plugin)
        if not version_match:
            raise ValueError("未能读取插件版本。")
        enabled_classes = set(re.findall(r"Registry\s*\.\s*Register\s*\(\s*(\w+)\s*\.\s*Definition\s*\)", plugin))
        definitions = {}
        for path in sorted((self.root / "Idols").glob("*.cs")):
            text = uncomment(path.read_text(encoding="utf-8-sig"))
            definition = re.search(
                r'Definition\s*=>\s*new\s*\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,'
                r'\s*(\w+)\s*,\s*(0x[0-9A-Fa-f]+)[Uu]?\s*,', text)
            if not definition:
                continue
            key, display_name, config, asset_id = definition.groups()
            content_version = re.search(r"ContentVersion\s*=\s*(\d+)", text)
            advice = re.search(r'ShortDescription\s*=\s*"([^"]*)"', text)
            artwork = re.search(
                r'(?:StickyNoteVisuals\.Create|ScreenEffectResources\.Create|HeldIdolAudio\.CreateAttached)'
                r'\(\s*prefab\s*,\s*"([a-z][a-z0-9_]{0,63})"', text)
            if not config.lower().startswith("0x"):
                constant = re.search(r"const\s+ulong\s+" + re.escape(config) + r"\s*=\s*(0x[0-9A-Fa-f]+)", text)
                if not constant:
                    raise ValueError(f"无法读取 {path.name} 的配置 ID。")
                config = constant[1]
            else:
                config = re.sub(r"[UuLl]+$", "", config)
            definitions[key] = {
                "key": key, "displayName": display_name,
                "enabled": path.stem in enabled_classes,
                "source": path.relative_to(self.root).as_posix(),
                "contentVersion": int(content_version[1]) if content_version else None,
                "configId": config,
                "assetId": asset_id,
                "advice": advice[1] if advice else "",
                "artworkKey": artwork[1] if artwork else None,
            }
        parsed_classes = {Path(d["source"]).stem for d in definitions.values()}
        if enabled_classes - parsed_classes:
            raise ValueError("未能解析已注册类型：" + "、".join(sorted(enabled_classes - parsed_classes)))
        return version_match[1], definitions

    def wiki(self):
        """Read-only learning catalog; keep decisions and private notes out of it."""
        with self.lock:
            data, _ = self.data()
            version, definitions = self.sources()
            planning = {item.get("key"): item for item in data["items"]}
            entries = []
            for key, definition in definitions.items():
                # Include paused, documented content; omit unimplemented ideas and
                # the retired spring prototype, which has no current catalog row.
                item = planning.get(key)
                if item is None and not definition["enabled"]:
                    continue
                artwork_key = definition["artworkKey"]
                image = self.root / "Assets/StickyNote/Previews" / f"{artwork_key}.png"
                image_revision = digest(image.read_bytes())[:16] if artwork_key and image.is_file() else None
                entries.append({
                    "key": key, "name": definition["displayName"],
                    "enabled": definition["enabled"], "advice": definition["advice"],
                    "category": item.get("category", "其他") if item else "其他",
                    "description": item.get("wikiSummary", item["summary"]) if item else "玩法说明待补充。",
                    "image": f"/wiki/notes/{artwork_key}.png?v={image_revision}" if image_revision else None,
                })
            entries.sort(key=lambda entry: (not entry["enabled"], planning.get(entry["key"], {}).get("id", 999)))
            result = {"schema": "ces-idol-wiki-v1", "version": version, "items": entries}
            result["revision"] = digest(encoded(result))
            return result

    def snapshot(self):
        with self.lock:
            before = self.signature()
            if self.cached is not None and before == self.cached_signature:
                return self.cached
            data, data_revision = self.data()
            progress, progress_revision = self.progress()
            version, definitions = self.sources()
            docs = {}
            for name in ["TODO.md", "docs/ARCHITECTURE.md", "CHANGELOG.md", "DEVELOPMENT.md"]:
                text = (self.root / name).read_text(encoding="utf-8-sig")
                # Full source documents stay on disk; expose relevant current sections.
                if name == "TODO.md":
                    excerpt = text
                elif name == "docs/ARCHITECTURE.md":
                    match = re.search(r"## 内置雕像\n(.*?)(?=\n## |\Z)", text, re.S)
                    excerpt = match[1].strip() if match else text[:12000]
                else:
                    excerpt = text[:16000]
                docs[name] = excerpt
            for item in data["items"]:
                definition = definitions.get(item.get("key"))
                item["sourceDefinition"] = definition
                item["done"] = bool(definition and definition["enabled"])
                item["implementationStatus"] = (
                    "已实现 · 已启用" if item["done"] else
                    "已暂停注册" if definition else "待实现"
                )
                if item["done"]:
                    item["wave"] = 0
            data["baseline"] = version
            data["enabledDefinitions"] = [d for d in definitions.values() if d["enabled"]]
            data["retiredDefinitions"] = [d for d in definitions.values() if not d["enabled"]]
            data["projectDocs"] = docs
            result = {
                "schema": "ces-workbench-snapshot-v1",
                "projectId": self.project_id,
                "token": self.token,
                "data": data, "progress": progress,
                "dataRevision": data_revision, "progressRevision": progress_revision,
            }
            result["revision"] = digest(encoded(result))
            after = self.signature()
            if before != after:
                # Never cache a mixture from an editor writing multiple files.
                raise ValueError("项目文件正在更新，稍后自动重试。")
            self.cached, self.cached_signature = result, after
            return result

    def save(self, raw):
        with self.lock:
            _, data_revision = self.data()
            _, progress_revision = self.progress()
            if raw.get("baseProgressRevision") != progress_revision or raw.get("dataRevision") != data_revision:
                return None
            progress = raw.get("progress", {})
            self.validate_progress(progress)
            # Keep historical rows if a later catalog removes an item.
            old_progress, _ = self.progress()
            current_ids = {item["id"] for item in self.data()[0]["items"]}
            historical = [d for d in old_progress["decisions"] if d["id"] not in current_ids]
            progress = {
                "schema": "ces-idol-progress-v2", "updatedAt": stamp(),
                "decisions": progress["decisions"] + historical,
            }
            payload = encoded(progress) + b"\n"
            # Back up the exact previous progress before atomically replacing it.
            if self.progress_file.exists():
                backup_dir = self.folder / "进度备份"
                backup_dir.mkdir(exist_ok=True)
                backup = backup_dir / f"进度_{datetime.now():%Y%m%d_%H%M%S_%f}.json"
                with backup.open("xb") as stream:
                    stream.write(self.progress_file.read_bytes())
            temporary = self.progress_file.with_name(f".需求进度.{secrets.token_hex(8)}.tmp")
            with temporary.open("xb") as stream:
                stream.write(payload)
                stream.flush()
                os.fsync(stream.fileno())
            os.replace(temporary, self.progress_file)
            self.cached_signature = None
            return self.snapshot()


def make_handler(workbench):
    class Handler(BaseHTTPRequestHandler):
        protocol_version = "HTTP/1.1"

        def log_message(self, *args):
            pass

        def send_bytes(self, status, body, content_type="application/json; charset=utf-8", extra=None):
            self.send_response(status)
            self.send_header("Content-Type", content_type)
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-store")
            self.send_header("X-Content-Type-Options", "nosniff")
            self.send_header("Referrer-Policy", "no-referrer")
            self.send_header("Content-Security-Policy",
                             "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; "
                             "img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'")
            for key, value in (extra or {}).items():
                self.send_header(key, value)
            self.end_headers()
            if body:
                self.wfile.write(body)

        def json_response(self, status, body, extra=None):
            self.send_bytes(status, encoded(body), extra=extra)

        def trusted_host(self):
            return self.headers.get("Host") == f"127.0.0.1:{self.server.server_port}"

        def do_GET(self):
            if not self.trusted_host():
                return self.json_response(403, {"error": "仅支持本机固定地址。"})
            path = urlsplit(self.path).path
            try:
                if path == "/api/health":
                    return self.json_response(200, {
                        "app": "ces-idol-workbench", "protocol": 1, "projectId": workbench.project_id,
                        "pid": os.getpid(),
                    })
                if path == "/api/snapshot":
                    snapshot = workbench.snapshot()
                    etag = '"' + snapshot["revision"] + '"'
                    if self.headers.get("If-None-Match") == etag:
                        return self.send_bytes(304, b"", extra={"ETag": etag})
                    return self.json_response(200, snapshot, {"ETag": etag})
                if path == "/api/wiki":
                    catalog = workbench.wiki()
                    etag = '"' + catalog["revision"] + '"'
                    if self.headers.get("If-None-Match") == etag:
                        return self.send_bytes(304, b"", extra={"ETag": etag})
                    return self.json_response(200, catalog, {"ETag": etag})
                if path == "/api/legacy":
                    return self.json_response(200, read_json(workbench.folder / "需求数据_原版20261013.json"))
                routes = {
                    "/": (workbench.folder / "index.html", "text/html; charset=utf-8"),
                    "/index.html": (workbench.folder / "index.html", "text/html; charset=utf-8"),
                    "/app.js": (workbench.folder / "app.js", "text/javascript; charset=utf-8"),
                    "/favicon.ico": (None, ""),
                    "/wiki": (workbench.root / "scripts/workbench/wiki.html", "text/html; charset=utf-8"),
                    "/wiki/": (workbench.root / "scripts/workbench/wiki.html", "text/html; charset=utf-8"),
                    "/wiki/app.js": (workbench.root / "scripts/workbench/wiki.js", "text/javascript; charset=utf-8"),
                    "/wiki/style.css": (workbench.root / "scripts/workbench/wiki.css", "text/css; charset=utf-8"),
                }
                if path.startswith("/wiki/notes/"):
                    # Only files actually referenced by the catalog are public.
                    allowed = {urlsplit(item["image"]).path for item in workbench.wiki()["items"] if item["image"]}
                    if path in allowed:
                        file = workbench.root / "Assets/StickyNote/Previews" / path.rsplit("/", 1)[1]
                        return self.send_bytes(200, file.read_bytes(), "image/png")
                if path not in routes:
                    return self.json_response(404, {"error": "页面不存在。"})
                file, mime = routes[path]
                if file is None:
                    return self.send_bytes(204, b"")
                return self.send_bytes(200, file.read_bytes(), mime)
            except (OSError, ValueError, KeyError, TypeError) as error:
                return self.json_response(503, {"error": f"读取项目失败：{error}"})

        def do_POST(self):
            origin = f"http://127.0.0.1:{self.server.server_port}"
            self.close_connection = True
            if (not self.trusted_host() or self.headers.get("Origin") != origin
                    or self.headers.get("X-Workbench-Token") != workbench.token):
                return self.json_response(403, {"error": "请求来源无效。"})
            if urlsplit(self.path).path != "/api/stop":
                return self.json_response(404, {"error": "入口不存在。"})
            self.json_response(200, {"stopped": True})
            threading.Thread(target=self.server.shutdown, daemon=True).start()

        def do_PUT(self):
            origin = f"http://127.0.0.1:{self.server.server_port}"
            if (not self.trusted_host() or self.headers.get("Origin") != origin
                    or self.headers.get("X-Workbench-Token") != workbench.token):
                self.close_connection = True
                return self.json_response(403, {"error": "保存请求来源无效。"})
            if urlsplit(self.path).path != "/api/progress":
                self.close_connection = True
                return self.json_response(404, {"error": "保存入口不存在。"})
            try:
                length = int(self.headers.get("Content-Length", "0"))
                if not 0 < length <= MAX_BODY or "application/json" not in self.headers.get("Content-Type", ""):
                    self.close_connection = True
                    return self.json_response(400, {"error": "进度文件格式或大小无效。"})
                raw = json.loads(self.rfile.read(length).decode("utf-8"))
                if not isinstance(raw, dict):
                    raise ValueError("保存内容必须是对象。")
                snapshot = workbench.save(raw)
                if snapshot is None:
                    return self.json_response(409, {"error": "文件已被更新，请保留本页草稿并载入最新进度。"})
                return self.json_response(200, snapshot)
            except (ValueError, TypeError, KeyError) as error:
                return self.json_response(400, {"error": str(error)})
            except OSError as error:
                return self.json_response(503, {"error": f"写入失败，草稿保留在浏览器：{error}"})

    return Handler


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--folder", type=Path)
    parser.add_argument("--port", type=int, default=18764)
    parser.add_argument("--open", action="store_true", help="Open the workbench in the default browser.")
    parser.add_argument("--stop", action="store_true", help="Stop this project's running workbench.")
    args = parser.parse_args()
    workbench = Workbench(args.root, args.folder or args.root / DEFAULT_FOLDER)
    url = f"http://127.0.0.1:{args.port}/"
    # A desktop shortcut can be clicked repeatedly without starting duplicate services.
    connection = http.client.HTTPConnection("127.0.0.1", args.port, timeout=2)
    existing = None
    try:
        connection.request("GET", "/api/health")
        response = connection.getresponse()
        existing = json.loads(response.read()) if response.status == 200 else None
    except (OSError, ValueError, http.client.HTTPException):
        pass
    finally:
        connection.close()
    if existing:
        if (existing.get("app") != "ces-idol-workbench" or existing.get("protocol") != 1
                or existing.get("projectId") != workbench.project_id):
            raise ValueError(f"端口 {args.port} 被其他服务占用，请换端口启动。")
        if args.stop:
            connection = http.client.HTTPConnection("127.0.0.1", args.port, timeout=5)
            try:
                connection.request("GET", "/api/snapshot")
                response = connection.getresponse()
                if response.status != 200:
                    raise ValueError("工作台数据读取失败，无法取得关闭凭据。")
                token = json.loads(response.read())["token"]
                connection.request("POST", "/api/stop", headers={
                    "Origin": url.rstrip("/"), "X-Workbench-Token": token, "Content-Length": "0",
                })
                response = connection.getresponse()
                response.read()
                if response.status != 200:
                    raise ValueError("当前后台不支持关闭，请检查是否为旧版服务。")
            finally:
                connection.close()
            return
        if args.open:
            webbrowser.open(url)
        return
    if args.stop:
        return
    workbench.snapshot()  # Fail visibly before binding if project inputs are invalid.
    server = ThreadingHTTPServer(("127.0.0.1", args.port), make_handler(workbench))
    server.daemon_threads = True
    if args.open:
        # Bind first, then open. serve_forever starts before the browser finishes navigation.
        webbrowser.open(url)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        import sys
        if "--open" in sys.argv and os.name == "nt":
            import ctypes
            ctypes.windll.user32.MessageBoxW(
                None, f"工作台启动失败：{error}\n请检查项目路径、需求数据与本机端口。",
                "Calypso's Expansion Set · 雕像工作台", 0x10,
            )
        else:
            raise
