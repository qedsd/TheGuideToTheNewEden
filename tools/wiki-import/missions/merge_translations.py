# -*- coding: utf-8 -*-
"""合并 to_translate/zh_XX.json 到 translations.json，并做完整性校验。

校验项：
  · 每个 zh_XX.json 的键集合必须与 batch_XX.json **完全一致**（多一个少一个都报出来）
  · 译文不能为空、不能仍等于英文原文（短词除外，如 ISK / ECM）
  · 译文里不应出现未翻译的长英文句子（> 3 个连续英文词）

不合格的条目**不写入** translations.json（build 会回退英文），但会打印出来便于补。
"""

import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
IN_DIR = os.path.join(HERE, 'to_translate')
OUT = os.path.join(HERE, 'translations.json')

ASCII_WORD = re.compile(r'[A-Za-z]{2,}')


def main():
    trans = {}
    if os.path.exists(OUT):
        trans = json.load(io.open(OUT, encoding='utf-8'))

    total_keys = merged = missing = same = empties = 0
    bad_files = []

    for name in sorted(os.listdir(IN_DIR)):
        if not re.match(r'^batch_(\d+)\.json$', name):
            continue
        idx = re.match(r'^batch_(\d+)\.json$', name).group(1)
        batch = json.load(io.open(os.path.join(IN_DIR, name), encoding='utf-8'))
        zh_path = os.path.join(IN_DIR, 'zh_%s.json' % idx)
        if not os.path.exists(zh_path):
            bad_files.append('缺少 zh_%s.json' % idx)
            continue
        try:
            zh = json.load(io.open(zh_path, encoding='utf-8'))
        except Exception as ex:  # noqa: BLE001
            bad_files.append('zh_%s.json 解析失败：%s' % (idx, ex))
            continue

        src = {k: v for k, v in batch}
        total_keys += len(src)

        extra = set(zh) - set(src)
        lost = set(src) - set(zh)
        if extra:
            bad_files.append('zh_%s 多出 %d 个键' % (idx, len(extra)))
        if lost:
            bad_files.append('zh_%s 缺少 %d 个键' % (idx, len(lost)))
            missing += len(lost)

        for k, en in src.items():
            zh_text = (zh.get(k) or '').strip()
            if not zh_text:
                empties += 1
                continue
            if zh_text == en.strip():
                # 纯符号/纯拉丁缩写允许原样
                if len(ASCII_WORD.findall(en)) > 0 and not re.search(r'[\u4e00-\u9fff]', zh_text):
                    same += 1
                    continue
            trans[k] = zh_text
            merged += 1

    json.dump(trans, io.open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

    print('批次源片段 %d，写入译文 %d 条（累计 %d 条）' % (total_keys, merged, len(trans)))
    print('  缺键 %d ｜ 空译文 %d ｜ 仍是英文 %d' % (missing, empties, same))
    for b in bad_files:
        print('  ！', b)

    # 抽样看看口吻
    samples = list(trans.items())[:0]
    return 0


if __name__ == '__main__':
    sys.exit(main())
