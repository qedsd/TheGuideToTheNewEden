#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""从 ESI 官方 OpenAPI 规格生成 AI 助手「ESI 全量网关」的操作索引。

用法（仓库根目录下执行）：
    python tools/esi/gen_esi_index.py                     # 联网取最新规格并生成
    python tools/esi/gen_esi_index.py --spec spec.json    # 用本地已下载的规格（离线）
    python tools/esi/gen_esi_index.py --check             # 只打印统计，不写文件

输出：TheGuideToTheNewEden/TheGuideToTheNewEden.WPF/Resources/Esi/EsiOperations.json
（随项目以 Content 复制到输出目录，运行时由 EsiOperationIndex 读取。）

为什么要这份索引（而不是反射 EVEStandard）：
    PointyHatGames.EVEStandard 4.1.0 的包内**没有 XML 文档**，DLL 里也没有路由模板 /
    权限特性（实测：`characters/{characterId}/wallet` 这类字符串不存在），
    反射只能拿到方法名与参数名，**拿不到 path / HTTP 动词 / scope / 说明**——
    而模型要可靠地挑端点、拼参数，这四样缺一不可。故元数据一律取自官方规格。

裁剪规则（都是为了"给模型看"而不是"存档"）：
    * 丢 `X-Compatibility-Date` / `X-Tenant` / `If-None-Match` / `If-Modified-Since`
      四个 header：前者固定值反而会把服务端行为钉死在过去，租户（服务器）由 base URL 决定，
      后两个是缓存协商（网关不做条件请求）。**请求时不发 X-Compatibility-Date，用服务端默认（最新）**。
    * `Accept-Language` 记为 `language: true`（网关按应用语言自动补 zh/en），不占参数表。
    * 只保留摘要（summary）与参数/字段名 + 类型，不保留长 description 与响应 schema
      （响应是 JSON，网关侧统一整形，不需要声明）。
    * 显式标出写操作：非 GET 且**不在**查询型 POST 白名单里 → `write: true`。
      ESI 有 8 个"POST 但只读"的端点（批量查询），若按动词一刀切，模型查个价格也要用户点确认。
"""

import argparse
import datetime
import json
import os
import sys
import urllib.request

SPEC_URL = "https://esi.evetech.net/meta/openapi.json"

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_OUT = os.path.join(
    REPO_ROOT, "TheGuideToTheNewEden", "TheGuideToTheNewEden.WPF",
    "Resources", "Esi", "EsiOperations.json")

# 查询型 POST：动词是 POST，语义是"批量查询"。不列入写操作。
READ_ONLY_POST_PATHS = {
    "/characters/affiliation",
    "/characters/{character_id}/assets/locations",
    "/characters/{character_id}/assets/names",
    "/characters/{character_id}/cspa",
    "/corporations/{corporation_id}/assets/locations",
    "/corporations/{corporation_id}/assets/names",
    "/universe/ids",
    "/universe/names",
}

DROP_HEADERS = {"X-Compatibility-Date", "X-Tenant", "If-None-Match", "If-Modified-Since"}
LANGUAGE_HEADER = "Accept-Language"

SUMMARY_LIMIT = 200
DESC_LIMIT = 110
MAX_ENUM_VALUES = 16
MAX_BODY_PROPS = 16
MAX_BODY_ITEM_PROPS = 16  # 数组元素的字段（如邮件收件人）

VERBS = ("get", "post", "put", "delete", "patch")


# ----------------------------------------------------------------------
#  规格解析
# ----------------------------------------------------------------------

def load_spec(src):
    if src.startswith("http://") or src.startswith("https://"):
        req = urllib.request.Request(src, headers={"User-Agent": "TheGuideToTheNewEden/esi-index"})
        with urllib.request.urlopen(req, timeout=90) as resp:
            return json.loads(resp.read().decode("utf-8"))
    with open(src, "r", encoding="utf-8") as handle:
        return json.load(handle)


def resolve(node, spec, depth=0):
    """展开 $ref（只跟链条，不复制整棵 schema 树）。"""
    while isinstance(node, dict) and "$ref" in node and depth < 8:
        ref = node["$ref"]
        if not ref.startswith("#/"):
            return {}
        target = spec
        for part in ref[2:].split("/"):
            target = target.get(part, {}) if isinstance(target, dict) else {}
        node = target
        depth += 1
    return node if isinstance(node, dict) else {}


def type_name(schema):
    kind = schema.get("type")
    if isinstance(kind, list):
        kind = next((k for k in kind if k != "null"), None)
    if kind:
        return kind
    if "properties" in schema:
        return "object"
    if "items" in schema:
        return "array"
    if "enum" in schema:
        return "string"
    return "string"


def enum_values(schema):
    values = schema.get("enum")
    if not isinstance(values, list) or not values:
        return None
    if len(values) > MAX_ENUM_VALUES:
        return None
    out = []
    for value in values:
        if isinstance(value, (str, int, float, bool)):
            out.append(value)
    return out or None


def clip(text, limit):
    if not isinstance(text, str):
        return None
    text = " ".join(text.split())
    if not text:
        return None
    return text if len(text) <= limit else text[: limit - 1] + "…"


def param_entry(param, spec):
    """参数条目。注意：类型/枚举写在参数的 `schema` 子节点里（OpenAPI 3），
    直接对参数对象取 `type` 会一律得到 "string"（本轮实测踩过：page 被标成字符串）。"""
    schema = resolve(param.get("schema") or {}, spec)
    if not schema:
        schema = param  # OpenAPI 2 风格（schema 内联在参数上）
    entry = {"name": param.get("name"), "type": type_name(schema)}
    if param.get("required"):
        entry["required"] = True
    values = enum_values(schema)
    if values:
        entry["enum"] = values
    desc = clip(param.get("description") or schema.get("description"), DESC_LIMIT)
    if desc:
        entry["desc"] = desc
    if schema.get("default") is not None and not values:
        entry["default"] = schema["default"]
    if schema.get("minimum") is not None:
        entry["min"] = schema["minimum"]
    if schema.get("maximum") is not None:
        entry["max"] = schema["maximum"]
    return entry


def body_summary(schema, spec):
    """请求体摘要：顶层字段 + 数组元素字段（模型要靠它构造 body）。"""
    schema = resolve(schema, spec)
    if not schema:
        return None
    kind = type_name(schema)
    summary = {"type": kind}
    desc = clip(schema.get("description"), DESC_LIMIT)
    if desc:
        summary["desc"] = desc

    if kind == "array":
        items = resolve(schema.get("items", {}), spec)
        summary["item"] = type_name(items)
        props = props_of(items, spec, MAX_BODY_ITEM_PROPS)
        if props:
            summary["itemProps"] = props
        if schema.get("minItems") is not None:
            summary["minItems"] = schema["minItems"]
        if schema.get("maxItems") is not None:
            summary["maxItems"] = schema["maxItems"]
    elif kind == "object":
        props = props_of(schema, spec, MAX_BODY_PROPS)
        if props:
            summary["props"] = props

    return summary


def props_of(schema, spec, limit):
    props = schema.get("properties")
    if not isinstance(props, dict) or not props:
        return None
    required = set(schema.get("required") or [])
    out = []
    for name, raw in list(props.items())[:limit]:
        child = resolve(raw, spec)
        entry = {"name": name, "type": type_name(child)}
        if name in required:
            entry["required"] = True
        if entry["type"] == "array":
            item = resolve(child.get("items", {}), spec)
            entry["item"] = type_name(item)
        values = enum_values(child)
        if values:
            entry["enum"] = values
        desc = clip(child.get("description"), DESC_LIMIT)
        if desc:
            entry["desc"] = desc
        out.append(entry)
    return out


def security_scopes(operation):
    scopes = []
    for requirement in operation.get("security") or []:
        if isinstance(requirement, dict):
            for value in requirement.values():
                if isinstance(value, list):
                    scopes.extend(v for v in value if isinstance(v, str))
    # 去重且保序
    seen = set()
    out = []
    for scope in scopes:
        if scope not in seen:
            seen.add(scope)
            out.append(scope)
    return out


def build(spec):
    paths = spec.get("paths") or {}
    operations = []
    for path, item in paths.items():
        if not isinstance(item, dict):
            continue
        for verb in VERBS:
            operation = item.get(verb)
            if not isinstance(operation, dict):
                continue

            path_params, query_params = [], []
            language = False
            for raw in operation.get("parameters") or []:
                param = resolve(raw, spec)
                where = param.get("in")
                if where == "header":
                    if param.get("name") == LANGUAGE_HEADER:
                        language = True
                    continue  # 其余 header 全部丢弃（见文件头说明）
                if where == "path":
                    path_params.append(param_entry(param, spec))
                elif where == "query":
                    query_params.append(param_entry(param, spec))

            body = None
            request_body = operation.get("requestBody")
            if isinstance(request_body, dict):
                content = request_body.get("content") or {}
                media = content.get("application/json") or {}
                body = body_summary(media.get("schema") or {}, spec)
                if body is not None and request_body.get("required"):
                    body["required"] = True

            scopes = security_scopes(operation)
            is_write = verb != "get" and path not in READ_ONLY_POST_PATHS

            operations.append({
                "id": operation.get("operationId"),
                "verb": verb.upper(),
                "path": path,
                "summary": clip(operation.get("summary") or operation.get("description"), SUMMARY_LIMIT) or "",
                "tag": (operation.get("tags") or [None])[0],
                "scopes": scopes,
                "auth": bool(scopes),
                "corp": path.startswith("/corporations/"),
                "pathParams": path_params,
                "query": query_params,
                "body": body,
                "language": language,
                "pageable": any(p.get("name") == "page" for p in query_params),
                "write": is_write,
            })

    operations.sort(key=lambda op: (op["verb"], op["path"]))
    return {
        "source": SPEC_URL,
        "generatedAt": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "specVersion": (spec.get("info") or {}).get("version"),
        "specTitle": (spec.get("info") or {}).get("title"),
        "operationCount": len(operations),
        "readOnlyPosts": sorted(READ_ONLY_POST_PATHS),
        "operations": operations,
    }


# ----------------------------------------------------------------------
#  自检与输出
# ----------------------------------------------------------------------

def self_check(index):
    """索引自身的完整性约束——探针之外的第二道闸（脚本每次跑都会执行）。"""
    problems = []
    ids = set()
    for op in index["operations"]:
        if not op["id"]:
            problems.append("存在没有 operationId 的操作：" + op["path"])
        if op["id"] in ids:
            problems.append("operationId 重复：" + str(op["id"]))
        ids.add(op["id"])
        if not op["path"].startswith("/"):
            problems.append("path 不以 / 开头：" + op["path"])
        if op["write"] and op["verb"] == "GET":
            problems.append("GET 被标成写操作：" + op["path"])
        if not op["write"] and op["verb"] in ("POST", "PUT", "DELETE") \
                and op["path"] not in READ_ONLY_POST_PATHS:
            problems.append("非白名单写操作未被标记：" + op["verb"] + " " + op["path"])
        for param in op["query"]:
            if param["name"] in ("datasource", "compatibility_date"):
                problems.append("残留服务端参数：" + param["name"] + " @ " + op["path"])
            # 类型必须来自参数的 schema 子节点：读错层会让所有参数一律变成 string
            if param["name"] == "page" and param["type"] != "integer":
                problems.append("page 参数类型不是 integer（疑似读错了 schema 层级）：" + op["path"])
            if param["name"] in ("type_id", "region_id", "system_id") and param["type"] == "string":
                problems.append("ID 类参数被读成 string（疑似读错了 schema 层级）："
                                + param["name"] + " @ " + op["path"])
    return problems


def print_stats(index):
    ops = index["operations"]
    writes = [op for op in ops if op["write"]]
    reads = [op for op in ops if not op["write"]]
    print("操作总数：%d（只读 %d / 写 %d）" % (len(ops), len(reads), len(writes)))
    print("匿名：%d，需授权：%d" % (len([o for o in ops if not o["auth"]]), len([o for o in ops if o["auth"]])))
    print("查询型 POST（不算写）：")
    for op in ops:
        if op["verb"] == "POST" and not op["write"]:
            print("    %s %s" % (op["verb"], op["path"]))
    print("写操作清单：")
    for op in writes:
        print("    %-7s %-60s %s" % (op["verb"], op["path"], op["summary"]))


def main():
    parser = argparse.ArgumentParser(description="生成 ESI 操作索引（AI 助手的 esi_* 网关用）")
    parser.add_argument("--spec", default=SPEC_URL, help="规格来源（URL 或本地文件）")
    parser.add_argument("--out", default=DEFAULT_OUT, help="输出 JSON 路径")
    parser.add_argument("--check", action="store_true", help="只打印统计与自检，不写文件")
    args = parser.parse_args()

    print("读取规格：%s" % args.spec)
    spec = load_spec(args.spec)
    index = build(spec)
    print_stats(index)

    problems = self_check(index)
    if problems:
        print("\n自检失败：")
        for problem in problems:
            print("    " + problem)
        return 1

    print("\n自检通过。")
    if args.check:
        return 0

    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(index, handle, ensure_ascii=False, indent=1)
        handle.write("\n")
    print("已写入：%s（%.1f KB）" % (args.out, os.path.getsize(args.out) / 1024.0))
    return 0


if __name__ == "__main__":
    sys.exit(main())
