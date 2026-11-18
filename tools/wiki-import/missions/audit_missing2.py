# -*- coding: utf-8 -*-
"""精确盘点"还缺哪些翻译"，并区分"能靠已有译文补"与"必须新翻"。

用法：python audit_missing2.py
"""

import collections
import io
import json
import os
import re
import sqlite3

DB = (r"D:\QEDSD\Code\TheGuideToTheNewEden\TheGuideToTheNewEden"
      r"\TheGuideToTheNewEden.WPF\Resources\Database\static.db")
HERE = r"D:\QEDSD\Code\TheGuideToTheNewEden\Tools\wiki-import\missions"
TRANS = json.load(io.open(os.path.join(HERE, 'translations.json'), encoding='utf-8'))

CJK = re.compile(r'[\u4e00-\u9fff]')
LATIN_WORD = re.compile(r'[A-Za-z]{2,}')
TABLE_ROW = re.compile(r'^\|')


def main():
    con = sqlite3.connect(DB)
    cur = con.cursor()
    cur.execute("select TitleCN, Objective, Ewar, ShipLimit, ShipSuggestion, Rewards, Extra, "
                "WikiContent from Mission")
    rows = cur.fetchall()
    print("任务 %d 条" % len(rows))

    # ---- 1. 结构化字段 ----
    print("\n【1】属性字段（页面顶部「属性」区 / AI 工具 / 搜索都读这些列）")
    for idx, name in enumerate(['Objective', 'Ewar', 'ShipLimit', 'ShipSuggestion', 'Rewards', 'Extra'],
                               start=1):
        empty = zh = en = 0
        samples = []
        for r in rows:
            v = (r[idx] or '').strip()
            if not v:
                empty += 1
            elif CJK.search(v):
                zh += 1
            else:
                en += 1
                if len(samples) < 3:
                    samples.append(v[:52])
        print("  %-15s 空 %4d ｜ 中文 %4d ｜ 仍英文 %4d" % (name, empty, zh, en))
        for s in samples:
            print("        例: %s" % s)

    # ---- 2. 简报 ----
    print("\n【2】简报（代理人接任务时那段话）")
    have = sum(1 for r in rows if "### 简报" in (r[7] or ""))
    print("  SDE 官方中文简报：%d 条" % have)
    print("  没有简报：%d 条（wiki 的 {{MissionBriefing}} 英文原文当前**未渲染**）" % (len(rows) - have))

    # ---- 3. 正文散文 ----
    print("\n【3】正文散文（walkthrough / 打法 / 要点）")
    paras = []
    for r in rows:
        body = r[7] or ''
        in_brief = False
        for line in body.split('\n'):
            s = line.strip()
            if s.startswith('### 简报'):
                in_brief = True
                continue
            if s.startswith('### ') or s.startswith('**') or s.startswith('> '):
                in_brief = False
                continue
            if not s or TABLE_ROW.match(s) or in_brief:
                continue
            if CJK.search(s):
                continue
            if len(LATIN_WORD.findall(s)) >= 2:
                paras.append(s)
    uniq = collections.Counter(paras)
    print("  英文段落 %d 段（去重后 %d 段），共 %d 字符"
          % (len(paras), len(uniq), sum(len(p) for p in paras)))
    print("  重复最多的（翻了收益最大）：")
    for text, cnt in uniq.most_common(5):
        print("    %2d× %s" % (cnt, text[:64]))

    # ---- 4. NPC 表 ----
    print("\n【4】NPC 表")
    names = name_en = notes = notes_en = 0
    for r in rows:
        body = r[7] or ''
        for m in re.finditer(r'^\| ([^|]+) \| ([\d\-~?]+) \| ([^|]*) \| ([^|]*) \|$', body, re.M):
            names += 1
            if not CJK.search(m.group(3)) and LATIN_WORD.search(m.group(3)):
                name_en += 1
            if m.group(4).strip():
                notes += 1
                if not CJK.search(m.group(4)):
                    notes_en += 1
    print("  舰名 %d 行，其中纯英文 %d（SDE 无对应：wiki 自造统称/拼错）" % (names, name_en))
    print("  备注 %d 行，其中整句英文 %d" % (notes, notes_en))

    # ---- 5. 解析残留 ----
    print("\n【5】解析残留检查")
    cur.execute("select count(*) from Mission where WikiContent like '%includeonly%'")
    print("  includeonly 残留：%d 条" % cur.fetchone()[0])
    cur.execute("select count(*) from Mission where WikiContent like '%{{%'")
    print("  {{ 模板残留：%d 条" % cur.fetchone()[0])

    # ---- 6. 新增页面未送翻的量（翻译缓存里没有的片段） ----
    print("\n【6】需要**新翻**的片段（译文缓存里没有）")
    todo = collections.Counter()
    for r in rows:
        for idx in range(1, 7):
            v = (r[idx] or '').strip()
            if v and not CJK.search(v):
                todo[v] += 1
    print("  属性字段待翻片段：%d 条（去重）" % len(todo))

    cache_hit = sum(1 for p in set(paras) if p in TRANS)
    print("  正文段落已在缓存里：%d / %d 段" % (cache_hit, len(set(paras))))


if __name__ == '__main__':
    main()
