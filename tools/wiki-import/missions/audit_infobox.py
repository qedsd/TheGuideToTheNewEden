# -*- coding: utf-8 -*-
"""全量核对：找出"页面里有任务字段、但解析器没识别成 infobox"的页面。

判据：正文里出现 `|Level=` / `|Type=` / `|Objective=` / `|Rewards=` 这类字段行，
但 parsed.json 里该页没有任何带 info 的 variant —— 说明信息框的模板名没被认出来。
同时统计所有"像任务信息框"的模板名，看看还有多少种变体。
"""

import io
import json
import os
import re
import sys
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'raw')
PARSED = os.path.join(HERE, 'parsed.json')

FIELD_LINE = re.compile(r'(?m)^\s*\|\s*(Level|Type|Objective|Rewards|ShipSuggestion|ShipSizeLimit|'
                        r'EWAR|WebPoint|Faction1?|Standings?Loss)\s*=')
TPL = re.compile(r'\{\{\s*([A-Za-z0-9_\-\. :/]+?)\s*[|\}]')


def main():
    parsed = json.load(io.open(PARSED, encoding='utf-8'))
    # raw 文件名有两种：index 里的（带空格）和 cosmos 补抓的（下划线）。
    # 必须按 index 的 file→title 映射来对，否则会把"下划线副本"误判成没解析（上一版就是这么误报的）。
    idx = json.load(io.open(os.path.join(HERE, 'index.json'), encoding='utf-8'))
    file_title = {e['file']: e['title'] for e in idx}
    parsed_by_title = {p['title']: p for p in parsed}

    names = Counter()
    suspects = []
    checked = 0
    for fn in os.listdir(RAW):
        if not fn.endswith('.txt'):
            continue
        checked += 1
        text = io.open(os.path.join(RAW, fn), encoding='utf-8').read()
        for name in TPL.findall(text):
            low = name.strip().lower()
            if 'mission' in low or low in ('infobox agent', 'cmbsiteinfo'):
                names[name.strip()] += 1
        if not FIELD_LINE.search(text):
            continue
        # 页面有字段行：看看解析结果里有没有 info
        title = file_title.get(fn)
        page = parsed_by_title.get(title) if title else None
        has_info = bool(page and any(v.get('info') for v in page.get('variants') or []))
        if not has_info:
            suspects.append({'file': fn, 'title': page['title'] if page else fn,
                             'fields': len(FIELD_LINE.findall(text)),
                             'chars': len(text)})

    print('扫描 raw 页面 %d 个' % checked)
    print('\n=== 见过"像任务信息框"的模板名（按出现次数）===')
    for name, cnt in names.most_common(20):
        print('  %5d× %s' % (cnt, name))

    print('\n=== 有字段行但没解析出 infobox 的页面：%d 个 ===' % len(suspects))
    for s in sorted(suspects, key=lambda x: -x['fields'])[:40]:
        print('  %-48s 字段行 %2d ｜ %5d 字符' % (s['title'][:48], s['fields'], s['chars']))

    json.dump(suspects, io.open(os.path.join(HERE, 'audit_suspects.json'), 'w', encoding='utf-8'),
              ensure_ascii=False, indent=1)
    print('\n→ audit_suspects.json')


if __name__ == '__main__':
    sys.exit(main())
