# -*- coding: utf-8 -*-
"""翻译质量自动修正（第三批：残留标记 + 零星英文句子）。

前两批修了术语与漏译步骤；这批处理抽查里剩下的硬骨头：
  · `&nbsp;` 实体、`|thumb` / `300px|thumb|xxx` 图片标记漏进正文
  · `的的` 之类的重复字
  · 表格里没进翻译队列的零星英文句子（NPC 变体说明）
"""

import io
import os
import re
import sqlite3

HERE = os.path.dirname(os.path.abspath(__file__))
DB = os.path.abspath(os.path.join(HERE, '..', '..', 'TheGuideToTheNewEden',
                                  'TheGuideToTheNewEden.WPF', 'Resources', 'Database', 'static.db'))

# 原样替换（英文句子逐条给译文，避免再走一轮 agent）
EXACT = {
    'Libertus is the only variant with tackle': 'Libertus 是唯一带反跳的变体',
    'Dionia is only variant with EWAR': 'Dionia 是唯一带电子战的变体',
    'Seer is only variant with EWAR': 'Seer 是唯一带电子战的变体',
    'Forian is the only variant with EWAR': 'Forian 是唯一带电子战的变体',
    'Bahir is only variant with EWAR': 'Bahir 是唯一带电子战的变体',
    'When destructed this groups pirates leave the scene': '被击毁后这伙海盗会离开现场',
    'is needed to transport the mission objective item': '用于运输任务目标物品',
    'urity system one jump away from the agent': '安星系，距离代理人一跳',
}

RULES = [
    ('&nbsp;', ' '),
    ('的的', '的'),
    (re.compile(r'\d+px\|thumb\|[^\s|]*'), ''),   # 300px|thumb|第一层 → 空
    (re.compile(r'\|thumb\b[^\s|]*'), ''),
    (re.compile(r'[ \t]{2,}'), ' '),
]


def main():
    con = sqlite3.connect(DB)
    changed = 0
    for table, col in (('Mission', 'WikiContent'), ('CombatSite', 'WikiContent'),
                       ('IncursionSite', 'Content')):
        for rid, body in con.execute('select Id, %s from %s' % (col, table)).fetchall():
            if not body:
                continue
            new = body
            for src, dst in EXACT.items():
                new = new.replace(src, dst)
            for pat, rep in RULES:
                new = pat.sub(rep, new) if hasattr(pat, 'sub') else new.replace(pat, rep)
            new = re.sub(r'(?m)^[ \t]+$', '', new)
            if new != body:
                con.execute('update %s set %s = ? where Id = ?' % (table, col), (new, rid))
                changed += 1
    con.commit()
    print('修正 %d 行' % changed)


if __name__ == '__main__':
    main()
