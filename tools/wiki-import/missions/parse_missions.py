# -*- coding: utf-8 -*-
"""解析任务页 wikitext → parsed.json。

任务页的骨架（实测三种都要兼容）：
  ① 单任务页：`{{Missiondetails|…}}` + `{{MissionBriefing|…}}` + 正文（可含 NPC 表）
  ② 多等级页：一个页面上有 **多个** `{{Missiondetails}}`（如 A Humble Gift 同时有 L1/L3 版），
     每个 infobox 之后跟它自己的 briefing/正文
  ③ 章节枢纽页：史诗任务线的 `Angel Sound/Chapter 1 - Rapture` 只有 `{{:任务名}}` 转写，
     真正的任务在独立页面里 → 记进 `includes`，交给 fetch 的第二轮补抓

产物 parsed.json：
  [{ title, cat, catCn, file, chars,
     variants: [{ info:{…}, briefing:"", sections:[{level,title,text,npcs:[…]}], npcs:[…] }],
     includes: ["The Balance of Power", …] }]
"""

import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
RAW_DIR = os.path.join(HERE, 'raw')
INDEX = os.path.join(HERE, 'index.json')
OUT = os.path.join(HERE, 'parsed.json')

# 页面上一律丢掉的外观/导航模板（无内容价值）
DROP_TEMPLATES = {
    'MissionLinks', 'NPCTableCSS', 'Clear', 'Missiondetails', 'MissionDetails',
    'Related class', 'Main', 'Open journal', 'Co', 'Color', 'Col',
    'Important note box', 'Note', 'Note box', 'Spoiler',
    # 页脚/导航类（会整块残留在正文里）
    'Missionrelated', 'MissionRelated', 'EpicArcNav', 'AngelEpicArcNav',
    'NPCTableSeparator', 'npcts', 'npcsep', 'NPCTableSep',
    'AmarrEpicArcNav', 'CaldariEpicArcNav', 'GallenteEpicArcNav', 'MinmatarEpicArcNav',
    'PirateEpicArcNav', 'SistersEpicArcNav', 'Navbox', 'Navbox subgroup',
}

INFO_FIELDS = [
    'Name', 'Level', 'Type', 'Objective', 'Faction', 'Faction1', 'Faction2',
    'WebPoint', 'EWAR', 'ShipSizeLimit', 'ShipSuggestion', 'Rewards',
    'StandingLoss', 'Extra', 'Damage', 'DamageDealt',
]


def read(path):
    return io.open(path, encoding='utf-8').read()


# ---------------------------------------------------------------- 模板解析
def find_templates(text, names):
    """找出顶层模板 `{{Name|...}}` 的 (start, end, name, body) 列表，支持嵌套花括号。"""
    out = []
    i = 0
    n = len(text)
    while True:
        j = text.find('{{', i)
        if j < 0:
            break
        # 名字
        k = j + 2
        while k < n and text[k] in ' \t\n':
            k += 1
        m = re.match(r'[A-Za-z0-9_\-\. :/]+', text[k:])
        if not m:
            i = j + 2
            continue
        name = m.group(0).strip()
        if name.lower() not in {n.lower() for n in names}:
            i = j + 2
            continue
        depth = 0
        p = j
        while p < n:
            if text.startswith('{{', p):
                depth += 1
                p += 2
                continue
            if text.startswith('}}', p):
                depth -= 1
                p += 2
                if depth == 0:
                    break
                continue
            p += 1
        out.append((j, p, name, text[j + 2 + len(name):p - 2]))
        i = p
    return out


def split_args(body):
    """把模板体按顶层 `|` 切成 kwargs（`|a=b`）与位置参数。"""
    parts = []
    depth_curly = depth_square = 0
    buf = []
    for idx in range(len(body)):
        ch = body[idx]
        if body.startswith('{{', idx):
            depth_curly += 1
        elif body.startswith('}}', idx):
            depth_curly -= 1
        elif ch == '[' and body.startswith('[[', idx):
            depth_square += 1
        elif ch == ']' and body[idx:idx + 2] == ']]':
            depth_square -= 1
        if ch == '|' and depth_curly == 0 and depth_square == 0:
            parts.append(''.join(buf))
            buf = []
            continue
        buf.append(ch)
    parts.append(''.join(buf))
    return parts


def parse_info(body):
    """{{Missiondetails|Level=…|Type=…}} → dict（键统一小写别名）。"""
    info = {}
    positional = []
    for part in split_args(body):
        if '=' in part:
            k, v = part.split('=', 1)
            k = k.strip()
            v = v.strip()
            if not v:
                continue
            info[k] = v
            # 兼容小写键
            info[k.lower()] = v
        else:
            p = part.strip()
            if p:
                positional.append(p)
    for i, p in enumerate(positional):
        if i < len(INFO_FIELDS):
            info.setdefault(INFO_FIELDS[i], p)
    return info


def parse_npc_row(body):
    parts = split_args(body)
    args = {}
    pos = []
    for p in parts:
        if '=' in p:
            k, v = p.split('=', 1)
            args[k.strip().lower()] = v.strip()
        else:
            # 模板体以 `|` 开头，split_args 的第一段恒为空串，必须跳过，
            # 否则"舰型/数量/名称"三列会整体错位（cls 空、name 变成数量）
            if p.strip():
                pos.append(p.strip())
    # {{NPCTableRow|Cruiser|3|Name|ewar=Web|trigger=…|note=…}}
    cls = pos[0] if len(pos) > 0 else ''
    count = pos[1] if len(pos) > 1 else ''
    name = pos[2] if len(pos) > 2 else ''
    return {
        'cls': cls, 'count': count, 'name': name,
        'ewar': args.get('ewar', ''), 'trigger': args.get('trigger', ''),
        'note': args.get('note', ''),
    }


# ---------------------------------------------------------------- 正文清理
# 这些模板**带正文**，不能整块删：拆成纯文本（取最后一个位置参数）
UNWRAP_TEMPLATES = {
    'MessageBox', 'messagebox', 'Co', 'co', 'Col', 'col', 'Color', 'color',
    'Icon', 'icon', 'Sh', 'sh', 'ShipType', 'shiptype', 'Note', 'note',
    'Note box', 'Notebox', 'Important note box', 'Quote', 'quote', 'Em', 'em',
}


def unwrap_templates(text, names=UNWRAP_TEMPLATES):
    """`{{Co|lime|文字}}` / `{{MessageBox|WARNING!|文字}}` → 文字（保留最后一个位置参数）。"""
    for _ in range(40):
        hit = None
        for start, end, name, body in find_templates(text, names):
            hit = (start, end, body)
            break
        if not hit:
            break
        start, end, body = hit
        parts = [p.strip() for p in split_args(body)]
        parts = [p for p in parts if p and '=' not in p]
        inner = parts[-1] if parts else ''
        text = text[:start] + (' ' + inner + ' ' if inner else '') + text[end:]
    return text


def strip_leftover_templates(text):
    """兜底：把还没清掉的 {{…}}（含嵌套）整块删掉，避免正文里出现 wiki 语法。"""
    for _ in range(60):
        start = text.find('{{')
        if start < 0:
            break
        depth = 0
        p = start
        end = -1
        while p < len(text):
            if text.startswith('{{', p):
                depth += 1
                p += 2
                continue
            if text.startswith('}}', p):
                depth -= 1
                p += 2
                if depth == 0:
                    end = p
                    break
                continue
            p += 1
        if end < 0:
            text = text[:start]
            break
        text = text[:start] + ' ' + text[end:]
    # 体积单位模板：{{m3|320}} 已在上面被删，这里补回单位
    return text


def strip_templates(text, drop=DROP_TEMPLATES):
    """去掉纯外观模板（保留模板内部不展开，避免把导航文案混进正文）。"""
    for _ in range(6):
        changed = False
        for start, end, name, _body in find_templates(text, drop):
            text = text[:start] + text[end:]
            changed = True
            break
        if not changed:
            break
    return text


def strip_wiki_markup(text):
    t = text
    t = re.sub(r'\[\[File:[^\]]*\]\]', '', t, flags=re.I)
    t = re.sub(r'\[\[Category:[^\]]*\]\]', '', t, flags=re.I)
    # [[目标|显示]] → 显示；[[目标]] → 目标
    t = re.sub(r'\[\[([^\]\|]+)\|([^\]]+)\]\]', r'\2', t)
    t = re.sub(r'\[\[([^\]]+)\]\]', r'\1', t)
    # 外链 [url 文字] → 文字
    t = re.sub(r'\[https?://\S+\s+([^\]]+)\]', r'\1', t)
    # '''粗体''' / ''斜体''
    t = re.sub(r"'{2,5}", '', t)
    # <ref>…</ref> 与自闭合
    t = re.sub(r'<ref[^>]*>.*?</ref>', '', t, flags=re.S | re.I)
    t = re.sub(r'<ref[^>]*/>', '', t, flags=re.I)
    # 常见行内标签
    t = re.sub(r'</?(sup|sub|span|small|big|br|em|strong|div|center|tt|code|nowiki|pre)[^>]*>', '', t, flags=re.I)
    t = t.replace('&nbsp;', ' ').replace('&amp;', '&')
    return t


def split_sections(text):
    """按 `== X ==` / `=== X ===` 切段，返回 [(level, title, body)]，首段 title 为空。"""
    lines = text.split('\n')
    out = []
    level_cur, title_cur, buf = 0, '', []
    for line in lines:
        m = re.match(r'^(={2,6})\s*(.+?)\s*\1\s*$', line)
        if m:
            out.append((level_cur, title_cur, '\n'.join(buf)))
            level_cur = len(m.group(1))
            title_cur = m.group(2)
            buf = []
        else:
            buf.append(line)
    out.append((level_cur, title_cur, '\n'.join(buf)))
    return out


def collect_npcs(text):
    """抽 NPC 表：返回 [{title, rows:[…]}]，并把表从文本里删掉。"""
    tables = []
    for start, end, _name, body in find_templates(text, {'NPCTableHead', 'npcth'}):
        tables.append({'title': strip_wiki_markup(body).strip(), 'start': start, 'end': end})
    rows = find_templates(text, {'NPCTableRow', 'npctr', 'npcrow'})
    # 把每条 row 归到它前面最近的 head
    for t in tables:
        t['rows'] = []
    for start, end, _name, body in rows:
        owner = None
        for t in tables:
            if t['start'] < start:
                owner = t
        row = parse_npc_row(body)
        if owner is not None:
            owner['rows'].append(row)
        else:
            if not tables:
                tables.append({'title': '', 'start': 0, 'end': 0, 'rows': []})
                owner = tables[0]
            owner['rows'].append(row)

    # 从文本里剔除 NPC 模板（head/row）与残留的 `|}` 表格收尾
    spans = [(t['start'], t['end']) for t in tables]
    spans += [(s, e) for s, e, _n, _b in rows]
    for s, e in sorted(spans, reverse=True):
        if e > s:
            text = text[:s] + text[e:]
    # 注意：这里**不能**顺手把 `{|` / `|}` 行删掉——正文里的 HTML 表格开头就在这两行之间，
    # 删了表格开头，后面的 `!`/`|` 单元格行就没法再转成 Markdown（踩过）。

    for t in tables:
        t.pop('start', None)
        t.pop('end', None)
    return tables, text


def convert_html_tables(text):
    """把 wiki 的 HTML 表格语法（`{|` … `|}` + `!`/`|` 行）转成 Markdown 表格。

    这类表格在 wiki 正文里很常见（采矿产出表、伤害波次表、掉落表），不转的话正文里会残留
    `! scope="col" | Ore Type` / `| style="text-align: left;" | Scordite` 这种原始语法。
    """
    out = []
    lines = text.split('\n')
    i = 0
    while i < len(lines):
        line = lines[i]
        if not line.strip().startswith('{|'):
            out.append(line)
            i += 1
            continue

        # 收集整个表格块
        block = []
        depth = 0
        while i < len(lines):
            cur = lines[i]
            if cur.strip().startswith('{|'):
                depth += 1
            if cur.strip().startswith('|}'):
                depth -= 1
                if depth <= 0:
                    i += 1
                    break
            block.append(cur)
            i += 1

        header, rows, current = [], [], []

        def cells_of(raw_line):
            body = raw_line[1:]
            # `style="…" | 文本` → 取最后一个 `|` 之后的部分
            if '|' in body and '=' in body.split('|')[0]:
                body = body.split('|')[-1]
            parts = [c.strip() for c in re.split(r'\|\||!!', body)]
            return [strip_wiki_markup(c) for c in parts if c.strip()]

        for raw_line in block:
            s = raw_line.strip()
            if not s or s.startswith('{|') or s.startswith('|}'):
                continue
            if s.startswith('|-'):
                # 行分隔符：把攒着的单元格收成一行（wiki 的每个单元格占一行，必须攒）
                if current:
                    rows.append(current)
                    current = []
                continue
            if not (s.startswith('!') or s.startswith('|')):
                continue
            cells = cells_of(s)
            if not cells:
                continue
            if s.startswith('!') and not rows and not current:
                header.extend(cells)
            else:
                current.extend(cells)

        if current:
            rows.append(current)

        if not header and rows:
            header, rows = rows[0], rows[1:]
        if not header:
            continue

        out.append('')
        out.append('| ' + ' | '.join(header) + ' |')
        out.append('|' + '---|' * len(header))
        for r in rows:
            cells = (r + [''] * len(header))[:len(header)]
            out.append('| ' + ' | '.join(cells) + ' |')
        out.append('')

    return '\n'.join(out)

def paragraphize(text):
    """把一段 wikitext 变成干净的段落/列表行序列。"""
    t = strip_wiki_markup(strip_leftover_templates(unwrap_templates(strip_templates(text))))
    t = convert_html_tables(t)
    # 转换后仍可能剩孤立的表格收尾符（原文里表格被 NPC 表剥离截断时）
    t = re.sub(r'(?m)^\s*\|}\s*$', '', t)
    lines = []
    for line in t.split('\n'):
        s = line.strip()
        if not s:
            if lines and lines[-1] != '':
                lines.append('')
            continue
        # 只丢 wiki 的行分隔符；**不能**丢 `| … |` 行——HTML 表格已经转成 Markdown，
        # 那些行正好是 Markdown 表格的行（早先的过滤条件会把它们一起吃掉）
        if s.startswith('|-') and not s.startswith('|---'):
            continue
        lines.append(s)
    # 去掉首尾空行与连续空行
    while lines and lines[0] == '':
        lines.pop(0)
    while lines and lines[-1] == '':
        lines.pop()
    out = []
    for line in lines:
        if line == '' and out and out[-1] == '':
            continue
        out.append(line)
    return '\n'.join(out)


def parse_page(entry):
    path = os.path.join(RAW_DIR, entry['file'])
    if not os.path.exists(path):
        return None
    text = read(path)

    includes = sorted({m.group(1).strip() for m in re.finditer(r'\{\{:([^\}|\}]+)', text)})

    infos = find_templates(text, {'Missiondetails', 'MissionDetails'})
    result = {
        'title': entry['title'], 'cat': entry['cat'], 'catCn': entry['catCn'],
        'file': entry['file'], 'chars': entry['chars'],
        'includes': includes, 'variants': [],
    }

    if not infos:
        # 没有 infobox：可能是章节枢纽/总览页，只留 includes 与纯文本
        body = paragraphize(text)
        result['variants'].append({
            'info': {}, 'briefing': '',
            'sections': [{'level': 0, 'title': '', 'text': body, 'npcs': []}] if body else [],
        })
        return result

    # 按 infobox 切段：每段 = 该 infobox 之后到下一个 infobox 之前
    bounds = [i[0] for i in infos] + [len(text)]
    for idx, (start, end, _name, body) in enumerate(infos):
        seg = text[end:bounds[idx + 1]]
        info = parse_info(body)

        briefing = ''
        briefings = find_templates(seg, {'MissionBriefing'})
        if briefings:
            b_start, b_end, _bn, b_body = briefings[0]
            # 模板体开头常见的 `|` 与引号要清掉，否则正文会出现 `|"The deal," …`
            b_body = b_body.lstrip('|').strip()
            briefing = paragraphize(b_body).strip('|').strip()
            seg = seg[:b_start] + seg[b_end:]

        npcs, seg_wo = collect_npcs(seg)
        # 单个 variant 内的 NPC 表按出现顺序挂在顶层
        sections = []
        for lvl, title, sbody in split_sections(seg_wo):
            clean = paragraphize(sbody)
            if not clean and not title:
                continue
            sections.append({'level': lvl, 'title': strip_wiki_markup(title), 'text': clean, 'npcs': []})

        # NPC 表统一放到 sections 末尾（界面里按标题顺序展示）
        if npcs:
            sections.append({'level': 0, 'title': '', 'text': '', 'npcs': npcs})

        result['variants'].append({'info': info, 'briefing': briefing, 'sections': sections})

    return result


def main():
    entries = json.load(io.open(INDEX, encoding='utf-8'))
    pages = []
    includes = set()
    for e in entries:
        p = parse_page(e)
        if not p:
            print('缺文件：%s' % e['file'])
            continue
        pages.append(p)
        includes.update(p['includes'])

    # 分类继承：史诗任务线里被 `{{:任务名}}` 转写的任务页往往自己不带分类，
    # 统一按"引用页的分类"继承，并记下所属任务线（arcOf），否则它们会全部堆进"安全任务"。
    by_title = {p['title']: p for p in pages}
    for page in pages:
        for inc in page.get('includes') or []:
            child = by_title.get(inc)
            if child is None:
                continue
            if child['cat'] == 0 and page['cat'] != 0:
                child['cat'] = page['cat']
                child['catCn'] = page['catCn']
                child['arcOf'] = page['title']

    json.dump(pages, io.open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

    known = {e['title'] for e in entries}
    missing = sorted(t for t in includes if t not in known)
    json.dump(missing, io.open(os.path.join(HERE, 'includes_missing.json'), 'w', encoding='utf-8'),
              ensure_ascii=False, indent=1)

    with_info = [p for p in pages if any(v['info'] for v in p['variants'])]
    variants = sum(1 for p in pages for v in p['variants'] if v['info'])
    print('解析 %d 页，其中带 infobox 的 %d 页、共 %d 个任务变体'
          % (len(pages), len(with_info), variants))
    print('转写缺页 %d 个（已写入 includes_missing.json）' % len(missing))
    for t in missing[:10]:
        print('   ', t)
    return 0


if __name__ == '__main__':
    sys.exit(main())
