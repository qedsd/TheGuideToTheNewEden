"""合并各批翻译结果，校验键集合完整性，产出 `translations.json`。

- 逐个 `batch_wN_XX.json` 与 `zh_wN_XX.json` 比对键集合，缺一个都报出来
- 站点名同理（`names_wN.json` → `zh_names_wN.json`）
- 合并结果：{ "unit_id": "中文" } 与 { "English Name": "中文名" } 分别落到
  `translations.json` / `translations_names.json`

用法：
    python merge_translations.py
"""

import io
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))

units_all = {}
names_all = {}
problems = []

files = sorted(f for f in os.listdir(HERE) if f.startswith('batch_w') and f.endswith('.json'))
for f in files:
    tag = f[len('batch_'):-len('.json')]
    zh_path = os.path.join(HERE, 'zh_%s.json' % tag)
    src = json.load(io.open(os.path.join(HERE, f), encoding='utf-8'))
    if not os.path.exists(zh_path):
        problems.append('%s：缺译文文件' % tag)
        continue
    zh = json.load(io.open(zh_path, encoding='utf-8'))
    missing = [k for k in src if k not in zh]
    if missing:
        problems.append('%s：缺 %d 条（例 %s）' % (tag, len(missing), missing[:3]))
    for k in src:
        if k in zh and zh[k]:
            units_all[k] = zh[k]
    print('%-10s %4d/%4d' % (tag, len(zh), len(src)))

for w in (1, 2, 3):
    src_path = os.path.join(HERE, 'names_w%d.json' % w)
    zh_path = os.path.join(HERE, 'zh_names_w%d.json' % w)
    if not os.path.exists(src_path):
        continue
    src = json.load(io.open(src_path, encoding='utf-8'))
    if not os.path.exists(zh_path):
        problems.append('names_w%d：缺译文文件' % w)
        continue
    zh = json.load(io.open(zh_path, encoding='utf-8'))
    missing = [k for k in src if k not in zh]
    if missing:
        problems.append('names_w%d：缺 %d 个（例 %s）' % (w, len(missing), missing[:3]))
    for k in src:
        if k in zh and zh[k]:
            names_all[k] = zh[k]
    print('names_w%-2d %4d/%4d' % (w, len(zh), len(src)))

json.dump(units_all, io.open(os.path.join(HERE, 'translations.json'), 'w', encoding='utf-8'),
          ensure_ascii=False, indent=1)
json.dump(names_all, io.open(os.path.join(HERE, 'translations_names.json'), 'w', encoding='utf-8'),
          ensure_ascii=False, indent=1)

print('\n合并：正文片段 %d 条、站点名 %d 个' % (len(units_all), len(names_all)))
if problems:
    print('\n问题 %d 项：' % len(problems))
    for p in problems:
        print('  -', p)
    sys.exit(1)
print('键集合完整。')
