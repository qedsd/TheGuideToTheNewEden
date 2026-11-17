"""把 `raw/pages/*.wiki` 解析成结构化的 JSON（英文原文），供 `build_sites.py` 翻译与生成。

产物
----
- `parsed.json` —— { page: {info, intro, pockets, loot, notes, extras, isStub} }

设计要点
--------
wiki 的站点页高度模板化（`{{CMBSiteInfo}}` + `{{NPCTableHead}}`/`{{NPCTableRow}}`），
这里按"花括号配平 + 顶层管道切分"的方式解析，不用正则硬切，避免被嵌套模板带偏。

用法
----
    python parse_wiki.py
"""

import io
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
PAGES = os.path.join(HERE, 'raw', 'pages')

NPC_CLASS_CN = {
    'frigate': '护卫',
    'elite frigate': '精英护卫',
    'commander frigate': '指挥官护卫',
    'destroyer': '驱逐',
    'elite destroyer': '精英驱逐',
    'cruiser': '巡洋',
    'elite cruiser': '精英巡洋',
    'commander cruiser': '指挥官巡洋',
    'battlecruiser': '战巡',
    'elite battlecruiser': '精英战巡',
    'battleship': '战列',
    'elite battleship': '精英战列',
    'commander battleship': '指挥官战列',
    'commander': '指挥官',
    'capital': '旗舰',
    'carrier': '航母',
    'dreadnought': '无畏',
    'sentry': '岗哨',
    'structure': '建筑',
    'station': '空间站',
    'acceleration gate': '加速轨道',
    'asteroid': '小行星',
    'hangar array': '机库阵列',
    'deadspace': '死亡空间',
    'drone': '无人机',
    'elite drone': '精英无人机',
    'container': '集装箱',
    'wreck': '残骸',
}


def find_templates(text, name):
    """返回名字为 name 的全部模板正文（不含外层 {{ }}），支持嵌套。"""
    out = []
    needle = '{{' + name
    i = 0
    while True:
        i = text.find(needle, i)
        if i < 0:
            break
        # 必须恰好是模板名（后面跟 | 或 }} 或空白换行）
        j = i + len(needle)
        if j < len(text) and text[j] not in ('|', '}', '\n', ' ', '\t'):
            i += 2
            continue
        depth = 0
        k = i
        while k < len(text) - 1:
            if text[k:k+2] == '{{':
                depth += 1
                k += 2
                continue
            if text[k:k+2] == '}}':
                depth -= 1
                k += 2
                if depth == 0:
                    out.append(text[i+2:k-2])
                    break
                continue
            k += 1
        i = k
    return out


def split_args(body):
    """把模板正文按顶层 `|` 切开（忽略嵌在 {{}} / [[]] / <nowiki> 里的管道）。"""
    parts, buf = [], []
    depth_curly = depth_square = 0
    i = 0
    while i < len(body):
        two = body[i:i+2]
        if two == '{{':
            depth_curly += 1
            buf.append(two)
            i += 2
            continue
        if two == '}}':
            depth_curly = max(0, depth_curly - 1)
            buf.append(two)
            i += 2
            continue
        if two == '[[':
            depth_square += 1
            buf.append(two)
            i += 2
            continue
        if two == ']]':
            depth_square = max(0, depth_square - 1)
            buf.append(two)
            i += 2
            continue
        c = body[i]
        if c == '|' and depth_curly == 0 and depth_square == 0:
            parts.append(''.join(buf))
            buf = []
            i += 1
            continue
        buf.append(c)
        i += 1
    parts.append(''.join(buf))
    return parts


def parse_params(body):
    """`a=1|b=2|c` -> ({'a':'1','b':'2'}, ['c'])

    注意：模板正文是从 `{{模板名` 之后切下来的，所以开头会带一个 `|`，
    `split_args` 会把它切成一个空的首段。这里丢掉**一个**空首段，
    但保留真实的空参数（例如 `{{NPCTableRow||3|Name}}` 的空吨位）。
    """
    args = split_args(body)
    if len(args) > 1 and not args[0].strip():
        args = args[1:]
    named, positional = {}, []
    for seg in args:
        m = re.match(r'^\s*([A-Za-z0-9_ \-]+?)\s*=\s*(.*)$', seg, re.S)
        if m:
            named[m.group(1).strip().lower()] = m.group(2).strip()
        else:
            positional.append(seg.strip())
    return named, positional


LINK_RE = re.compile(r'\[\[(?:[^\]\|]*\|)?([^\]]*)\]\]')
FILE_RE = re.compile(r'\[\[File:[^\]]*\]\]', re.I)
TMPL_DROP = re.compile(r'\{\{(?:co|Color box|colour|sh|icon|sic)\|', re.I)


def clean(text):
    """去掉 wiki 标记，留下可读纯文本。"""
    if not text:
        return ''
    t = text
    t = FILE_RE.sub('', t)
    # {{co|#fff|文字}} -> 文字
    t = re.sub(r'\{\{co\|[^|]*\|([^}]*)\}\}', r'\1', t, flags=re.I)
    t = re.sub(r'\{\{sh\|([^}]*)\}\}', r'\1', t, flags=re.I)
    t = re.sub(r'<br\s*/?>', ' ', t, flags=re.I)
    t = re.sub(r'<[^>]+>', '', t)
    t = LINK_RE.sub(r'\1', t)
    t = t.replace("'''", '').replace("''", '')
    t = re.sub(r'\{\{[^{}]*\}\}', '', t)
    # 表格/模板的残骸行：`|}``|-``{{``}}`
    t = re.sub(r'^\s*\|?[}|].*$', '', t, flags=re.M)
    t = re.sub(r'^\s*\{\{.*$', '', t, flags=re.M)
    t = t.replace('{{', '').replace('}}', '').replace('|}', '')
    t = t.replace('&nbsp;', ' ')
    t = re.sub(r'[ \t]+', ' ', t)
    t = re.sub(r'\n{3,}', '\n\n', t)
    return t.strip(' \n\t:：|')


def split_sections(text):
    """按 == / === / ==== 切段：[(level, title, body)]，第一段 title 为 ''。"""
    lines = text.split('\n')
    out, cur_title, cur_level, buf = [], '', 0, []
    head = re.compile(r'^(={2,6})\s*(.*?)\s*\1\s*$')
    for ln in lines:
        m = head.match(ln.strip())
        if m:
            out.append((cur_level, cur_title, '\n'.join(buf)))
            cur_level = len(m.group(1))
            cur_title = m.group(2).strip()
            buf = []
        else:
            buf.append(ln)
    out.append((cur_level, cur_title, '\n'.join(buf)))
    return out


def parse_npc_blocks(body):
    """把一个段落里所有 NPCTableHead/Row/Separator 解析成有序块。"""
    blocks = []
    cur = None
    for raw in find_templates(body, 'NPCTableHead'):
        pass  # 仅用于确认存在；真正的顺序遍历在下面用正则扫描

    token = re.compile(r'\{\{(NPCTableHead|NPCTableRow|NPCTableSeparator)(.*?)\}\}', re.S)
    for m in token.finditer(body):
        kind, inner = m.group(1), m.group(2)
        if kind == 'NPCTableHead':
            named, pos = parse_params(inner)
            cur = {'title': clean(pos[0]) if pos else '', 'rows': []}
            blocks.append(cur)
        elif kind == 'NPCTableSeparator':
            named, pos = parse_params(inner)
            if cur is None:
                cur = {'title': '', 'rows': []}
                blocks.append(cur)
            cur['rows'].append({'separator': clean(pos[0]) if pos else ''})
        else:
            if cur is None:
                cur = {'title': '', 'rows': []}
                blocks.append(cur)
            named, pos = parse_params(inner)
            pos = [clean(p) for p in pos]
            row = {
                'cls': pos[0] if len(pos) > 0 else '',
                'count': pos[1] if len(pos) > 1 else '',
                'name': pos[2] if len(pos) > 2 else '',
                'ewar': clean(named.get('ewar', '')),
                'point': clean(named.get('point', '')),
                'trigger': clean(named.get('trigger', '')),
                'note': clean(named.get('note', '')),
                'cargo': clean(named.get('cargo', named.get('drop', ''))),
            }
            if not any(row[k] for k in ('cls', 'name', 'trigger', 'note', 'cargo')):
                continue
            cur['rows'].append(row)
    return [b for b in blocks if b['rows']]


def parse_loot(body):
    rows = []
    for tmpl in ('Loot', 'LootTable'):
        pass
    # 手工解析 == Loot == 里的 wikitables
    for m in re.finditer(r'\{\|(.+?)\n\|\}', body, re.S):
        tbl = m.group(1)
        cells, headers = [], []
        for ln in tbl.split('\n'):
            s = ln.strip()
            if not s or s.startswith('|-') or s.startswith('|+') or s.startswith('|}'):
                continue
            if s.startswith('!'):
                headers.append(clean(s.lstrip('!').replace('!!', '\n')))
            elif s.startswith('|'):
                parts = [clean(x) for x in s.lstrip('|').split('||')]
                cells.append(parts)
        if cells:
            for c in cells:
                if len(c) >= 2:
                    rows.append(c)
    return rows


def parse_page(title, text):
    info = {}
    for body in find_templates(text, 'CMBSiteInfo')[:1]:
        named, _ = parse_params(body)
        info = named

    is_stub = bool(find_templates(text, 'Stub')) or '{{stub}}' in text.lower()

    sections = split_sections(text)
    intro_parts, pockets, loot, notes, rewards, escalation = [], [], [], [], [], []

    for sec_no, (level, stitle, sbody) in enumerate(sections):
        low = stitle.lower()
        if level == 0:
            # 首段：去掉模板与分类后的正文
            body = re.sub(r'\{\{[^{}]*\}\}', '', sbody)
            body = re.sub(r'\[\[Category:[^\]]*\]\]', '', body)
            txt = clean(body)
            if txt:
                intro_parts.append(txt)
            continue
        if 'loot' in low or 'reward' in low:
            loot.extend(parse_loot(sbody))
            txt = re.sub(r'\{\|.+?\n\|\}', '', sbody, flags=re.S)
            txt = re.sub(r'\[\[Category:[^\]]*\]\]', '', txt)
            t = clean(txt)
            if t:
                rewards.append({'title': stitle, 'text': t, 'order': sec_no})
            continue
        if 'escalat' in low:
            t = clean(re.sub(r'\[\[Category:[^\]]*\]\]', '', sbody))
            if t:
                escalation.append({'title': stitle, 'text': t, 'order': sec_no})
            continue
        blocks = parse_npc_blocks(sbody)
        prose = re.sub(r'\{\{NPCTable(?:Head|Row|Separator).*?\}\}', '', sbody, flags=re.S)
        prose = re.sub(r'\{\|.+?\n\|\}', '', prose, flags=re.S)
        prose = re.sub(r'\[\[Category:[^\]]*\]\]', '', prose)
        prose = clean(prose)
        if blocks:
            pockets.append({'title': stitle, 'prose': prose, 'blocks': blocks, 'order': sec_no})
        elif prose:
            # 纯文字段落（说明、警告、打法）
            notes.append({'title': stitle, 'text': prose, 'order': sec_no})

    # 消息框里的文字（DB 描述、warp-in 提示、警告）
    messages = []
    for tmpl in ('MessageBox', 'Note box', 'Important note box', 'Color box'):
        for body in find_templates(text, tmpl):
            named, pos = parse_params(body)
            bits = [clean(p) for p in pos if clean(p)]
            for k, v in named.items():
                if k in ('collapsed', 'italics', 'color', 'border', 'bar'):
                    continue
                if clean(v):
                    bits.append(clean(v))
            if bits:
                messages.append(bits)

    desc = []
    for body in find_templates(text, 'Description'):
        _, pos = parse_params(body)
        desc.extend(clean(p) for p in pos if clean(p))

    return {
        'page': title,
        'name': clean(info.get('name', '')) or title.replace('_', ' '),
        'type': clean(info.get('type', '')),
        'rating': clean(info.get('rating', '')),
        'location': clean(info.get('location', '')),
        'shipLimit': clean(info.get('ship limit', '')),
        'faction': clean(info.get('faction', '')),
        'signature': clean(info.get('signature strength', '')),
        'isStub': is_stub,
        'intro': intro_parts,
        'pockets': pockets,
        'lootRows': loot,
        'notes': notes,
        'rewards': rewards,
        'escalation': escalation,
        'messages': messages,
        'desc': desc,
    }


def safe_name(title):
    """与 fetch_wiki.py 保持一致的 Windows 文件名映射。"""
    table = {'?': '？', ':': '：', '*': '＊', '"': '＂', '<': '＜', '>': '＞', '|': '｜', '/': '／'}
    for bad, good in table.items():
        title = title.replace(bad, good)
    return title


def load_title_map():
    """safe 文件名 -> 原始 wiki 页名。"""
    path = os.path.join(HERE, 'index.json')
    if not os.path.exists(path):
        return {}
    idx = json.load(io.open(path, encoding='utf-8'))
    out = {}
    for bucket, items in idx.items():
        if isinstance(items, list):
            for it in items:
                page = it.get('page')
                if page:
                    out[safe_name(page)] = page
    return out


def main():
    title_map = load_title_map()
    files = sorted(f for f in os.listdir(PAGES) if f.endswith('.wiki'))
    out = {}
    for f in files:
        stem = f[:-5]
        title = title_map.get(stem, stem)
        text = io.open(os.path.join(PAGES, f), encoding='utf-8', errors='replace').read()
        if text.lstrip().startswith('#REDIRECT'):
            continue
        out[title] = parse_page(title, text)
    json.dump(out, io.open(os.path.join(HERE, 'parsed.json'), 'w', encoding='utf-8'),
              ensure_ascii=False, indent=1)
    print('解析完成：%d 页' % len(out))
    # 粗略统计待翻译正文量
    chars = 0
    for v in out.values():
        for s in v['intro']:
            chars += len(s)
        for p in v['pockets']:
            chars += len(p['prose'])
        for sec in v['notes']:
            chars += len(sec['text'])
        for sec in v['rewards']:
            chars += len(sec['text'])
        for sec in v['escalation']:
            chars += len(sec['text'])
        for m in v['messages']:
            chars += sum(len(x) for x in m)
        for d in v['desc']:
            chars += len(d)
    print('待翻译英文正文约 %d 字符（%.0f KB）' % (chars, chars / 1024.0))
    stubs = sum(1 for v in out.values() if v['isStub'])
    withnpc = sum(1 for v in out.values() if v['pockets'])
    print('stub 页 %d，含 NPC 表的页 %d' % (stubs, withnpc))


if __name__ == '__main__':
    main()

