# -*- coding: utf-8 -*-
"""把多轮"旧库中文名 → 英文标题"对齐结果合并成 align_merged.json（构建只认这一个）。

每轮一个 align_result*.json（`{"<英文标题>": "<中文名>"}`），可能互有重复。
合并规则：**先出现的优先**（第一轮经过更多人工确认），冲突会打印出来。
键必须能在 align_new_titles.json 的 `en` 里命中，否则丢弃（防止旧库改过标题导致错配）。
"""

import glob
import io
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'align_merged.json')
TITLES = os.path.join(HERE, 'align_new_titles.json')


def main():
    valid = {x['en'] for x in json.load(io.open(TITLES, encoding='utf-8'))}
    merged = {}
    conflicts = 0

    for path in sorted(glob.glob(os.path.join(HERE, 'align_result*.json'))):
        data = json.load(io.open(path, encoding='utf-8'))
        bad = [k for k in data if k not in valid]
        dup = [k for k in data if k in merged and merged[k] != data[k]]
        fresh = 0
        for k, v in data.items():
            if k not in valid:
                continue
            if k in merged:
                if merged[k] != v:
                    conflicts += 1
                    print('  冲突 %s: 保留「%s」忽略「%s」' % (k, merged[k], v))
                continue
            merged[k] = v
            fresh += 1
        print('%-22s %4d 条（新增 %d，无效键 %d，同键不同值 %d）'
              % (os.path.basename(path), len(data), fresh, len(bad), len(dup)))

    json.dump(merged, io.open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('\n合并结果：%d 条 → %s（冲突 %d）' % (len(merged), OUT, conflicts))
    return 0


if __name__ == '__main__':
    sys.exit(main())
