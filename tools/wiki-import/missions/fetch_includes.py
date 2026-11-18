# -*- coding: utf-8 -*-
"""补抓"转写页"：史诗任务线的章节页只有 `{{:任务名}}`，真正的任务在独立页面里。

从 parsed.json 收集 includes，按引用它的页面继承分类，抓回 raw/ 并并入 index.json。
"""

import io
import json
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from fetch_missions import RAW_DIR, INDEX, safe_name, raw_wikitext  # noqa: E402


def main():
    parsed = json.load(io.open(os.path.join(HERE, 'parsed.json'), encoding='utf-8'))
    entries = json.load(io.open(INDEX, encoding='utf-8'))
    known = {e['title'] for e in entries}

    want = {}
    for page in parsed:
        for inc in page.get('includes') or []:
            if inc and inc not in known and inc not in want:
                want[inc] = page['cat']

    print('待补抓 %d 页' % len(want))
    added = failed = 0
    for title, cat in sorted(want.items()):
        try:
            text = raw_wikitext(title)
        except Exception as ex:  # noqa: BLE001
            print('   FAIL %s: %s' % (title, ex))
            failed += 1
            continue
        if not text.strip():
            print('   空页：%s' % title)
            failed += 1
            continue

        path = os.path.join(RAW_DIR, safe_name(title) + '.txt')
        io.open(path, 'w', encoding='utf-8', newline='').write(text)
        entries.append({
            'title': title, 'cat': cat, 'catCn': '转写页',
            'file': os.path.basename(path), 'chars': len(text),
        })
        added += 1
        time.sleep(0.15)

    entries.sort(key=lambda e: (e['cat'], e['title']))
    json.dump(entries, io.open(INDEX, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('补抓完成：新增 %d，失败 %d，index 共 %d 页' % (added, failed, len(entries)))
    return 0


if __name__ == '__main__':
    sys.exit(main())
