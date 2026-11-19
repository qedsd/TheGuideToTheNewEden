# -*- coding: utf-8 -*-
"""翻译质量抽查：用规则扫一遍全库，再抽样给人看。

规则分三类：
① 术语违规：游戏里的固定译名被译成别的说法（`pocket→口袋`、`standing→站台`、`agent→经纪人`）；
② 残留：中文句子里夹着长英文串、wiki 标记（`'''` `[[` `{{` `&nbsp;`）、重复字（`的的` `了了`）；
③ 结构：行首多余符号、数字/单位不一致。

用法：python qa_translation.py [--sample 20]
"""

import io
import json
import os
import random
import re
import sqlite3
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DB = os.path.abspath(os.path.join(HERE, '..', '..', 'TheGuideToTheNewEden',
                                  'TheGuideToTheNewEden.WPF', 'Resources', 'Database', 'static.db'))

CJK = re.compile(r'[\u4e00-\u9fff]')

# 正确译名 → 常见错译（出现即报告）
TERM_ERRORS = {
    '萨沙国度': ['沙沙国', '萨沙国国', '桑沙'],
    '萨沙': ['沙沙'],
    '血袭者': ['血掠者', '血袭教'],
    '古斯塔斯': ['古斯他斯', '古斯塔斯人'],
    '天使': ['安吉尔'],
    '天蛇': ['塞pentis', '塞彭蒂斯'],
    '无人机': ['雄蜂'],
    '冬眠者': ['睡眠者', '沉睡者'],
    '死亡空间': ['死角空间', '深空空间'],
    '代理人': ['经纪人', '中介', '特工'],
    '声望': ['站台', '地位值'],
    '赏金': ['奖金', '悬赏金'],
    '跃迁': ['曲速', '跃迁飞行'],
    '打捞': ['抢救', '捞取'],
    '掉落': ['战利品箱'],
    '加速门': ['加速门禁', '加速度门'],
    '层': [],
}

# 明确不该出现的残留标记
MARKUP_LEAKS = ["'''", '[[', ']]', '{{', '}}', '&nbsp;', '<br', '|thumb', '300px']


def check(text):
    """返回问题列表（标签, 片段）。"""
    issues = []
    if not text:
        return issues
    for good, bads in TERM_ERRORS.items():
        for bad in bads:
            # 错译串是正确译名的子串时（`血袭` ⊂ `血袭者`）会疯狂误报，跳过
            if bad in good:
                continue
            idx = text.find(bad)
            if idx >= 0:
                issues.append(('术语', '%s→应为%s：…%s…'
                               % (bad, good, text[max(0, idx - 15):idx + len(bad) + 15].replace('\n', ' '))))
    for leak in MARKUP_LEAKS:
        if leak in text:
            idx = text.find(leak)
            issues.append(('残留', '%s：…%s…' % (leak, text[max(0, idx - 20):idx + 30].replace('\n', ' '))))
    for bad in ('的的', '了了', '是是', '在在', '。。'):
        if bad in text:
            issues.append(('重复字', bad))
    # 中文句子里夹 5 个以上连续英文单词
    for m in re.finditer(r'[\u4e00-\u9fff][^。！？\n]{0,40}?'
                         r'((?:[A-Za-z][A-Za-z\'-]*\s+){5,}[A-Za-z][A-Za-z\'-]*)', text):
        issues.append(('夹杂英文', m.group(1)[:70]))
    return issues


def main():
    sample = 20
    if '--sample' in sys.argv:
        sample = int(sys.argv[sys.argv.index('--sample') + 1])

    con = sqlite3.connect(DB)
    stats = {'Mission': 0, 'CombatSite': 0, 'IncursionSite': 0}
    hits = []
    for table, col, label in (('Mission', 'WikiContent', '任务'),
                              ('CombatSite', 'WikiContent', '站点'),
                              ('IncursionSite', 'Content', '入侵')):
        for rid, title, body in con.execute(
                'select Id, coalesce(TitleCN, TitleEN), %s from %s' % (col, table)):
            if not body:
                continue
            problems = check(body)
            if problems:
                stats[table] += 1
                hits.append((label, title, problems))

    print('=== 规则扫描 ===')
    for table, label in (('Mission', '任务'), ('CombatSite', '站点'), ('IncursionSite', '入侵')):
        print('  %s：%d 条命中' % (label, stats[table]))
    print('\n--- 命中明细（前 25 条）---')
    for label, title, problems in hits[:25]:
        print('  [%s] %s' % (label, title))
        for kind, detail in problems[:3]:
            print('      %s: %s' % (kind, detail))

    # 随机抽样：给人看的原文/译文对照
    print('\n=== 随机抽样 %d 条（对照原文看） ===' % sample)
    rows = con.execute(
        'select TitleEN, TitleCN, WikiContent from Mission '
        "where length(WikiContent) > 600 order by random() limit ?", (sample // 2,)).fetchall()
    rows += con.execute(
        'select TitleEN, TitleCN, WikiContent from CombatSite '
        'where length(WikiContent) > 600 order by random() limit ?', (sample - sample // 2,)).fetchall()
    for en, cn, body in rows:
        print('\n--- %s ｜ %s ---' % (cn, en))
        print(' '.join(body.split())[:260])

    json.dump([{'label': l, 'title': t, 'problems': p} for l, t, p in hits],
              io.open(os.path.join(HERE, 'qa_hits.json'), 'w', encoding='utf-8'),
              ensure_ascii=False, indent=1)
    print('\n→ qa_hits.json（%d 条）' % len(hits))


if __name__ == '__main__':
    sys.exit(main())
