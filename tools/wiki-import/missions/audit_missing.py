# -*- coding: utf-8 -*-
"""盘点任务库"还没翻译"的部分：按来源/段落类型统计英文体量，给后续翻译排优先级。"""

import collections
import io
import json
import os
import re
import sqlite3

DB = (r"D:\QEDSD\Code\TheGuideToTheNewEden\TheGuideToTheNewEden"
      r"\TheGuideToTheNewEden.WPF\Resources\Database\static.db")
HERE = r"D:\QEDSD\Code\TheGuideToTheNewEden\Tools\wiki-import\missions"

CJK = re.compile(r'[\u4e00-\u9fff]')
LATIN_WORD = re.compile(r'[A-Za-z]{2,}')
TABLE_ROW = re.compile(r'^\|')


def main():
    con = sqlite3.connect(DB)
    cur = con.cursor()
    cur.execute("select TitleEN, TitleCN, Objective, Ewar, ShipLimit, ShipSuggestion, Rewards, "
                "Extra, Location, Arc, WikiContent, IsStub from Mission")
    rows = cur.fetchall()
    print("任务总数: %d" % len(rows))

    # 1) 结构化字段：有多少还是纯英文 / 为空
    fields = ['Objective', 'Ewar', 'ShipLimit', 'ShipSuggestion', 'Rewards', 'Extra', 'Location']
    print("\n== 结构化字段 ==")
    for idx, name in enumerate(fields, start=2):
        empty = latin = cjk = 0
        for r in rows:
            v = (r[idx] or '').strip()
            if not v:
                empty += 1
            elif CJK.search(v):
                cjk += 1
            else:
                latin += 1
        print("  %-16s 空 %4d ｜ 中文 %4d ｜ 仅英文 %4d" % (name, empty, cjk, latin))

    # 2) 正文：按段落统计中英文
    print("\n== 正文（WikiContent）==")
    total_chars = cjk_chars = latin_only_chars = 0
    latin_only_paras = 0
    brief_have = brief_cjk = brief_latin = 0
    table_rows = table_rows_latin = 0
    english_paras = collections.Counter()

    for _en, _cn, _obj, _ew, _sl, _ss, _rw, _ex, _loc, _arc, body, _stub in rows:
        body = body or ''
        total_chars += len(body)
        cjk_chars += len(CJK.findall(body))

        # 分段：简报段单独统计
        in_brief = False
        for line in body.split('\n'):
            s = line.strip()
            if s.startswith('### 简报'):
                in_brief = True
                brief_have += 1
                continue
            if s.startswith('### ') or s.startswith('**') or s.startswith('> '):
                in_brief = False
                continue
            if not s:
                continue
            if TABLE_ROW.match(s):
                table_rows += 1
                if not CJK.search(s) and LATIN_WORD.search(s):
                    table_rows_latin += 1
                in_brief = False
                continue
            if CJK.search(s):
                if in_brief:
                    brief_cjk += 1
                continue
            if len(LATIN_WORD.findall(s)) >= 4:
                latin_only_chars += len(s)
                latin_only_paras += 1
                if in_brief:
                    brief_latin += 1
                english_paras[s[:60]] += 1

    print("  总字符 %d ｜ 含汉字字符 %d (%.0f%%)" % (total_chars, cjk_chars, 100.0 * cjk_chars / total_chars))
    print("  纯英文段落 %d 段，共 %d 字符" % (latin_only_paras, latin_only_chars))
    print("  有简报的任务 %d（其中中文段 %d，英文段 %d）" % (brief_have, brief_cjk, brief_latin))
    print("  表格行 %d，其中无中文 %d" % (table_rows, table_rows_latin))

    # 3) 未翻译段落按“重复度”排 —— 重复多说明是模板化的句子，先翻收益大
    print("\n== 出现次数最多的纯英文段落（前 15）==")
    for text, cnt in english_paras.most_common(15):
        print("  %2d× %s" % (cnt, text))

    # 4) 分类维度：每类还剩多少英文段落
    print("\n== 按分类 ==")
    by_cat = collections.defaultdict(lambda: [0, 0, 0])   # 任务数, 英文段, 英文字符
    for r in rows:
        pass
    cur.execute("select Category, WikiContent from Mission")
    for cat, body in cur.fetchall():
        body = body or ''
        paras = [l.strip() for l in body.split('\n')
                 if l.strip() and not TABLE_ROW.match(l.strip()) and not CJK.search(l)
                 and len(LATIN_WORD.findall(l)) >= 4]
        by_cat[cat][0] += 1
        by_cat[cat][1] += len(paras)
        by_cat[cat][2] += sum(len(p) for p in paras)
    CAT = {0: '安全', 1: '运输', 2: '采矿', 3: '故事线', 4: '史诗', 5: '异常', 6: 'COSMOS',
           7: '研发', 8: '职业', 9: 'Circle'}
    for cat in sorted(by_cat):
        n, paras, chars = by_cat[cat]
        print("  %-8s 任务 %4d ｜ 英文段 %5d ｜ %7d 字符" % (CAT.get(cat, cat), n, paras, chars))


if __name__ == '__main__':
    main()
