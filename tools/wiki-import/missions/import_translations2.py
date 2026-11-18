# -*- coding: utf-8 -*-
"""把 to_translate2/zh_XXX.json 合并进 translations.json。

校验：
- 键必须存在于对应 batch 里（防止模型编键）
- 值必须含汉字（否则视为没翻，跳过）
- 值不能等于原文
- 键数量应等于 batch 条目数（少了会打印缺哪些）
"""

import glob
import io
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
DIR = os.path.join(HERE, 'to_translate2')
TRANS = os.path.join(HERE, 'translations.json')
CJK = re.compile(r'[\u4e00-\u9fff]')


def main():
    trans = json.load(io.open(TRANS, encoding='utf-8'))
    before = len(trans)

    total = added = skipped = 0
    missing_report = []
    for batch_path in sorted(glob.glob(os.path.join(DIR, 'batch_*.json'))):
        name = os.path.basename(batch_path).replace('batch_', '').replace('.json', '')
        zh_path = os.path.join(DIR, 'zh_%s.json' % name)
        if not os.path.exists(zh_path):
            missing_report.append(name + '(无译文文件)')
            continue

        batch = json.load(io.open(batch_path, encoding='utf-8'))
        try:
            zh = json.load(io.open(zh_path, encoding='utf-8'))
        except Exception as ex:  # noqa: BLE001
            missing_report.append('%s(解析失败:%s)' % (name, ex))
            continue

        keys = {item['k']: item['en'] for item in batch}
        total += len(batch)
        absent = [item['en'][:40] for item in batch if item['k'] not in zh]
        if absent:
            missing_report.append('%s 缺 %d 条，例如 %s' % (name, len(absent), absent[0]))

        for k, en in keys.items():
            val = (zh.get(k) or '').strip()
            if not val or val == en or not CJK.search(val):
                skipped += 1
                continue
            if k not in trans:
                added += 1
            trans[k] = val

    json.dump(trans, io.open(TRANS, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('译文缓存 %d → %d（新增 %d），跳过 %d 条无效/未翻，处理条目 %d'
          % (before, len(trans), added, skipped, total))
    if missing_report:
        print('\n需要关注的批次：')
        for item in missing_report[:20]:
            print('  ', item)


if __name__ == '__main__':
    main()
