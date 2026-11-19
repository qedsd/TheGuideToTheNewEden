# -*- coding: utf-8 -*-
"""把入侵站点的渲染 HTML 解析成结构化 Markdown（小节 + 表格）。

渲染后模板已经展开，所以"舰队需求表""敌人清单表"都能拿到真实内容；
这是抓 wikitext 拿不到的部分。

产物：incursions_parsed.json —— [{title, type, sections:[{level,title,text}], chars}]
其中 type 从 wikitext 的 `{{incursion sites|<type>|...}}` 读出（vanguard/assaults/headquarters）。
"""

import io
import json
import os
import re
import sys
import urllib.parse
import urllib.request

import lxml.html

API = 'https://wiki.eveuniversity.org/api.php'
BASE = 'https://wiki.eveuniversity.org'
UA = {'User-Agent': 'TheGuideToTheNewEden/1.0'}
HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'raw', 'incursions')
OUT = os.path.join(HERE, 'incursions_parsed.json')

DROP_CLASSES = ('navbox', 'navbox-inner', 'noprint', 'mw-editsection', 'toc', 'reference',
                'mw-references-wrap', 'printfooter', 'catlinks', 'mw-jump-link', 'hatnote',
                'ambox', 'mbox', 'metadata', 'sistersitebox', 'plainlinks')


def raw_wikitext(title):
    url = BASE + '/index.php?' + urllib.parse.urlencode(
        {'title': title.replace(' ', '_'), 'action': 'raw'})
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=60) as resp:
        return resp.read().decode('utf-8', 'replace')


def site_type(text):
    """`{{incursion sites|assaults|ncn}}` → assaults。"""
    m = re.search(r'\{\{\s*incursion sites\s*\|\s*([a-z ]+)', text, re.I)
    if not m:
        return ''
    raw = m.group(1).strip().lower()
    return {'vanguard': 'vanguard', 'assaults': 'assault', 'assault': 'assault',
            'headquarters': 'headquarters', 'hq': 'headquarters'}.get(raw, raw)


def clean_node(node):
    """删掉导航框、编辑链接、图片等噪声。"""
    for bad in node.xpath('.//*[contains(@class, "navbox") or contains(@class, "mw-editsection") '
                          'or contains(@class, "toc") or contains(@class, "noprint") '
                          'or contains(@class, "mw-references-wrap") or contains(@class, "catlinks") '
                          'or contains(@class, "mw-jump-link") or contains(@class, "hatnote")]'):
        bad.getparent().remove(bad)
    for img in node.xpath('.//img'):
        img.getparent().remove(img)
    for sup in node.xpath('.//sup[contains(@class, "reference")]'):
        sup.getparent().remove(sup)


def table_to_md(table):
    """表格 → Markdown。合并单元格按"左上角文本填满"处理，够读就行。"""
    rows = []
    # 注意：lxml 会把行包进 <tbody>，用 ./tr 一行都取不到（踩过，表格全空）
    for tr in table.xpath('.//tr'):
        cells = []
        for td in tr.xpath('./th|./td'):
            text = ' '.join(td.text_content().split())
            # wiki 的响应式表格会把同一格渲染两遍（`184 184 m`），去掉紧邻重复
            text = re.sub(r'\b(\S+(?: \S+){0,2}) \1\b', r'\1', text)
            text = re.sub(r'\s+', ' ', text).strip()
            span = int(td.get('colspan') or 1)
            cells.append(text)
            for _ in range(span - 1):
                cells.append('')
        if any(c for c in cells):
            rows.append(cells)
    if not rows:
        return ''
    width = max(len(r) for r in rows)
    rows = [r + [''] * (width - len(r)) for r in rows]
    out = ['| ' + ' | '.join(rows[0]) + ' |', '|' + '---|' * width]
    for r in rows[1:]:
        out.append('| ' + ' | '.join(r) + ' |')
    return '\n'.join(out)


def walk(node, out, level=2):
    """按文档顺序把 h2/h3/段落/列表/表格 变成 Markdown。"""
    for child in node:
        tag = child.tag if isinstance(child.tag, str) else ''
        if tag in ('h2', 'h3', 'h4'):
            lv = min(6, int(tag[1]) + 1)
            text = ' '.join(child.text_content().split())
            if text:
                out.append(('heading', lv, text))
        elif tag == 'table':
            md = table_to_md(child)
            if md:
                out.append(('table', 0, md))
        elif tag in ('ul', 'ol'):
            items = []
            for li in child.xpath('./li'):
                t = ' '.join(li.text_content().split())
                if t:
                    items.append('- ' + t)
            if items:
                out.append(('list', 0, '\n'.join(items)))
        elif tag == 'p':
            text = ' '.join(child.text_content().split())
            if text:
                out.append(('para', 0, text))
        elif tag == 'div':
            walk(child, out, level)


def parse_page(path):
    html = io.open(path, encoding='utf-8').read()
    doc = lxml.html.fromstring(html)
    content = doc.xpath('//*[@id="mw-content-text"]')
    node = content[0] if content else doc
    clean_node(node)

    items = []
    walk(node, items)

    sections, cur = [], {'level': 0, 'title': '', 'text': []}
    for kind, lv, text in items:
        if kind == 'heading':
            if cur['title'] or cur['text']:
                sections.append(cur)
            cur = {'level': lv, 'title': text, 'text': []}
        else:
            cur['text'].append(text)
    if cur['title'] or cur['text']:
        sections.append(cur)

    for s in sections:
        s['text'] = '\n\n'.join(s['text'])
    return sections


def main():
    index = json.load(io.open(os.path.join(RAW, 'index.json'), encoding='utf-8'))
    out = []
    for entry in index:
        title = entry['title']
        path = os.path.join(RAW, entry['file'])
        sections = parse_page(path)
        try:
            kind = site_type(raw_wikitext(title))
        except Exception:  # noqa: BLE001
            kind = ''
        chars = sum(len(s['text']) for s in sections)
        out.append({'title': title, 'type': kind, 'sections': sections, 'chars': chars})
        print('%-46s %-14s %2d 节 %7d 字符' % (title[:46], kind or '-', len(sections), chars))

    json.dump(out, io.open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('\n合计 %d 页，%d 字符 → %s' % (len(out), sum(o['chars'] for o in out), OUT))


if __name__ == '__main__':
    sys.exit(main())
