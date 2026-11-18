# -*- coding: utf-8 -*-
"""对比"旧任务库"（405 条，只有中文名）与"新任务库"（wiki 全量），列出可能漏掉的任务。

旧库只有中文名，所以要先把它的名字归一化（去掉「（一级任务）」「（3/5）」这类后缀与空格），
再在新库里按中文名精确 / 包含 匹配，剩下的就是"旧库有、新库似乎没有"的候选。
"""

import io
import json
import os
import re
import sqlite3
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
NEW_DB = (r"D:\QEDSD\Code\TheGuideToTheNewEden\TheGuideToTheNewEden"
          r"\TheGuideToTheNewEden.WPF\Resources\Database\static.db")
OLD_DB = os.path.join(os.environ.get('TEMP', '.'), 'old_static.db')

SUFFIX = re.compile(r'[（(][^）)]*[）)]\s*$')


def norm(s):
    """去掉**所有**尾部括号注释（旧名字常有「（四级任务）」+「（The Blockade）」两层）。"""
    s = (s or '').strip()
    for _ in range(4):
        nxt = SUFFIX.sub('', s).strip()
        if nxt == s:
            break
        s = nxt
    s = re.sub(r'[\s\-–—_·•]+', '', s)
    return s


def main():
    if not os.path.exists(OLD_DB):
        print('缺少旧库副本：%s' % OLD_DB)
        return 1

    old = sqlite3.connect(OLD_DB).execute(
        'select Title_Zh from Mission where Title_Zh is not null and Title_Zh <> ""').fetchall()
    old_names = [r[0] for r in old]
    print('旧库任务：%d 条' % len(old_names))

    new = sqlite3.connect(NEW_DB).execute('select TitleEN, TitleCN from Mission').fetchall()
    print('新库任务：%d 条' % len(new))

    new_norm = [norm(cn) for _en, cn in new]
    new_set = set(new_norm)
    # 包含匹配用的长串集合
    new_joined = '\n'.join(new_norm)

    missing = []
    for name in old_names:
        n = norm(name)
        if not n:
            continue
        if n in new_set:
            continue
        # 旧名常带"（一级任务 加达里）"这种尾注，去掉后仍不中，就试包含
        if len(n) >= 3 and n in new_joined:
            continue
        missing.append(name)

    print('旧库有、新库未匹配上：%d 条' % len(missing))
    for m in missing[:40]:
        print('   ', m)

    out = os.path.join(HERE, 'old_only_titles.json')
    json.dump(sorted(set(missing)), io.open(out, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('\n完整清单 → %s' % out)
    return 0


if __name__ == '__main__':
    sys.exit(main())
