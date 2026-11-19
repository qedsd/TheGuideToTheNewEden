# -*- coding: utf-8 -*-
"""翻译质量自动修正（第一批：纯文本替换）。

修的是抽查里确认的几类问题：
  ① `特工` → `代理人`（agent 的统一译名）
  ② 残留 `'''`（wiki 粗体标记）
  ③ `曲速` → `跃迁`（warp 的错译）
  ④ 数字被中文逗号断开：`1、800m3` → `1,800 m3`
另外把"表格表头行还是英文"的抽出来，交给下一步翻译。
"""

import io
import json
import os
import re
import sqlite3

HERE = os.path.dirname(os.path.abspath(__file__))
DB = os.path.abspath(os.path.join(HERE, '..', '..', 'TheGuideToTheNewEden',
                                  'TheGuideToTheNewEden.WPF', 'Resources', 'Database', 'static.db'))
OUT = os.path.join(HERE, 'qa_header_fix.json')
CJK = re.compile(r'[\u4e00-\u9fff]')
WORD = re.compile(r"[A-Za-z][A-Za-z'\-]*")

# 纯文本替换（顺序有意义）
RULES = [
    ("'''", ''),
    ('特工', '代理人'),
    ('曲速', '跃迁'),
    (re.compile(r'(\d)、(\d{3})'), r'\1,\2'),      # 1、800 → 1,800
    (re.compile(r'。{2,}'), '。'),
    (re.compile(r'\s+([，。；：])'), r'\1'),
]


def collect_headers(content):
    """表头行：`| a | b |` 后面紧跟 `|---|`。返回 [{line_index, cells}]。"""
    lines = (content or '').split('\n')
    out = []
    for i, line in enumerate(lines[:-1]):
        s = line.strip()
        if not s.startswith('|') or s.startswith('|---'):
            continue
        nxt = lines[i + 1].strip()
        if not nxt.startswith('|---'):
            continue
        cells = [c.strip() for c in s.split('|') if c.strip()]
        if cells and all(not CJK.search(c) for c in cells) and any(WORD.search(c) for c in cells):
            out.append({'index': i, 'cells': cells})
    return out


def main():
    con = sqlite3.connect(DB)
    changed = 0
    headers = []
    for table, col in (('Mission', 'WikiContent'), ('CombatSite', 'WikiContent'),
                       ('IncursionSite', 'Content')):
        rows = con.execute('select Id, %s from %s' % (col, table)).fetchall()
        for rid, body in rows:
            if not body:
                continue
            new = body
            for pat, rep in RULES:
                new = pat.sub(rep, new) if hasattr(pat, 'sub') else new.replace(pat, rep)
            for h in collect_headers(new):
                headers.append({'table': table, 'id': rid, 'line': h['index'], 'cells': h['cells']})
            if new != body:
                con.execute('update %s set %s = ? where Id = ?' % (table, col), (new, rid))
                changed += 1
    con.commit()
    print('修正 %d 行' % changed)

    # 表头去重后导出
    uniq = sorted({c for h in headers for c in h['cells'] if WORD.search(c) and not CJK.search(c)})
    json.dump({'headers': uniq}, io.open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('英文表头单元格 %d 个（去重）→ %s' % (len(uniq), OUT))
    for c in uniq[:15]:
        print('   ', c)


if __name__ == '__main__':
    main()
